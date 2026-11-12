// The main window: status, the big "Protect now" button, the everyday settings as switches, and a summary of
// the last 24 hours. Closing it keeps SleepGuard running in the tray.

using System;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SleepGuard
{
    public class MainWindow : Form
    {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);

        const int W = 480;
        readonly Action protect, openSettings, changePin, settingsChanged;
        readonly Label statusText = new Label(), statusDetail = new Label(), hint = new Label(), summary = new Label();
        readonly Panel statusDot = new Panel();
        readonly PillButton bProtect = new PillButton();
        readonly Toggle tAuto = new Toggle(), tPlaying = new Toggle(), tSleep = new Toggle();
        readonly NumericUpDown nAuto = new NumericUpDown(), nSleep = new NumericUpDown();
        readonly Label lPlaying = new Label(), lAfter = new Label(), lAfterTail = new Label(), lSleepA = new Label(), lSleepB = new Label();
        bool loading = true, toldAboutTray;
        public bool AllowExit;

        public MainWindow(Action protect, Action openSettings, Action changePin, Action settingsChanged)
        {
            this.protect = protect;
            this.openSettings = openSettings;
            this.changePin = changePin;
            this.settingsChanged = settingsChanged;

            Text = "SleepGuard";
            Icon = Icons.App;
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            Font = Theme.Font(9.5f, FontStyle.Regular);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(W, 664);
            KeyPreview = true;

            // Header
            var logo = new PictureBox { Image = Icons.Draw(48), SizeMode = PictureBoxSizeMode.Normal, Bounds = new Rectangle(22, 18, 48, 48), BackColor = Color.Transparent };
            Controls.Add(logo);
            Controls.Add(new Label { Text = "SleepGuard", AutoSize = true, Left = 80, Top = 16, Font = Theme.Font(18f, FontStyle.Bold), ForeColor = Theme.Text });
            Controls.Add(new Label { Text = "Your video keeps playing. Everyone else is locked out.", AutoSize = true, Left = 82, Top = 54, ForeColor = Theme.Dim });

            // Status card
            var status = AddCard(86, 182);
            statusDot.SetBounds(22, 26, 12, 12);
            statusDot.Paint += delegate(object s, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.Clear(Theme.Card);
                using (var b = new SolidBrush(statusDot.ForeColor)) e.Graphics.FillEllipse(b, 0, 0, 11, 11);
            };
            status.Controls.Add(statusDot);
            Style(statusText, 42, 16, 15f, FontStyle.Bold, Theme.Text);
            status.Controls.Add(statusText);
            statusDetail.SetBounds(22, 50, W - 84, 40);
            statusDetail.ForeColor = Theme.Dim;
            statusDetail.BackColor = Color.Transparent;
            status.Controls.Add(statusDetail);
            bProtect.SetBounds(20, 96, W - 80, 48);
            bProtect.Text = "Protect now";
            bProtect.Click += delegate { protect(); };
            status.Controls.Add(bProtect);
            hint.SetBounds(20, 150, W - 80, 20);
            hint.TextAlign = ContentAlignment.MiddleCenter;
            hint.ForeColor = Theme.Dim;
            hint.BackColor = Color.Transparent;
            hint.Font = Theme.Font(8.75f, FontStyle.Regular);
            status.Controls.Add(hint);

            // Automatic turn-on
            var auto = AddCard(282, 128);
            AddTitle(auto, "Turn on by itself", 16);
            PlaceToggle(auto, tAuto, 14);
            Style(lAfter, 20, 54, 9.5f, FontStyle.Regular, Theme.Text); lAfter.Text = "after";
            auto.Controls.Add(lAfter);
            DarkNumber(nAuto, 1, 240);
            nAuto.Location = new Point(lAfter.Right + 6, 51);
            auto.Controls.Add(nAuto);
            Style(lAfterTail, nAuto.Right + 6, 54, 9.5f, FontStyle.Regular, Theme.Text); lAfterTail.Text = "minutes without keyboard or mouse";
            auto.Controls.Add(lAfterTail);
            Style(lPlaying, 20, 90, 9.5f, FontStyle.Regular, Theme.Text); lPlaying.Text = "Only while sound is playing";
            auto.Controls.Add(lPlaying);
            PlaceToggle(auto, tPlaying, 86);

            // Sleep timer
            var sleep = AddCard(424, 92);
            AddTitle(sleep, "Sleep timer", 16);
            PlaceToggle(sleep, tSleep, 14);
            Style(lSleepA, 20, 54, 9.5f, FontStyle.Regular, Theme.Text); lSleepA.Text = "Pause the video after";
            sleep.Controls.Add(lSleepA);
            DarkNumber(nSleep, 1, 480);
            nSleep.Location = new Point(lSleepA.Right + 6, 51);
            sleep.Controls.Add(nSleep);
            Style(lSleepB, nSleep.Right + 6, 54, 9.5f, FontStyle.Regular, Theme.Text); lSleepB.Text = "minutes of protection";
            sleep.Controls.Add(lSleepB);

            // Last 24 hours
            var day = AddCard(530, 70);
            AddTitle(day, "Last 24 hours", 12);
            summary.SetBounds(20, 38, W - 84, 22);
            summary.ForeColor = Theme.Dim;
            summary.BackColor = Color.Transparent;
            day.Controls.Add(summary);

            // Footer links
            int x = 22;
            x = AddLink("All settings", x, 614, delegate { openSettings(); }) + 22;
            x = AddLink("Activity log", x, 614, delegate { openSettings(); }) + 22;
            AddLink("Change PIN", x, 614, delegate { changePin(); });
            var note = new Label { Text = "Closing this window keeps SleepGuard running in the tray.", AutoSize = true, Top = 638, ForeColor = Theme.Dim, Font = Theme.Font(8.5f, FontStyle.Regular) };
            Controls.Add(note);
            note.Left = 22;

            // Changes save at once.
            tAuto.CheckedChanged += delegate { if (loading) return; Settings.AutoEnabled = tAuto.Checked; Changed(); };
            tPlaying.CheckedChanged += delegate { if (loading) return; Settings.OnlyWhenPlaying = tPlaying.Checked; Changed(); };
            tSleep.CheckedChanged += delegate { if (loading) return; Settings.SleepTimerEnabled = tSleep.Checked; Changed(); };
            nAuto.ValueChanged += delegate { if (loading) return; Settings.AutoMinutes = (int)nAuto.Value; Changed(); };
            nSleep.ValueChanged += delegate { if (loading) return; Settings.SleepTimerMinutes = (int)nSleep.Value; Changed(); };

            RefreshAll(false);
        }

        // ---------- building blocks ----------

        Card AddCard(int top, int height)
        {
            var c = new Card();
            c.SetBounds(20, top, W - 40, height);
            Controls.Add(c);
            return c;
        }

        void AddTitle(Control card, string text, int top)
        {
            var l = new Label { Text = text, AutoSize = true, Left = 20, Top = top, Font = Theme.Font(11f, FontStyle.Bold), ForeColor = Theme.Text, BackColor = Color.Transparent };
            card.Controls.Add(l);
        }

        void PlaceToggle(Control card, Toggle t, int top)
        {
            t.Location = new Point(card.Width - t.Width - 18, top);
            card.Controls.Add(t);
        }

        static void Style(Label l, int x, int y, float size, FontStyle style, Color color)
        {
            l.AutoSize = true; l.Left = x; l.Top = y; l.Font = Theme.Font(size, style); l.ForeColor = color; l.BackColor = Color.Transparent;
        }

        static void DarkNumber(NumericUpDown n, int min, int max)
        {
            n.Minimum = min; n.Maximum = max; n.Width = 58;
            n.BackColor = Theme.Back; n.ForeColor = Theme.Text; n.BorderStyle = BorderStyle.FixedSingle;
            n.Font = Theme.Font(9.5f, FontStyle.Regular);
            n.TextAlign = HorizontalAlignment.Center;
        }

        int AddLink(string text, int x, int y, Action action)
        {
            var l = new Label { Text = text, AutoSize = true, Left = x, Top = y, ForeColor = Theme.AccentHover, Cursor = Cursors.Hand, Font = Theme.Font(9.5f, FontStyle.Regular) };
            l.MouseEnter += delegate { l.Font = Theme.Font(9.5f, FontStyle.Underline); };
            l.MouseLeave += delegate { l.Font = Theme.Font(9.5f, FontStyle.Regular); };
            l.Click += delegate { action(); };
            Controls.Add(l);
            return l.Right;
        }

        void Changed()
        {
            Settings.Save();
            settingsChanged();
            RefreshAll(false);
        }

        // ---------- state ----------

        public void RefreshAll(bool guarding)
        {
            loading = true;
            tAuto.Checked = Settings.AutoEnabled;
            tPlaying.Checked = Settings.OnlyWhenPlaying;
            tSleep.Checked = Settings.SleepTimerEnabled;
            nAuto.Value = Math.Max(nAuto.Minimum, Math.Min(nAuto.Maximum, Settings.AutoMinutes));
            nSleep.Value = Math.Max(nSleep.Minimum, Math.Min(nSleep.Maximum, Settings.SleepTimerMinutes));
            loading = false;

            nAuto.Enabled = tPlaying.Enabled = Settings.AutoEnabled;
            lAfter.ForeColor = lAfterTail.ForeColor = lPlaying.ForeColor = Settings.AutoEnabled ? Theme.Text : Theme.Dim;
            nSleep.Enabled = Settings.SleepTimerEnabled;
            lSleepA.ForeColor = lSleepB.ForeColor = Settings.SleepTimerEnabled ? Theme.Text : Theme.Dim;

            statusDot.ForeColor = guarding ? Theme.On : Theme.Off;
            statusDot.Invalidate();
            statusText.Text = guarding ? "Protection is on" : "Protection is off";
            statusDetail.Text = !Settings.AutoEnabled
                ? "Turning on by itself is switched off. Use the button below or the shortcut."
                : "Turns on by itself after " + Settings.AutoMinutes + (Settings.AutoMinutes == 1 ? " minute" : " minutes") +
                  " without keyboard or mouse" + (Settings.OnlyWhenPlaying ? ", while sound is playing." : ".");
            hint.Text = "Shortcut from anywhere, even over a full-screen video:  Ctrl + Alt + " + Settings.HotkeyLetter;
            summary.Text = Summary();
        }

        // "Protected 2 times · 3 wrong PIN attempts, last at 01:12" for the last 24 hours.
        static string Summary()
        {
            DateTime since = DateTime.Now.AddHours(-24);
            int on = 0, wrong = 0;
            DateTime lastWrong = DateTime.MinValue;
            foreach (string line in Activity.Recent(1000))
            {
                DateTime t;
                if (line.Length < 19 || !DateTime.TryParseExact(line.Substring(0, 19), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out t)) continue;
                if (t < since) break;                     // newest first
                if (line.Contains("Protection ON")) on++;
                if (line.Contains("Wrong PIN attempt")) { wrong++; if (t > lastWrong) lastWrong = t; }
            }
            if (on == 0 && wrong == 0) return "Nothing yet.";
            string s = "Protected " + on + (on == 1 ? " time" : " times");
            if (wrong > 0) s += "  ·  " + wrong + (wrong == 1 ? " wrong PIN attempt" : " wrong PIN attempts") + ", last at " + lastWrong.ToString("HH:mm");
            else s += "  ·  no wrong PIN attempts";
            return s;
        }

        // ---------- showing ----------

        public void ShowAndFocus()
        {
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            BringToFront();
            Activate();
            if (!SetForegroundWindow(Handle)) { TopMost = true; TopMost = false; }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int dark = 1;   // dark title bar on Windows 10 (1809+) and 11
            if (DwmSetWindowAttribute(Handle, 20, ref dark, 4) != 0) DwmSetWindowAttribute(Handle, 19, ref dark, 4);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
            base.OnKeyDown(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!AllowExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                if (!toldAboutTray) { toldAboutTray = true; ClosedToTray(this, EventArgs.Empty); }
                return;
            }
            base.OnFormClosing(e);
        }

        public event EventHandler ClosedToTray = delegate { };
    }
}
