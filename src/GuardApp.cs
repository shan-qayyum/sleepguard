// Tray app: turns protection on after a stretch without input (or on the hotkey), reads the PIN from the
// blocked keys, runs the sleep timer, and turns protection off on the right PIN or the Windows password.

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SleepGuard
{
    public class GuardApp : ApplicationContext
    {
        const int SoundWindowSeconds = 60;   // "sound is playing" = heard within the last minute
        const int RefreshHookSeconds = 60;
        const float SilenceLevel = 0.0005f;

        readonly NotifyIcon tray = new NotifyIcon();
        readonly InputBlocker blocker = new InputBlocker();
        readonly PinPrompt prompt = new PinPrompt();
        readonly AudioMeter audio = new AudioMeter();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 1000 };
        readonly ToolStripMenuItem turnOnItem = new ToolStripMenuItem();
        HotkeyWindow hotkey;
        SettingsForm settingsOpen;
        Icon iconOn, iconOff;

        const int PinPauseSeconds = 5;               // a half-typed PIN is forgotten after this long without typing

        string typed = "";
        int wrongTries, wrongWhileOn;
        DateTime lastWrong, lastKey;
        int quietSince = Environment.TickCount;     // keys swallowed while protected may not count as input
        int lastSoundTick = Environment.TickCount - SoundWindowSeconds * 2000;
        int onSinceTick, lastRefreshTick;
        bool sleepTimerDone;
        bool checking;                                // a PIN check is running on a worker thread

        public bool Guarding { get; private set; }

        public GuardApp()
        {
            Settings.Load();
            if (!Settings.HasPin)
            {
                using (var setup = new PinSetup(false))
                    if (setup.ShowDialog() != DialogResult.OK) { Environment.Exit(0); return; }
                Activity.Add("PIN set up");
            }

            prompt.CreateControl();                  // a window handle to marshal hook events onto the UI thread
            IntPtr unused = prompt.Handle;
            iconOn = MakeIcon(Color.FromArgb(220, 60, 50));
            iconOff = MakeIcon(Color.FromArgb(110, 116, 125));

            var menu = new ContextMenuStrip();
            turnOnItem.Click += delegate { TurnOn("menu"); };
            menu.Items.Add(turnOnItem);
            menu.Items.Add("Settings and activity...", null, delegate { ShowSettings(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, delegate { ExitThread(); });
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { ShowSettings(); };
            UpdateTray();
            tray.Visible = true;

            blocker.KeyPressed += OnKeyFromHook;
            RegisterHotkey(true);
            SystemEvents.SessionSwitch += OnSessionSwitch;
            timer.Tick += delegate { Tick(); };
            timer.Start();
            Activity.Add("SleepGuard started");
        }

        // ---------- decisions (static so they can be tested) ----------

        // Seconds since the last real input, never counting from before protection was last turned off.
        public static int IdleSeconds(uint lastInputTick, int nowTick, int quietSinceTick)
        {
            int sinceInput = unchecked(nowTick - (int)lastInputTick) / 1000;
            int sinceQuiet = unchecked(nowTick - quietSinceTick) / 1000;
            return Math.Max(0, Math.Min(sinceInput, sinceQuiet));
        }

        public static bool ShouldTurnOn(bool guarding, bool autoEnabled, int autoMinutes, int idleSeconds, bool onlyWhenPlaying, bool soundRecently)
        {
            if (guarding || !autoEnabled) return false;
            if (idleSeconds < autoMinutes * 60) return false;
            return !onlyWhenPlaying || soundRecently;
        }

        public static bool SleepTimerDue(bool guarding, bool enabled, bool done, int minutes, int onSeconds)
        {
            return guarding && enabled && !done && onSeconds >= minutes * 60;
        }

        public static char KeyToChar(Keys key)
        {
            if (key >= Keys.D0 && key <= Keys.D9) return (char)('0' + (key - Keys.D0));
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9) return (char)('0' + (key - Keys.NumPad0));
            if (key >= Keys.A && key <= Keys.Z) return (char)('A' + (key - Keys.A));
            return '\0';
        }

        // ---------- every second ----------

        void Tick()
        {
            int now = Environment.TickCount;
            float peak = audio.Peak();
            if (peak > SilenceLevel) lastSoundTick = now;
            bool soundRecently = unchecked(now - lastSoundTick) < SoundWindowSeconds * 1000;

            if (!Guarding)
            {
                uint last = Native.LastInputTick();
                if (last == 0) return;
                int idle = IdleSeconds(last, now, quietSince);
                if (ShouldTurnOn(false, Settings.AutoEnabled, Settings.AutoMinutes, idle, Settings.OnlyWhenPlaying, soundRecently))
                    TurnOn("no input for " + Settings.AutoMinutes + " min");
                return;
            }

            if (unchecked(now - lastRefreshTick) >= RefreshHookSeconds * 1000)
            {
                lastRefreshTick = now;
                if (!blocker.Refresh()) Activity.Add("Warning: could not re-attach the input block");
            }

            int onSeconds = unchecked(now - onSinceTick) / 1000;
            if (SleepTimerDue(Guarding, Settings.SleepTimerEnabled, sleepTimerDone, Settings.SleepTimerMinutes, onSeconds))
            {
                sleepTimerDone = true;
                bool playingNow = unchecked(now - lastSoundTick) < 5000;
                if (playingNow) Native.PressPlayPause();      // only pause; never start something that has stopped
                if (Settings.SleepTimerScreenOff) Native.TurnDisplayOff();
                Activity.Add("Sleep timer: " + (playingNow ? "paused playback" : "nothing was playing")
                             + (Settings.SleepTimerScreenOff ? ", screen off" : ""));
            }
        }

        // ---------- on / off ----------

        public void TurnOn(string reason)
        {
            if (Guarding) return;
            if (!blocker.Start())
            {
                Activity.Add("FAILED to turn on (" + reason + "): Windows did not allow blocking input");
                tray.ShowBalloonTip(5000, "SleepGuard", "Windows did not allow blocking input. The laptop is NOT protected.", ToolTipIcon.Error);
                return;
            }
            Guarding = true;
            typed = "";
            wrongTries = wrongWhileOn = 0;
            sleepTimerDone = false;
            onSinceTick = lastRefreshTick = Environment.TickCount;
            if (settingsOpen != null) settingsOpen.Close();
            UpdateTray();
            Activity.Add("Protection ON (" + reason + ")");
            if (Settings.NotifyOnTurnOn)
                tray.ShowBalloonTip(3000, "SleepGuard is on", "Keyboard and mouse are blocked until the PIN is typed.", ToolTipIcon.Info);
        }

        void TurnOff(string reason)
        {
            blocker.Stop();
            prompt.HideNow();
            Guarding = false;
            typed = "";
            quietSince = Environment.TickCount;
            UpdateTray();
            Activity.Add("Protection OFF (" + reason + ")");
            if (wrongWhileOn > 0)
                tray.ShowBalloonTip(10000, "While you were away",
                    wrongWhileOn + (wrongWhileOn == 1 ? " wrong PIN attempt" : " wrong PIN attempts") + ", last at " + lastWrong.ToString("HH:mm") +
                    ". Details: tray icon > Settings and activity.", ToolTipIcon.Warning);
            wrongTries = wrongWhileOn = 0;
        }

        // Runs on the hook thread, which must return quickly: hand the key to the UI thread.
        void OnKeyFromHook(Keys key)
        {
            if (!Guarding) return;
            try { prompt.BeginInvoke(new Action<Keys>(HandleKey), key); }
            catch (InvalidOperationException) { }
        }

        void HandleKey(Keys key)
        {
            if (!Guarding) return;
            DateTime now = DateTime.Now;
            if ((now - lastKey).TotalSeconds > PinPauseSeconds) typed = "";   // start over after a pause
            lastKey = now;
            char c = KeyToChar(key);
            if (c != '\0') { if (typed.Length < 16) typed += c; }
            else if (key == Keys.Back) { if (typed.Length > 0) typed = typed.Substring(0, typed.Length - 1); }
            else if (key == Keys.Escape) typed = "";
            else if (key == Keys.Enter) { CheckTyped(); return; }
            if (Settings.ShowPrompt)
                prompt.ShowState("Protected - type the PIN, then Enter  (Esc clears)", Color.FromArgb(232, 234, 237), typed.Length);
        }

        // The PIN hash is deliberately slow, so it is checked on a worker thread; the result comes back to the UI thread.
        void CheckTyped()
        {
            string attempt = typed;
            typed = "";
            if (attempt.Length == 0 || checking) return;
            checking = true;
            if (Settings.ShowPrompt) prompt.ShowState("Checking...", Color.FromArgb(232, 234, 237), 0);
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool ok = Settings.CheckPin(attempt);
                try { prompt.BeginInvoke(new Action<bool>(PinResult), ok); }
                catch (InvalidOperationException) { }
            });
        }

        void PinResult(bool ok)
        {
            checking = false;
            if (!Guarding) return;
            if (ok) { TurnOff("PIN"); return; }
            wrongTries++; wrongWhileOn++;
            lastWrong = DateTime.Now;
            Activity.Add("Wrong PIN attempt (" + wrongTries + " of " + Settings.MaxWrongTries + ")");
            if (wrongTries >= Settings.MaxWrongTries)
            {
                // Someone is guessing: hand over to the Windows lock screen, which needs the account password.
                prompt.HideNow();
                wrongTries = 0;
                Activity.Add("Windows locked after " + Settings.MaxWrongTries + " wrong PINs");
                Native.LockWorkStation();
                return;
            }
            if (Settings.ShowPrompt)
                prompt.ShowState("Wrong PIN - " + (Settings.MaxWrongTries - wrongTries) + " left before Windows locks",
                    Color.FromArgb(240, 85, 80), 0);
        }

        void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            // Getting past the Windows lock screen proves it is the owner, which also covers a forgotten PIN.
            if (e.Reason == SessionSwitchReason.SessionUnlock && Guarding) TurnOff("Windows password");
        }

        // ---------- tray, settings, hotkey ----------

        void ShowSettings()
        {
            if (Guarding) return;
            if (settingsOpen != null) { settingsOpen.Activate(); return; }
            char before = Settings.HotkeyLetter;
            using (settingsOpen = new SettingsForm())
            {
                settingsOpen.ShowDialog();
            }
            settingsOpen = null;
            if (Settings.HotkeyLetter != before) RegisterHotkey(true);
            UpdateTray();
        }

        void RegisterHotkey(bool warn)
        {
            if (hotkey != null) hotkey.Dispose();
            hotkey = new HotkeyWindow(Settings.HotkeyLetter, delegate { TurnOn("hotkey"); });
            if (!hotkey.Registered && warn)
                tray.ShowBalloonTip(6000, "SleepGuard", "Ctrl+Alt+" + Settings.HotkeyLetter +
                    " is already used by another program. Pick another letter in Settings, or use the tray icon.", ToolTipIcon.Warning);
        }

        void UpdateTray()
        {
            tray.Icon = Guarding ? iconOn : iconOff;
            string auto = Settings.AutoEnabled ? "auto after " + Settings.AutoMinutes + " min" : "auto off";
            string t = Guarding ? "SleepGuard: ON - keyboard and mouse blocked" : "SleepGuard: off (" + auto + ", Ctrl+Alt+" + Settings.HotkeyLetter + ")";
            tray.Text = t.Length > 63 ? t.Substring(0, 63) : t;
            turnOnItem.Text = "Turn on now  (Ctrl+Alt+" + Settings.HotkeyLetter + ")";
        }

        static Icon MakeIcon(Color fill)
        {
            using (var bmp = new Bitmap(16, 16))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                using (var brush = new SolidBrush(fill))
                using (var font = new Font("Arial", 10f, FontStyle.Bold, GraphicsUnit.Pixel))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.FillEllipse(brush, 0, 0, 15, 15);
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    SizeF size = g.MeasureString("Z", font);
                    g.DrawString("Z", font, Brushes.White, (16 - size.Width) / 2 + 0.5f, (16 - size.Height) / 2 + 0.5f);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        protected override void ExitThreadCore()
        {
            if (Guarding) return;                    // never exit while protecting (input is blocked anyway)
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            timer.Stop();
            blocker.Dispose();
            if (hotkey != null) hotkey.Dispose();
            prompt.Dispose();
            tray.Visible = false;
            tray.Dispose();
            Activity.Add("SleepGuard exited");
            base.ExitThreadCore();
        }
    }

    // Receives the global Ctrl+Alt+<letter> hotkey.
    sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        const int WM_HOTKEY = 0x0312, Id = 1;
        const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_NOREPEAT = 0x4000;
        readonly Action pressed;
        public readonly bool Registered;

        public HotkeyWindow(char letter, Action onPressed)
        {
            pressed = onPressed;
            CreateHandle(new CreateParams());
            Registered = RegisterHotKey(Handle, Id, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, (uint)char.ToUpperInvariant(letter));
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY) pressed();
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (Registered) UnregisterHotKey(Handle, Id);
            DestroyHandle();
        }
    }

    static class Program
    {
        public const string Version = "1.0.0";

        [STAThread]
        static void Main()
        {
            bool first;
            using (var single = new Mutex(true, @"Local\SleepGuard_SingleInstance", out first))
            {
                if (!first) return;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new GuardApp());
            }
        }
    }
}
