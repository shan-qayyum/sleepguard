// The small box that appears while someone types at a guarded laptop. It never takes focus, so the video
// underneath keeps playing in full screen, and it hides itself again after a few seconds.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace SleepGuard
{
    public class PinPrompt : Form
    {
        readonly Label title = new Label(), dots = new Label();
        readonly Timer hideTimer = new Timer { Interval = 6000 };

        public PinPrompt()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(28, 30, 34);
            Size = new Size(520, 100);
            Opacity = 0.92;

            title.SetBounds(10, 14, 500, 30);
            title.TextAlign = ContentAlignment.MiddleCenter;
            title.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(232, 234, 237);
            dots.SetBounds(10, 50, 500, 36);
            dots.TextAlign = ContentAlignment.MiddleCenter;
            dots.Font = new Font("Segoe UI", 18f, FontStyle.Bold);
            dots.ForeColor = Color.FromArgb(90, 160, 240);
            Controls.Add(title);
            Controls.Add(dots);

            hideTimer.Tick += delegate { hideTimer.Stop(); Hide(); };
        }

        // Never steal focus from the video.
        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 /* WS_EX_NOACTIVATE */ | 0x00000080 /* WS_EX_TOOLWINDOW */ | 0x00000008 /* WS_EX_TOPMOST */;
                return cp;
            }
        }

        public void ShowState(string message, Color color, int typed)
        {
            title.Text = message;
            title.ForeColor = color;
            // Shrink the font until the whole message fits on one line.
            float size = 12f;
            while (size > 8f && TextRenderer.MeasureText(message, title.Font).Width > title.Width - 4)
            {
                size -= 0.5f;
                Font old = title.Font;
                title.Font = new Font("Segoe UI", size, FontStyle.Bold);
                old.Dispose();
            }
            dots.Text = typed > 0 ? new string('●', Math.Min(typed, 16)) : "";
            Rectangle area = Screen.PrimaryScreen.Bounds;
            Location = new Point(area.Left + (area.Width - Width) / 2, area.Bottom - Height - 80);
            if (!Visible) Show();
            hideTimer.Stop();
            hideTimer.Start();
        }

        public void HideNow() { hideTimer.Stop(); Hide(); }
    }
}
