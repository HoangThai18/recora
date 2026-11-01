using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Recora.App.Logging;
using Recora.Core.Capture;
using Recora.Core.Settings;

namespace Recora.App.UI
{
    /// <summary>
    /// The bar on the recorded screen while recording: a blinking dot, the clock, size and fps, pause and stop.
    /// Kept out of every capture, never takes focus from the game, draggable. A per-pixel-alpha layered window
    /// repainted twice a second, so it costs the game nothing measurable.
    /// </summary>
    internal sealed class RecordingBar : Form
    {
        private const int WsExLayered = 0x80000;
        private const int WsExToolWindow = 0x80;
        private const int WsExTopmost = 0x8;
        private const int WsExNoActivate = 0x08000000;
        private const int WmNcLButtonDown = 0xA1;
        private const int HtCaption = 2;

        private readonly Timer _blink = new Timer { Interval = 500 };
        private readonly Font _clockFont;
        private readonly Font _labelFont;
        private readonly Font _detailsFont;
        private readonly float _scale;
        private RectangleF _pauseBox;
        private RectangleF _stopBox;
        private int _hover;
        private bool _blinkOn = true;
        private bool _paused;
        private string _clock = "00:00:00";
        private string _details = string.Empty;

        private RecordingBar(float scale)
        {
            _scale = scale;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            Text = "Recora đang ghi";
            _clockFont = new Font("Segoe UI Semibold", 15f * scale, FontStyle.Regular, GraphicsUnit.Pixel);
            _labelFont = new Font("Segoe UI Semibold", 11f * scale, FontStyle.Regular, GraphicsUnit.Pixel);
            _detailsFont = new Font("Segoe UI", 12f * scale, FontStyle.Regular, GraphicsUnit.Pixel);
            Size = Measure();
            _blink.Tick += (_, _) =>
            {
                _blinkOn = !_blinkOn;
                Render();
            };
        }

        public event EventHandler? PauseClicked;

        public event EventHandler? StopClicked;

        /// <summary>
        /// Shows the bar in <paramref name="corner"/> of <paramref name="screen"/>, or returns null when Windows can't
        /// keep it out of the video.
        /// </summary>
        public static RecordingBar? Open(PixelRect screen, OverlayCorner corner)
        {
            var scale = DpiOf(screen);
            var bar = new RecordingBar(scale);
            var place = OverlayPlacement.Place(screen, bar.Width, bar.Height, corner, (int)(16 * scale));
            bar.Location = new Point(place.X, place.Y);
            try
            {
                bar.CreateHandle();

                // A per-pixel layered window has no surface until its first UpdateLayeredWindow, and Windows 10 refused
                // the affinity on it then (error 8). It is still hidden here, so nothing can reach the video.
                bar.Render();
                if (!WindowExclusion.Apply(bar))
                {
                    Log.Warn("recording bar: this Windows can't hide it from capture, not shown");
                    bar.Dispose();
                    return null;
                }

                bar.Show();
                bar._blink.Start();
                return bar;
            }
            catch
            {
                // Never leave a half-made top-most window behind.
                bar.Dispose();
                throw;
            }
        }

        /// <summary>Recording or paused, with the clock and "1,2 GB · 60 fps".</summary>
        public void Update(bool paused, string clock, string details)
        {
            if (paused == _paused && clock == _clock && details == _details)
            {
                return;
            }

            _paused = paused;
            _clock = clock;
            _details = details;
            Render();
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.ExStyle |= WsExLayered | WsExToolWindow | WsExTopmost | WsExNoActivate;
                return parameters;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var hover = _pauseBox.Contains(e.Location) ? 1 : _stopBox.Contains(e.Location) ? 2 : 0;
            Cursor = hover == 0 ? Cursors.SizeAll : Cursors.Hand;
            if (hover != _hover)
            {
                _hover = hover;
                Render();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hover != 0)
            {
                _hover = 0;
                Render();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            if (_pauseBox.Contains(e.Location))
            {
                PauseClicked?.Invoke(this, EventArgs.Empty);
            }
            else if (_stopBox.Contains(e.Location))
            {
                StopClicked?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                // Anywhere else drags the bar, as if it were a title bar.
                ReleaseCapture();
                SendMessage(Handle, WmNcLButtonDown, (IntPtr)HtCaption, IntPtr.Zero);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _blink.Dispose();
                _clockFont.Dispose();
                _labelFont.Dispose();
                _detailsFont.Dispose();
            }

            base.Dispose(disposing);
        }

        private float S(float value) => value * _scale;

        /// <summary>Wide enough for the longest clock and details, so the bar never jumps as the numbers grow.</summary>
        private Size Measure()
        {
            using (var bitmap = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(bitmap))
            {
                var label = g.MeasureString("TẠM DỪNG", _labelFont).Width;
                var clock = g.MeasureString("00:00:00", _clockFont).Width;
                var details = g.MeasureString("999,9 MB · 144 fps", _detailsFont).Width;
                var width = S(14) + S(10) + S(8) + label + S(8) + clock + S(12) + S(1) + S(12) + details + S(10) + S(28) * 2 + S(4) + S(8);
                return new Size((int)Math.Ceiling(width), (int)Math.Ceiling(S(42)));
            }
        }

        /// <summary>The bar as drawn, for the screenshot tool (a capture of the screen can't see it).</summary>
        public Bitmap Snapshot()
        {
            var bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                PaintBar(g);
            }

            return bitmap;
        }

        private void Render()
        {
            if (!IsHandleCreated || IsDisposed)
            {
                return;
            }

            using (var bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bitmap))
                {
                    PaintBar(g);
                }

                Push(bitmap);
            }
        }

        private void PaintBar(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            var box = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            var radius = box.Height / 2;
            Theme.FillRounded(g, Color.FromArgb(228, 14, 16, 21), box, radius);
            Theme.StrokeRounded(g, Color.FromArgb(46, 255, 255, 255), 1f, box, radius);

            var middle = Height / 2f;
            var x = S(14);

            // Dot: red and blinking while recording, steady amber while paused.
            var dotColor = _paused ? Palette.Dark.Pause : Palette.Dark.Record;
            var dot = S(10);
            if (_paused || _blinkOn)
            {
                using (var halo = new SolidBrush(Color.FromArgb(70, dotColor)))
                using (var fill = new SolidBrush(dotColor))
                {
                    g.FillEllipse(halo, x - S(3), middle - dot / 2 - S(3), dot + S(6), dot + S(6));
                    g.FillEllipse(fill, x, middle - dot / 2, dot, dot);
                }
            }
            else
            {
                using (var dim = new SolidBrush(Color.FromArgb(90, dotColor)))
                {
                    g.FillEllipse(dim, x, middle - dot / 2, dot, dot);
                }
            }

            x += dot + S(8);
            var label = _paused ? "TẠM DỪNG" : "REC";
            x = Draw(g, label, _labelFont, Color.FromArgb(200, dotColor), x, middle) + S(8);
            x = Draw(g, _clock, _clockFont, Color.White, x, middle) + S(12);

            using (var line = new Pen(Color.FromArgb(50, 255, 255, 255), 1f))
            {
                g.DrawLine(line, x, middle - S(10), x, middle + S(10));
            }

            x += S(13);
            Draw(g, _details, _detailsFont, Color.FromArgb(185, 255, 255, 255), x, middle);

            var button = S(28);
            _stopBox = new RectangleF(Width - S(8) - button, middle - button / 2, button, button);
            _pauseBox = new RectangleF(_stopBox.X - S(4) - button, _stopBox.Y, button, button);
            PaintButton(g, _pauseBox, _hover == 1, _paused ? IconKind.Record : IconKind.Pause, _paused ? Palette.Dark.Record : Color.White);
            PaintButton(g, _stopBox, _hover == 2, IconKind.Stop, Palette.Dark.Record);
        }

        private void PaintButton(Graphics g, RectangleF box, bool hover, IconKind icon, Color color)
        {
            using (var fill = new SolidBrush(Color.FromArgb(hover ? 60 : 26, 255, 255, 255)))
            {
                g.FillEllipse(fill, box);
            }

            Icons.Draw(g, icon, RectangleF.Inflate(box, -box.Width * 0.28f, -box.Height * 0.28f), color);
        }

        private float Draw(Graphics g, string text, Font font, Color color, float x, float middle)
        {
            var size = g.MeasureString(text, font, PointF.Empty, StringFormat.GenericTypographic);
            using (var brush = new SolidBrush(color))
            {
                g.DrawString(text, font, brush, x, middle - size.Height / 2, StringFormat.GenericTypographic);
            }

            return x + size.Width;
        }

        /// <summary>Hands the bitmap (with its alpha) to Windows as the window's whole look.</summary>
        private void Push(Bitmap bitmap)
        {
            var screen = GetDC(IntPtr.Zero);
            var memory = CreateCompatibleDC(screen);
            var hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
            var old = SelectObject(memory, hBitmap);
            try
            {
                var size = new NativeSize { Width = bitmap.Width, Height = bitmap.Height };
                var source = new NativePoint();
                var blend = new BlendFunction { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
                if (!UpdateLayeredWindow(Handle, screen, IntPtr.Zero, ref size, memory, ref source, 0, ref blend, 2))
                {
                    Log.Warn($"recording bar: UpdateLayeredWindow failed ({Marshal.GetLastWin32Error()})");
                }
            }
            finally
            {
                SelectObject(memory, old);
                DeleteObject(hBitmap);
                DeleteDC(memory);
                ReleaseDC(IntPtr.Zero, screen);
            }
        }

        private static float DpiOf(PixelRect screen)
        {
            try
            {
                var monitor = MonitorFromPoint(new NativePoint { X = screen.X + screen.Width / 2, Y = screen.Y + screen.Height / 2 }, 2);
                if (GetDpiForMonitor(monitor, 0, out var dpiX, out _) == 0 && dpiX > 0)
                {
                    return dpiX / 96f;
                }
            }
            catch (EntryPointNotFoundException)
            {
                // Windows 7/8: no per-monitor DPI.
            }
            catch (DllNotFoundException)
            {
            }

            return 1f;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeSize
        {
            public int Width;
            public int Height;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BlendFunction
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr window, IntPtr screenDc, IntPtr destination, ref NativeSize size, IntPtr sourceDc, ref NativePoint source, int key, ref BlendFunction blend, int flags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr window);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr window, IntPtr dc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr dc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr dc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr dc, IntPtr gdiObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr gdiObject);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint dpiX, out uint dpiY);
    }
}
