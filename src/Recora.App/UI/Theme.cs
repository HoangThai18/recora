using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Recora.App.UI
{
    /// <summary>
    /// thaipro.store's light pastel-blue palette (navy text, #1A62A8 accent, blue status bar at the bottom).
    /// Fonts are created once: GDI font handles are a limited resource.
    /// </summary>
    internal static class Theme
    {
        public static readonly Color Background = FromHex(0xEAF4FB);
        public static readonly Color Surface = Color.White;
        public static readonly Color Sidebar = FromHex(0xDCEBF7);
        public static readonly Color Border = FromHex(0xC9DDF0);
        public static readonly Color Text = FromHex(0x12304F);
        public static readonly Color Muted = FromHex(0x5A7392);
        public static readonly Color Accent = FromHex(0x1A62A8);
        public static readonly Color AccentSoft = FromHex(0xD4E7F8);
        public static readonly Color AccentHover = FromHex(0xE6F1FB);
        public static readonly Color Record = FromHex(0xE5484D);
        public static readonly Color RecordHover = FromHex(0xD13A3F);
        public static readonly Color Pause = FromHex(0xE09B12);
        public static readonly Color Ok = FromHex(0x1F9D6B);
        public static readonly Color StatusText = Color.White;

        /// <summary>
        /// Extra scale on top of the monitor DPI. Only <c>--ui-screenshot --ui-scale</c> sets it, to preview a 150% layout
        /// on a 100% CI desktop.
        /// </summary>
        public static float UiScale { get; private set; } = 1f;

        public static Font Body { get; private set; } = null!;

        public static Font BodyBold { get; private set; } = null!;

        public static Font Small { get; private set; } = null!;

        public static Font Title { get; private set; } = null!;

        public static Font Brand { get; private set; } = null!;

        public static Font Mono { get; private set; } = null!;

        public static Font MonoSmall { get; private set; } = null!;

        /// <summary>The recording clock on the home page.</summary>
        public static Font Clock { get; private set; } = null!;

        static Theme() => Configure(1f);

        public static void Configure(float uiScale)
        {
            UiScale = uiScale;
            Body = new Font("Segoe UI", 9.75f * uiScale, FontStyle.Regular, GraphicsUnit.Point);
            BodyBold = new Font("Segoe UI Semibold", 9.75f * uiScale, FontStyle.Regular, GraphicsUnit.Point);
            Small = new Font("Segoe UI", 8.5f * uiScale, FontStyle.Regular, GraphicsUnit.Point);
            Title = new Font("Segoe UI Semibold", 13f * uiScale, FontStyle.Regular, GraphicsUnit.Point);
            Brand = new Font("Segoe UI Semibold", 14f * uiScale, FontStyle.Regular, GraphicsUnit.Point);
            Mono = new Font("Consolas", 9.75f * uiScale, FontStyle.Regular, GraphicsUnit.Point);
            MonoSmall = new Font("Consolas", 8.5f * uiScale, FontStyle.Regular, GraphicsUnit.Point);
            Clock = new Font("Consolas", 24f * uiScale, FontStyle.Bold, GraphicsUnit.Point);
        }

        /// <summary>The colour <paramref name="amount"/> of the way from <paramref name="from"/> to <paramref name="to"/>.</summary>
        public static Color Blend(Color from, Color to, double amount)
        {
            var t = amount < 0 ? 0 : amount > 1 ? 1 : amount;
            int Mix(int a, int b) => (int)Math.Round(a + (b - a) * t);
            return Color.FromArgb(Mix(from.A, to.A), Mix(from.R, to.R), Mix(from.G, to.G), Mix(from.B, to.B));
        }

        public static Color Fade(Color color, double opacity) =>
            Color.FromArgb((int)Math.Round(color.A * (opacity < 0 ? 0 : opacity > 1 ? 1 : opacity)), color);

        public static Color FromHex(int rgb) => Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);

        public static GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            var d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));

            if (d <= 0.5f)
            {
                path.AddRectangle(rect);
                return path;
            }

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRounded(Graphics g, Color color, RectangleF rect, float radius)
        {
            using (var path = RoundedRect(rect, radius))
            using (var brush = new SolidBrush(color))
            {
                g.FillPath(brush, path);
            }
        }

        public static void StrokeRounded(Graphics g, Color color, float width, RectangleF rect, float radius)
        {
            using (var path = RoundedRect(rect, radius))
            using (var pen = new Pen(color, width))
            {
                g.DrawPath(pen, path);
            }
        }

        public static void Smooth(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        }
    }
}
