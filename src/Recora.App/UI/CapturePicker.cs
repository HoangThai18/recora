using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Recora.App.Native;

namespace Recora.App.UI
{
    internal enum PickKind
    {
        Region,
        Window,
    }

    /// <summary>What the user picked: a rectangle in desktop pixels, and the window when one was clicked.</summary>
    internal sealed record PickResult(Rectangle Bounds, IntPtr Window, string? WindowTitle);

    /// <summary>
    /// Full-screen overlays (one per monitor) for "Chọn vùng" and "Cửa sổ": the screen dims, the window under the
    /// pointer lights up, a drag draws a region. Click a window to take it whole; Enter confirms, Esc cancels.
    /// </summary>
    internal static class CapturePicker
    {
        public static PickResult? Pick(PickKind kind, IntPtr ownWindow)
        {
            var overlays = Screen.AllScreens.Select(screen => new PickerOverlay(screen, kind, ownWindow)).ToList();
            PickResult? result = null;
            var open = overlays.Count;

            foreach (var overlay in overlays)
            {
                overlay.Done += (_, picked) =>
                {
                    result ??= picked;
                    foreach (var other in overlays)
                    {
                        if (!other.IsDisposed)
                        {
                            other.Close();
                        }
                    }
                };
                overlay.FormClosed += (_, _) => open--;
                overlay.Show();
            }

            overlays.FirstOrDefault(o => o.Bounds.Contains(Cursor.Position))?.Activate();
            while (open > 0)
            {
                Application.DoEvents();
                System.Threading.Thread.Sleep(10);
            }

            foreach (var overlay in overlays)
            {
                overlay.Dispose();
            }

            return result;
        }
    }

    internal sealed class PickerOverlay : Form
    {
        private const int MinWidth = 160;
        private const int MinHeight = 90;
        private readonly Screen _screen;
        private readonly PickKind _kind;
        private readonly IntPtr _own;
        private readonly Bitmap _shot;
        private Point? _dragFrom;
        private Rectangle _selection;
        private Rectangle _hoverWindow;
        private IntPtr _hoverHandle;
        private string? _hoverTitle;

        public PickerOverlay(Screen screen, PickKind kind, IntPtr ownWindow)
        {
            _screen = screen;
            _kind = kind;
            _own = ownWindow;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            DoubleBuffered = true;
            Cursor = Cursors.Cross;
            AutoScaleMode = AutoScaleMode.None;
            Bounds = screen.Bounds;

            _shot = new Bitmap(screen.Bounds.Width, screen.Bounds.Height);
            using (var g = Graphics.FromImage(_shot))
            {
                g.CopyFromScreen(screen.Bounds.Location, Point.Empty, screen.Bounds.Size);
            }
        }

        public event EventHandler<PickResult?>? Done;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            // The picker itself must never be recorded or picked.
            WindowExclusion.Apply(this);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Bounds = _screen.Bounds;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _shot.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Done?.Invoke(this, null);
            }
            else if (e.KeyCode == Keys.Enter && !_selection.IsEmpty)
            {
                Confirm(_selection, IntPtr.Zero, null);
            }

            base.OnKeyDown(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                Done?.Invoke(this, null);
                return;
            }

            _dragFrom = e.Location;
            _selection = Rectangle.Empty;
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragFrom.HasValue && e.Button == MouseButtons.Left)
            {
                var from = _dragFrom.Value;
                _selection = Rectangle.FromLTRB(Math.Min(from.X, e.X), Math.Min(from.Y, e.Y), Math.Max(from.X, e.X), Math.Max(from.Y, e.Y));
                Invalidate();
            }
            else
            {
                UpdateHover(PointToScreen(e.Location));
            }

            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !_dragFrom.HasValue)
            {
                return;
            }

            _dragFrom = null;
            var dragged = _selection.Width >= 8 && _selection.Height >= 8;

            if (_kind == PickKind.Region && dragged)
            {
                if (_selection.Width < MinWidth || _selection.Height < MinHeight)
                {
                    // Too small to encode well (BR-07): grow around the centre instead of refusing.
                    var center = new Point(_selection.X + _selection.Width / 2, _selection.Y + _selection.Height / 2);
                    _selection = new Rectangle(center.X - MinWidth / 2, center.Y - MinHeight / 2, MinWidth, MinHeight);
                    _selection.Intersect(ClientRectangle);
                }

                Confirm(_selection, IntPtr.Zero, null);
                return;
            }

            // A click (no drag) takes the window under the pointer, in both modes.
            if (!_hoverWindow.IsEmpty)
            {
                var local = RectangleToClient(_hoverWindow);
                local.Intersect(ClientRectangle);
                Confirm(local, _hoverHandle, _hoverTitle);
            }

            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.DrawImageUnscaled(_shot, 0, 0);
            using (var dim = new SolidBrush(Color.FromArgb(120, 10, 24, 40)))
            {
                using (var region = new Region(ClientRectangle))
                {
                    var bright = Highlight();
                    if (!bright.IsEmpty)
                    {
                        region.Exclude(bright);
                    }

                    g.FillRegion(dim, region);
                }
            }

            var box = Highlight();
            if (!box.IsEmpty)
            {
                using (var pen = new Pen(Theme.Accent, 3f))
                {
                    g.DrawRectangle(pen, box);
                }

                var label = _dragFrom.HasValue || _kind == PickKind.Region && !_selection.IsEmpty
                    ? $"{box.Width} × {box.Height}"
                    : (_hoverTitle ?? "Cửa sổ") + $" · {box.Width} × {box.Height}";
                DrawTag(g, label, new Point(box.X, Math.Max(0, box.Y - 34)));
            }

            var help = _kind == PickKind.Region
                ? "Kéo chuột để chọn vùng  ·  bấm vào một cửa sổ để lấy cả cửa sổ  ·  Esc để huỷ"
                : "Bấm vào cửa sổ cần ghi  ·  Esc để huỷ";
            var size = TextRenderer.MeasureText(help, Theme.BodyBold);
            DrawTag(g, help, new Point((Width - size.Width - 28) / 2, 28));
        }

        private Rectangle Highlight()
        {
            if (!_selection.IsEmpty)
            {
                return _selection;
            }

            return _hoverWindow.IsEmpty ? Rectangle.Empty : Rectangle.Intersect(RectangleToClient(_hoverWindow), ClientRectangle);
        }

        private void DrawTag(Graphics g, string text, Point at)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var size = TextRenderer.MeasureText(text, Theme.BodyBold);
            var box = new RectangleF(at.X, at.Y, size.Width + 28, size.Height + 12);
            Theme.FillRounded(g, Theme.AccentStrong, box, 8);
            TextRenderer.DrawText(g, text, Theme.BodyBold, Rectangle.Round(box), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private void UpdateHover(Point screenPoint)
        {
            var handle = WindowPicking.TopLevelAt(screenPoint, _own, Handle);
            if (handle == _hoverHandle)
            {
                return;
            }

            _hoverHandle = handle;
            _hoverWindow = handle == IntPtr.Zero ? Rectangle.Empty : WindowPicking.VisibleBounds(handle);
            _hoverTitle = handle == IntPtr.Zero ? null : WindowPicking.Title(handle);
            Invalidate();
        }

        private void Confirm(Rectangle local, IntPtr window, string? title)
        {
            var desktop = new Rectangle(local.X + _screen.Bounds.X, local.Y + _screen.Bounds.Y, local.Width, local.Height);
            Done?.Invoke(this, new PickResult(desktop, window, title));
        }
    }

    /// <summary>Finding the real top-level window under a point, skipping Recora's own and the picker overlays.</summary>
    internal static class WindowPicking
    {
        private const uint GaRoot = 2;
        private const int DwmExtendedFrameBounds = 9;
        private const int DwmCloaked = 14;

        public static IntPtr TopLevelAt(Point screenPoint, params IntPtr[] skip)
        {
            // Walk the z-order from the top: WindowFromPoint would return the overlay we are drawing.
            for (var hwnd = GetTopWindow(IntPtr.Zero); hwnd != IntPtr.Zero; hwnd = GetWindow(hwnd, 2))
            {
                if (skip.Contains(hwnd) || !IsCandidate(hwnd))
                {
                    continue;
                }

                if (VisibleBounds(hwnd).Contains(screenPoint))
                {
                    return hwnd;
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>The windows a recording can follow, front first, without Recora's own.</summary>
        public static IReadOnlyList<Core.Capture.OpenWindow> List()
        {
            var own = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            var names = new Dictionary<uint, string>();
            var windows = new List<Core.Capture.OpenWindow>();
            for (var hwnd = GetTopWindow(IntPtr.Zero); hwnd != IntPtr.Zero; hwnd = GetWindow(hwnd, 2))
            {
                if (!IsCandidate(hwnd))
                {
                    continue;
                }

                GetWindowThreadProcessId(hwnd, out var pid);
                if (pid == own)
                {
                    continue;
                }

                if (!names.TryGetValue(pid, out var process))
                {
                    process = ProcessName(pid);
                    names[pid] = process;
                }

                // The desktop's Program Manager and the taskbar are no recording targets.
                if (string.Equals(process, "explorer", StringComparison.OrdinalIgnoreCase) && Title(hwnd) == "Program Manager")
                {
                    continue;
                }

                windows.Add(new Core.Capture.OpenWindow(hwnd.ToInt64(), Title(hwnd), process));
            }

            return windows;
        }

        /// <summary>The program of the window in front, or null when it is no recordable window.</summary>
        public static Core.Capture.OpenWindow? Foreground()
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero || !IsCandidate(hwnd))
            {
                return null;
            }

            GetWindowThreadProcessId(hwnd, out var pid);
            return new Core.Capture.OpenWindow(hwnd.ToInt64(), Title(hwnd), ProcessName(pid));
        }

        private static string ProcessName(uint pid)
        {
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById((int)pid);
                return process.ProcessName;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is System.ComponentModel.Win32Exception)
            {
                return string.Empty;
            }
        }

        public static Rectangle VisibleBounds(IntPtr hwnd)
        {
            if (DwmGetWindowAttribute(hwnd, DwmExtendedFrameBounds, out Rect rect, Marshal.SizeOf(typeof(Rect))) != 0)
            {
                GetWindowRect(hwnd, out rect);
            }

            return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }

        public static string Title(IntPtr hwnd)
        {
            var text = new StringBuilder(256);
            GetWindowText(hwnd, text, text.Capacity);
            return text.ToString();
        }

        private static bool IsCandidate(IntPtr hwnd)
        {
            if (!IsWindowVisible(hwnd) || IsIconic(hwnd) || GetAncestor(hwnd, GaRoot) != hwnd)
            {
                return false;
            }

            // Hidden UWP frames and other virtual-desktop windows report as visible but are cloaked.
            if (DwmGetWindowAttribute(hwnd, DwmCloaked, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
            {
                return false;
            }

            var bounds = VisibleBounds(hwnd);
            return bounds.Width >= 50 && bounds.Height >= 50 && Title(hwnd).Length > 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetTopWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint cmd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int max);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out Rect value, int size);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);
    }
}
