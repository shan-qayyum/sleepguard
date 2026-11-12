// Small custom controls for the main window: rounded cards, pill buttons and toggle switches, in the app's
// night-blue theme.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SleepGuard
{
    static class Theme
    {
        public static readonly Color Back = Color.FromArgb(18, 19, 42);
        public static readonly Color Card = Color.FromArgb(30, 32, 66);
        public static readonly Color CardEdge = Color.FromArgb(46, 49, 92);
        public static readonly Color Text = Color.FromArgb(238, 237, 255);
        public static readonly Color Dim = Color.FromArgb(160, 163, 200);
        public static readonly Color Accent = Color.FromArgb(123, 108, 246);
        public static readonly Color AccentHover = Color.FromArgb(142, 129, 255);
        public static readonly Color On = Color.FromArgb(61, 214, 140);
        public static readonly Color Off = Color.FromArgb(120, 124, 160);
        public static readonly Color Warn = Color.FromArgb(245, 184, 46);
        public static readonly Color TrackOff = Color.FromArgb(58, 61, 102);

        public static Font Font(float size, FontStyle style) { return new Font("Segoe UI", size, style); }

        public static GraphicsPath Rounded(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // A rounded card that child controls sit on.
    class Card : Panel
    {
        public Card()
        {
            DoubleBuffered = true;
            BackColor = Theme.Card;
            ResizeRedraw = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var bg = new SolidBrush(Parent != null ? Parent.BackColor : Theme.Back)) e.Graphics.FillRectangle(bg, ClientRectangle);
            using (var path = Theme.Rounded(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 14))
            using (var fill = new SolidBrush(Theme.Card))
            using (var edge = new Pen(Theme.CardEdge))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(edge, path);
            }
        }
    }

    // A large rounded button.
    class PillButton : Control
    {
        bool hover, down;
        public Color Fill = Theme.Accent, HoverFill = Theme.AccentHover;

        public PillButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
            Cursor = Cursors.Hand;
            ForeColor = Color.White;
            Font = Theme.Font(12f, FontStyle.Bold);
            BackColor = Color.Transparent;
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) OnClick(EventArgs.Empty);
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, down ? 1.5f : 0.5f, Width - 1.5f, Height - 2f);
            using (var path = Theme.Rounded(r, r.Height / 2))
            using (var fill = new SolidBrush(hover ? HoverFill : Fill))
                g.FillPath(fill, path);
            if (Focused && ShowFocusCues)
                using (var path = Theme.Rounded(new RectangleF(3, 3, Width - 7, Height - 7), (Height - 7) / 2f))
                using (var pen = new Pen(Color.FromArgb(160, 255, 255, 255), 1.5f))
                    g.DrawPath(pen, path);
            TextRenderer.DrawText(g, Text, Font, Rectangle.Round(r), ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    // An on/off switch.
    class Toggle : Control
    {
        bool isOn;
        public event EventHandler CheckedChanged;

        public Toggle()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
            Size = new Size(46, 26);
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;
            TabStop = true;
        }

        public bool Checked
        {
            get { return isOn; }
            set { if (isOn == value) return; isOn = value; Invalidate(); if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty); }
        }

        protected override void OnClick(EventArgs e) { Checked = !Checked; Focus(); base.OnClick(e); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) Checked = !Checked;
            base.OnKeyDown(e);
        }

        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var track = new RectangleF(1, 2, Width - 3, Height - 5);
            Color trackColor = isOn ? Theme.Accent : Theme.TrackOff;
            if (!Enabled) trackColor = Color.FromArgb(90, trackColor);
            using (var path = Theme.Rounded(track, track.Height / 2))
            using (var fill = new SolidBrush(trackColor))
                g.FillPath(fill, path);
            if (Focused && ShowFocusCues)
                using (var path = Theme.Rounded(track, track.Height / 2))
                using (var pen = new Pen(Color.FromArgb(170, 255, 255, 255), 1.2f))
                    g.DrawPath(pen, path);
            float k = track.Height - 6;
            float x = isOn ? track.Right - k - 3 : track.X + 3;
            using (var knob = new SolidBrush(Enabled ? Color.White : Color.FromArgb(150, 255, 255, 255)))
                g.FillEllipse(knob, x, track.Y + 3, k, k);
        }
    }
}
