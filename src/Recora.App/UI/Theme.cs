using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Recora.App.UI
{
    /// <summary>The colours of one look. Every slot used as a control's BackColor or ForeColor is distinct, so <see cref="Theme.Use"/> can remap them.</summary>
    internal sealed class Palette
    {
        public Palette(string name, bool dark)
        {
            Name = name;
            IsDark = dark;
        }

        public string Name { get; }

        public bool IsDark { get; }

        public Color Background { get; set; }

        public Color Surface { get; set; }

        public Color Border { get; set; }

        public Color Text { get; set; }

        public Color Muted { get; set; }

        /// <summary>Accent for text, strokes and the selected state.</summary>
        public Color Accent { get; set; }

        /// <summary>Accent behind white text (primary buttons, the chosen screen).</summary>
        public Color AccentStrong { get; set; }

        public Color AccentPressed { get; set; }

        public Color AccentSoft { get; set; }

        public Color AccentHover { get; set; }

        public Color Record { get; set; }

        public Color RecordHover { get; set; }

        public Color Pause { get; set; }

        public Color Ok { get; set; }

        /// <summary>The dark stage behind the recording clock and the trim preview.</summary>
        public Color StageTop { get; set; }

        public Color StageBottom { get; set; }

        public Color StageEdge { get; set; }

        /// <summary>Windows 11 style: neutral grey window, white cards, Fluent blue.</summary>
        public static readonly Palette Light = new Palette("light", false)
        {
            Background = Hex(0xF3F4F6),
            Surface = Color.White,
            Border = Hex(0xE2E5EA),
            Text = Hex(0x1A1D23),
            Muted = Hex(0x5F6875),
            Accent = Hex(0x0F6CBD),
            AccentStrong = Hex(0x0E64B0),
            AccentPressed = Hex(0x0B528F),
            AccentSoft = Hex(0xE3EEFA),
            AccentHover = Hex(0xEAECF0),
            Record = Hex(0xE5484D),
            RecordHover = Hex(0xD13A3F),
            Pause = Hex(0xD98E04),
            Ok = Hex(0x16A34A),
            StageTop = Hex(0x111827),
            StageBottom = Hex(0x1E293B),
            StageEdge = Hex(0x334155),
        };

        /// <summary>Gaming style: graphite window, dark cards, indigo accent.</summary>
        public static readonly Palette Dark = new Palette("dark", true)
        {
            Background = Hex(0x0F1115),
            Surface = Hex(0x181B21),
            Border = Hex(0x272B33),
            Text = Hex(0xE8EAEE),
            Muted = Hex(0x8D96A4),
            Accent = Hex(0x8B8DFA),
            AccentStrong = Hex(0x5B5EE8),
            AccentPressed = Hex(0x4A4DD0),
            AccentSoft = Hex(0x24264A),
            AccentHover = Hex(0x22262E),
            Record = Hex(0xF0565B),
            RecordHover = Hex(0xDB4549),
            Pause = Hex(0xF0A020),
            Ok = Hex(0x22C55E),
            StageTop = Hex(0x07090C),
            StageBottom = Hex(0x12151B),
            StageEdge = Hex(0x2A2F38),
        };

        // Not Theme.FromHex: Theme's static fields read Palette.Light, so touching Theme from here would hand it a null.
        private static Color Hex(int rgb) => Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);

        /// <summary>Slots in the order <see cref="Theme.Use"/> matches a control's colours against.</summary>
        internal Color[] Slots => new[] { Background, Surface, Border, Text, Muted, Accent, AccentSoft, AccentHover, AccentStrong, Record, Pause, Ok };
    }

    /// <summary>
    /// The current look: <see cref="Palette.Light"/> or <see cref="Palette.Dark"/>, switchable while the app runs.
    /// Painted controls read these at paint time; plain controls are remapped by <see cref="Use"/>.
    /// Fonts are created once: GDI font handles are a limited resource.
    /// </summary>
    internal static class Theme
    {
        private static Palette? _current;

        public static Palette Current => _current ?? Palette.Light;

        public static Color Background => Current.Background;

        public static Color Surface => Current.Surface;

        /// <summary>The sidebar sits on the window background, as in Windows 11 Settings.</summary>
        public static Color Sidebar => Current.Background;

        public static Color Border => Current.Border;

        public static Color Text => Current.Text;

        public static Color Muted => Current.Muted;

        public static Color Accent => Current.Accent;

        public static Color AccentStrong => Current.AccentStrong;

        public static Color AccentPressed => Current.AccentPressed;

        public static Color AccentSoft => Current.AccentSoft;

        public static Color AccentHover => Current.AccentHover;

        public static Color Record => Current.Record;

        public static Color RecordHover => Current.RecordHover;

        public static Color Pause => Current.Pause;

        public static Color Ok => Current.Ok;

        public static Color StageTop => Current.StageTop;

        public static Color StageBottom => Current.StageBottom;

        public static Color StageEdge => Current.StageEdge;

        /// <summary>
        /// Switches to <paramref name="palette"/> and recolours <paramref name="roots"/>: every BackColor and ForeColor
        /// that held a slot of the old palette gets the same slot of the new one, then everything repaints.
        /// </summary>
        public static void Use(Palette palette, params Control[] roots)
        {
            var old = Current;
            _current = palette;
            if (ReferenceEquals(old, palette))
            {
                return;
            }

            var from = old.Slots;
            var to = palette.Slots;
            Color Map(Color color)
            {
                for (var i = 0; i < from.Length; i++)
                {
                    if (from[i].ToArgb() == color.ToArgb())
                    {
                        return to[i];
                    }
                }

                return color;
            }

            foreach (var root in roots)
            {
                Recolor(root, Map);
                if (root is Form form && form.IsHandleCreated)
                {
                    StyleWindow(form);
                }

                root.Invalidate(true);
            }
        }

        /// <summary>Title bar, frame and scroll bars that match the palette (Windows 10 2004+ dark title, Windows 11 caption colour).</summary>
        public static void StyleWindow(Form form)
        {
            var handle = form.Handle;
            var dark = Current.IsDark ? 1 : 0;
            if (Native.NativeMethods.DwmSetWindowAttribute(handle, Native.NativeMethods.DwmUseImmersiveDarkMode, ref dark, sizeof(int)) != 0)
            {
                Native.NativeMethods.DwmSetWindowAttribute(handle, Native.NativeMethods.DwmUseImmersiveDarkModeOld, ref dark, sizeof(int));
            }

            var caption = ColorRef(Current.Background);
            var border = ColorRef(Current.Border);
            var text = ColorRef(Current.Text);
            Native.NativeMethods.DwmSetWindowAttribute(handle, Native.NativeMethods.DwmCaptionColor, ref caption, sizeof(int));
            Native.NativeMethods.DwmSetWindowAttribute(handle, Native.NativeMethods.DwmBorderColor, ref border, sizeof(int));
            Native.NativeMethods.DwmSetWindowAttribute(handle, Native.NativeMethods.DwmTextColor, ref text, sizeof(int));
            StyleScrollBars(form);
        }

        /// <summary>Dark or light scroll bars and lists on the native controls under <paramref name="root"/>.</summary>
        public static void StyleScrollBars(Control root)
        {
            if (root is ListView || root is ComboBox || (root is ScrollableControl scrollable && scrollable.AutoScroll))
            {
                if (root.IsHandleCreated)
                {
                    StyleNative(root);
                }
                else
                {
                    root.HandleCreated += (_, _) => StyleNative(root);
                }
            }

            foreach (Control child in root.Controls)
            {
                StyleScrollBars(child);
            }
        }

        private static void StyleNative(Control control)
        {
            var name = control is ComboBox
                ? (Current.IsDark ? "DarkMode_CFD" : "CFD")
                : (Current.IsDark ? "DarkMode_Explorer" : "Explorer");
            Native.NativeMethods.SetWindowTheme(control.Handle, name, null);
            if (control is ListView)
            {
                // The column header is a child window with its own theme.
                var header = Native.NativeMethods.SendMessage(control.Handle, 0x101F, IntPtr.Zero, IntPtr.Zero);
                if (header != IntPtr.Zero)
                {
                    Native.NativeMethods.SetWindowTheme(header, Current.IsDark ? "DarkMode_ItemsView" : "ItemsView", null);
                }
            }
        }

        /// <summary>The palette for <paramref name="mode"/>; System reads Windows' "app mode" (light unless set to dark).</summary>
        public static Palette For(Recora.Core.Settings.ThemeMode mode)
        {
            switch (mode)
            {
                case Recora.Core.Settings.ThemeMode.Light:
                    return Palette.Light;
                case Recora.Core.Settings.ThemeMode.Dark:
                    return Palette.Dark;
                default:
                    return WindowsUsesDarkApps() ? Palette.Dark : Palette.Light;
            }
        }

        private static bool WindowsUsesDarkApps()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
                }
            }
            catch (Exception error) when (error is System.Security.SecurityException || error is UnauthorizedAccessException || error is System.IO.IOException)
            {
                return false;
            }
        }

        private static void Recolor(Control control, Func<Color, Color> map)
        {
            control.BackColor = map(control.BackColor);
            control.ForeColor = map(control.ForeColor);
            foreach (Control child in control.Controls)
            {
                Recolor(child, map);
            }
        }

        private static int ColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);

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

            // Windows 11's own UI font when it is installed; Segoe UI everywhere else.
            var text = Installed("Segoe UI Variable Text") ? "Segoe UI Variable Text" : "Segoe UI";
            var textBold = Installed("Segoe UI Variable Text Semibold") ? "Segoe UI Variable Text Semibold" : "Segoe UI Semibold";
            var display = Installed("Segoe UI Variable Display Semibold") ? "Segoe UI Variable Display Semibold" : "Segoe UI Semibold";
            Body = new Font(text, 9.75f * uiScale, FontStyle.Regular, GraphicsUnit.Point);
            BodyBold = new Font(textBold, 9.75f * uiScale, FontStyle.Regular, GraphicsUnit.Point);
            Small = new Font(text, 8.5f * uiScale, FontStyle.Regular, GraphicsUnit.Point);
            Title = new Font(display, 15f * uiScale, FontStyle.Regular, GraphicsUnit.Point);
            Brand = new Font(display, 14f * uiScale, FontStyle.Regular, GraphicsUnit.Point);
            Mono = new Font("Consolas", 9.75f * uiScale, FontStyle.Regular, GraphicsUnit.Point);
            MonoSmall = new Font("Consolas", 8.5f * uiScale, FontStyle.Regular, GraphicsUnit.Point);
            Clock = new Font("Consolas", 24f * uiScale, FontStyle.Bold, GraphicsUnit.Point);
        }

        private static bool Installed(string family)
        {
            using (var font = new Font(family, 10f))
            {
                return string.Equals(font.Name, family, StringComparison.OrdinalIgnoreCase);
            }
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
