// Dialog to choose (or change) the PIN.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace SleepGuard
{
    public class PinSetup : Form
    {
        readonly TextBox current = new TextBox(), first = new TextBox(), second = new TextBox();
        readonly Label error = new Label();

        public PinSetup(bool change)
        {
            Text = change ? "SleepGuard - change PIN" : "SleepGuard - choose a PIN";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            Font = new Font("Segoe UI", 9.5f);
            ClientSize = new Size(380, change ? 262 : 214);

            var intro = new Label
            {
                Text = "SleepGuard keeps your video playing but blocks the keyboard and mouse until this PIN is typed. " +
                       "Use digits or letters, at least 4."
            };
            intro.SetBounds(16, 12, 348, 60);
            Controls.Add(intro);

            int y = 76;
            if (change) { AddField("Current PIN", current, ref y); }
            AddField("New PIN", first, ref y);
            AddField("New PIN again", second, ref y);

            error.SetBounds(16, y, 348, 20);
            error.ForeColor = Color.Firebrick;
            Controls.Add(error);

            var ok = new Button { Text = "Save", DialogResult = DialogResult.None };
            ok.SetBounds(196, y + 26, 80, 30);
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            cancel.SetBounds(284, y + 26, 80, 30);
            ok.Click += delegate { Save(change); };
            Controls.Add(ok); Controls.Add(cancel);
            AcceptButton = ok; CancelButton = cancel;
        }

        void AddField(string label, TextBox box, ref int y)
        {
            Controls.Add(new Label { Text = label, Left = 16, Top = y + 4, AutoSize = true });
            box.SetBounds(140, y, 224, 26);
            box.UseSystemPasswordChar = true;
            box.MaxLength = 16;
            Controls.Add(box);
            y += 36;
        }

        void Save(bool change)
        {
            if (change && !Settings.CheckPin(current.Text)) { error.Text = "The current PIN is wrong."; return; }
            string pin = first.Text;
            if (pin.Length < 4) { error.Text = "Use at least 4 characters."; return; }
            foreach (char c in pin)
                if (!char.IsLetterOrDigit(c) || c > 127) { error.Text = "Use only digits and letters A-Z."; return; }
            if (pin != second.Text) { error.Text = "The two PINs do not match."; return; }
            Settings.SetPin(pin);
            if (change) Activity.Add("PIN changed");
            DialogResult = DialogResult.OK;
        }
    }
}
