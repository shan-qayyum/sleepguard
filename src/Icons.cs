// The app icon (crescent moon with a gold padlock), drawn as vector art so the tray icon is sharp at any DPI.
// The same drawing produced assets\app.ico, which is embedded in the .exe.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SleepGuard
{
    public static class Icons
    {
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);

        // The icon shown on windows (from the .exe).
        public static Icon App
        {
            get { try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { return SystemIcons.Application; } }
        }

        // Tray icon: full colour while protecting, grey while off.
        public static Icon Tray(bool on)
        {
            int s = SystemInformation.SmallIconSize.Width;
            using (Bitmap art = Draw(s))
            using (Bitmap shown = on ? (Bitmap)art.Clone() : Greyscale(art))
            {
                IntPtr h = shown.GetHicon();
                using (Icon temp = Icon.FromHandle(h))
                {
                    Icon copy = (Icon)temp.Clone();      // own copy, so the GDI handle can be freed now
                    DestroyIcon(h);
                    return copy;
                }
            }
        }

        static Bitmap Greyscale(Bitmap src)
        {
            var dst = new Bitmap(src.Width, src.Height);
            var matrix = new ColorMatrix(new[]
            {
                new[] { 0.30f, 0.30f, 0.30f, 0, 0 },
                new[] { 0.59f, 0.59f, 0.59f, 0, 0 },
                new[] { 0.11f, 0.11f, 0.11f, 0, 0 },
                new[] { 0f, 0, 0, 0.85f, 0 },
                new[] { 0f, 0, 0, 0, 1 },
            });
            using (var g = Graphics.FromImage(dst))
            using (var attr = new ImageAttributes())
            {
                attr.SetColorMatrix(matrix);
                g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attr);
            }
            return dst;
        }

        static GraphicsPath Rounded(float x, float y, float w, float h, float r)
        {
            var p = new GraphicsPath();
            float d = r * 2;
            p.AddArc(x, y, d, d, 180, 90);
            p.AddArc(x + w - d, y, d, d, 270, 90);
            p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            p.AddArc(x, y + h - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static Bitmap Draw(int s)
        {
            var b = new Bitmap(s, s);
            using (var g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                float m = s * 0.04f, w = s - 2 * m;
                using (var tile = Rounded(m, m, w, w, w * 0.22f))
                using (var bg = new LinearGradientBrush(new PointF(0, m), new PointF(s, m + w), Color.FromArgb(76, 64, 190), Color.FromArgb(24, 22, 66)))
                using (var edge = new Pen(Color.FromArgb(80, 255, 255, 255), Math.Max(1f, s * 0.012f)))
                {
                    g.FillPath(bg, tile);
                    g.DrawPath(edge, tile);
                }
                using (var outer = new GraphicsPath())
                using (var cut = new GraphicsPath())
                using (var moon = new Region(new RectangleF(0, 0, s, s)))
                using (var white = new SolidBrush(Color.FromArgb(245, 243, 255)))
                {
                    outer.AddEllipse(s * 0.14f, s * 0.14f, s * 0.60f, s * 0.60f);
                    cut.AddEllipse(s * 0.34f, s * 0.06f, s * 0.54f, s * 0.54f);
                    moon.Intersect(outer);
                    moon.Exclude(cut);
                    g.FillRegion(white, moon);
                }
                float lx = s * 0.55f, ly = s * 0.58f, lw = s * 0.32f, lh = s * 0.26f, sw = lw * 0.62f;
                using (var shackle = new Pen(Color.FromArgb(245, 184, 46), Math.Max(1.4f, s * 0.055f)))
                {
                    g.DrawArc(shackle, lx + (lw - sw) / 2, ly - lh * 0.62f, sw, lh * 1.1f, 180, 180);
                    g.DrawLine(shackle, lx + (lw - sw) / 2, ly - lh * 0.07f, lx + (lw - sw) / 2, ly + lh * 0.1f);
                    g.DrawLine(shackle, lx + (lw + sw) / 2, ly - lh * 0.07f, lx + (lw + sw) / 2, ly + lh * 0.1f);
                }
                using (var body = Rounded(lx, ly, lw, lh, lw * 0.16f))
                using (var gold = new LinearGradientBrush(new PointF(0, ly), new PointF(0, ly + lh), Color.FromArgb(255, 205, 80), Color.FromArgb(222, 150, 20)))
                    g.FillPath(gold, body);
            }
            return b;
        }
    }
}
