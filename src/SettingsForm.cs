// The settings window: when protection turns on, what happens when someone tries, the sleep timer, and the
// activity log. Every control sizes itself to its text, so nothing is clipped.

using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SleepGuard
{
    public class SettingsForm : Form
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "SleepGuard";

        readonly CheckBox cAuto = new CheckBox(), cOnlyPlaying = new CheckBox(), cPrompt = new CheckBox(),
                          cSleep = new CheckBox(), cScreenOff = new CheckBox(), cNotify = new CheckBox(), cStartup = new CheckBox();
        readonly NumericUpDown nAuto = new NumericUpDown(), nTries = new NumericUpDown(), nSleep = new NumericUpDown();
        readonly ComboBox cbHotkey = new ComboBox();
        readonly ListBox activity = new ListBox();
        int y = 12;

        public SettingsForm()
        {
            Text = "SleepGuard " + Program.Version + " - Settings";
            Icon = Icons.App;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5f);
            AutoScaleMode = AutoScaleMode.Font;
            const int width = 600;

            Section("Turning on");
            Row(Check(cAuto, "Turn on automatically after", 16), Number(nAuto, 1, 240, Settings.AutoMinutes),
                Text_("minutes without keyboard or mouse"));
            Row(Check(cOnlyPlaying, "...but only while sound is playing (so it does not turn on when nothing is on)", 36));
            Label hk = Text_("Hotkey to turn on right away:  Ctrl + Alt +");
            hk.Left = 16;
            cbHotkey.DropDownStyle = ComboBoxStyle.DropDownList;
            cbHotkey.Width = 54;
            for (char c = 'A'; c <= 'Z'; c++) cbHotkey.Items.Add(c.ToString());
            Row(hk, cbHotkey);

            Section("When someone tries to use the laptop");
            Label lt = Text_("Lock Windows after");
            lt.Left = 16;
            Row(lt, Number(nTries, 1, 10, Settings.MaxWrongTries), Text_("wrong PINs (it then needs your Windows password)"));
            Row(Check(cPrompt, "Show the PIN box when someone types, clicks or scrolls", 16));

            Section("Sleep timer");
            Row(Check(cSleep, "Pause the video after", 16), Number(nSleep, 1, 480, Settings.SleepTimerMinutes),
                Text_("minutes of protection"));
            Row(Check(cScreenOff, "...and turn the screen off", 36));

            Section("Other");
            Row(Check(cNotify, "Show a notification when protection turns on", 16));
            Row(Check(cStartup, "Start with Windows", 16));
            var bPin = new Button { Text = "Change PIN...", AutoSize = true, Left = 16 };
            bPin.Click += delegate { using (var s = new PinSetup(true)) s.ShowDialog(this); };
            Row(bPin);

            Section("Activity (newest first)");
            activity.SetBounds(16, y, width - 32, 150);
            activity.HorizontalScrollbar = true;
            Controls.Add(activity);
            y += 156;
            var bClear = new Button { Text = "Clear activity", AutoSize = true, Left = 16, Top = y };
            bClear.Click += delegate { Activity.Clear(); activity.Items.Clear(); };
            Controls.Add(bClear);

            var bSave = new Button { Text = "Save", AutoSize = true, MinimumSize = new Size(90, 0) };
            var bCancel = new Button { Text = "Cancel", AutoSize = true, MinimumSize = new Size(90, 0), DialogResult = DialogResult.Cancel };
            Controls.Add(bSave); Controls.Add(bCancel);
            bCancel.Location = new Point(width - 16 - bCancel.PreferredSize.Width, y);
            bSave.Location = new Point(bCancel.Left - 8 - bSave.PreferredSize.Width, y);
            bSave.Click += delegate { SaveAndClose(); };
            AcceptButton = bSave; CancelButton = bCancel;
            ClientSize = new Size(width, y + bSave.PreferredSize.Height + 14);

            // Current values
            cAuto.Checked = Settings.AutoEnabled;
            cOnlyPlaying.Checked = Settings.OnlyWhenPlaying;
            cbHotkey.SelectedItem = Settings.HotkeyLetter.ToString();
            cPrompt.Checked = Settings.ShowPrompt;
            cSleep.Checked = Settings.SleepTimerEnabled;
            cScreenOff.Checked = Settings.SleepTimerScreenOff;
            cNotify.Checked = Settings.NotifyOnTurnOn;
            cStartup.Checked = StartupEnabled();
            foreach (string line in Activity.Recent(200)) activity.Items.Add(line);
            if (activity.Items.Count == 0) activity.Items.Add("(nothing yet)");

            cAuto.CheckedChanged += delegate { UpdateEnabled(); };
            cSleep.CheckedChanged += delegate { UpdateEnabled(); };
            UpdateEnabled();
        }

        void UpdateEnabled()
        {
            nAuto.Enabled = cOnlyPlaying.Enabled = cAuto.Checked;
            nSleep.Enabled = cScreenOff.Enabled = cSleep.Checked;
        }

        // ----- layout helpers -----

        void Section(string text)
        {
            if (y > 12) y += 8;
            var l = new Label { Text = text, AutoSize = true, Left = 12, Top = y, Font = new Font(Font, FontStyle.Bold) };
            Controls.Add(l);
            y += l.PreferredHeight + 6;
        }

        CheckBox Check(CheckBox c, string text, int left)
        {
            c.Text = text; c.AutoSize = true; c.Left = left;
            return c;
        }

        NumericUpDown Number(NumericUpDown n, int min, int max, int value)
        {
            n.Minimum = min; n.Maximum = max; n.Value = Math.Max(min, Math.Min(max, value)); n.Width = 60;
            return n;
        }

        Label Text_(string text) { return new Label { Text = text, AutoSize = true }; }

        // Places controls left to right on one row, vertically centred, then moves down.
        void Row(params Control[] items)
        {
            int x = items[0].Left > 0 ? items[0].Left : 16;
            int h = 0;
            foreach (Control c in items) { Controls.Add(c); h = Math.Max(h, c.PreferredSize.Height); }
            foreach (Control c in items)
            {
                c.Left = x;
                c.Top = y + (h - c.PreferredSize.Height) / 2;
                x = c.Left + (c.AutoSize ? c.PreferredSize.Width : c.Width) + 6;
            }
            y += h + 6;
        }

        void SaveAndClose()
        {
            Settings.AutoEnabled = cAuto.Checked;
            Settings.AutoMinutes = (int)nAuto.Value;
            Settings.OnlyWhenPlaying = cOnlyPlaying.Checked;
            Settings.HotkeyLetter = ((string)cbHotkey.SelectedItem)[0];
            Settings.MaxWrongTries = (int)nTries.Value;
            Settings.ShowPrompt = cPrompt.Checked;
            Settings.SleepTimerEnabled = cSleep.Checked;
            Settings.SleepTimerMinutes = (int)nSleep.Value;
            Settings.SleepTimerScreenOff = cScreenOff.Checked;
            Settings.NotifyOnTurnOn = cNotify.Checked;
            Settings.Save();
            SetStartup(cStartup.Checked);
            DialogResult = DialogResult.OK;
        }

        public static bool StartupEnabled()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue(RunName) != null;
        }

        static void SetStartup(bool on)
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
                if (on) k.SetValue(RunName, "\"" + Application.ExecutablePath + "\"");
                else k.DeleteValue(RunName, false);
        }
    }
}
