using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Recora.App.Logging;

namespace Recora.App.UI.Controls
{
    /// <summary>
    /// Holds another program's top-level window as a child: caption and borders removed, sized to this panel, shown
    /// and hidden with it. <see cref="Release"/> gives the window back its own frame on the desktop.
    /// </summary>
    internal sealed class WindowHost : Panel
    {
        private const int GwlStyle = -16;
        private const int GwlExStyle = -20;
        private const long WsChild = 0x40000000L;
        private const long WsPopup = 0x80000000L;
        private const long WsCaption = 0x00C00000L;
        private const long WsThickFrame = 0x00040000L;
        private const long WsSysMenu = 0x00080000L;
        private const long WsMinimizeBox = 0x00020000L;
        private const long WsMaximizeBox = 0x00010000L;
        private const long WsExAppWindow = 0x00040000L;
        private const long WsExWindowEdge = 0x00000100L;
        private const long WsExClientEdge = 0x00000200L;
        private const long WsExDlgModalFrame = 0x00000001L;
        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpFrameChanged = 0x0020;
        private const uint SwpShowWindow = 0x0040;
        private const int SwRestore = 9;
        private const int SwShow = 5;

        private readonly Timer _watch = new Timer { Interval = 300 };
        private IntPtr _child;
        private long _style;
        private long _exStyle;

        public WindowHost()
        {
            BackColor = Theme.Background;

            // The guest's own window code re-applies its frame and size now and then (winit restores its styles on
            // focus and DPI changes); a light watch puts them back while the page is on screen.
            _watch.Tick += (_, _) => Enforce();
            VisibleChanged += (_, _) => _watch.Enabled = Visible && Child != IntPtr.Zero;
        }

        /// <summary>Where things are, for the selftest log: window rect, client origin and size, and styles.</summary>
        public string Describe()
        {
            var window = Child;
            if (window == IntPtr.Zero)
            {
                return "no child";
            }

            GetWindowRect(window, out var rect);
            GetClientRect(window, out var client);
            var origin = new NativePoint();
            ClientToScreen(window, ref origin);
            var host = RectangleToScreen(ClientRectangle);
            return $"host {host.X},{host.Y} {host.Width}x{host.Height}; window {rect.Left},{rect.Top} {rect.Right - rect.Left}x{rect.Bottom - rect.Top}; "
                + $"client origin {origin.X},{origin.Y} size {client.Right}x{client.Bottom}; "
                + $"style 0x{GetWindowLongPtr(window, GwlStyle).ToInt64():x} ex 0x{GetWindowLongPtr(window, GwlExStyle).ToInt64():x}";
        }

        /// <summary>The held window has no frame and covers exactly this panel (checked by the selftest).</summary>
        public bool Fits
        {
            get
            {
                var window = Child;
                if (window == IntPtr.Zero || !GetWindowRect(window, out var rect))
                {
                    return false;
                }

                var style = GetWindowLongPtr(window, GwlStyle).ToInt64();
                var screen = RectangleToScreen(ClientRectangle);
                return (style & WsCaption) != WsCaption && (style & WsThickFrame) == 0 && (style & WsChild) != 0
                    && rect.Left == screen.Left && rect.Top == screen.Top && rect.Right == screen.Right && rect.Bottom == screen.Bottom;
            }
        }

        public IntPtr Child => _child != IntPtr.Zero && IsWindow(_child) ? _child : IntPtr.Zero;

        public bool Holds(IntPtr window) => window != IntPtr.Zero && GetParent(window) == Handle;

        public void Adopt(IntPtr window)
        {
            _child = window;
            _style = GetWindowLongPtr(window, GwlStyle).ToInt64();
            _exStyle = GetWindowLongPtr(window, GwlExStyle).ToInt64();

            ShowWindow(window, SwRestore);
            var style = (_style & ~(WsPopup | WsCaption | WsThickFrame | WsSysMenu | WsMinimizeBox | WsMaximizeBox)) | WsChild;
            var exStyle = _exStyle & ~(WsExAppWindow | WsExWindowEdge | WsExClientEdge | WsExDlgModalFrame);
            SetWindowLongPtr(window, GwlStyle, new IntPtr(style));
            SetWindowLongPtr(window, GwlExStyle, new IntPtr(exStyle));
            SetParent(window, Handle);
            Fit(SwpFrameChanged | SwpShowWindow);
            ShowWindow(window, SwShow);
            _watch.Enabled = Visible;
            Log.Info($"embedded window 0x{window.ToInt64():x} into 0x{Handle.ToInt64():x}");
        }

        /// <summary>Puts the frame-less child style, the parent and the size back if the guest changed any of them.</summary>
        public void Enforce()
        {
            var window = Child;
            if (window == IntPtr.Zero)
            {
                _watch.Enabled = false;
                return;
            }

            var style = GetWindowLongPtr(window, GwlStyle).ToInt64();
            var unwanted = WsPopup | WsCaption | WsThickFrame | WsSysMenu | WsMinimizeBox | WsMaximizeBox;
            var flags = 0u;
            if ((style & unwanted) != 0 || (style & WsChild) == 0)
            {
                SetWindowLongPtr(window, GwlStyle, new IntPtr((style & ~unwanted) | WsChild));
                flags |= SwpFrameChanged;
            }

            if (GetParent(window) != Handle)
            {
                SetParent(window, Handle);
                flags |= SwpFrameChanged;
            }

            var screen = RectangleToScreen(ClientRectangle);
            if (flags != 0 || !GetWindowRect(window, out var rect)
                || rect.Left != screen.Left || rect.Top != screen.Top || rect.Right != screen.Right || rect.Bottom != screen.Bottom)
            {
                Fit(flags);
            }
        }

        /// <summary>Back to a normal window of its own, where it was on screen, so nothing open in it is lost.</summary>
        public void Release()
        {
            var window = Child;
            _child = IntPtr.Zero;
            _watch.Enabled = false;
            if (window == IntPtr.Zero)
            {
                return;
            }

            var screen = RectangleToScreen(ClientRectangle);
            SetParent(window, IntPtr.Zero);
            SetWindowLongPtr(window, GwlStyle, new IntPtr(_style));
            SetWindowLongPtr(window, GwlExStyle, new IntPtr(_exStyle));
            SetWindowPos(window, IntPtr.Zero, screen.X, screen.Y, Math.Max(800, screen.Width), Math.Max(500, screen.Height), SwpNoZOrder | SwpFrameChanged | SwpShowWindow);
            Log.Info($"released window 0x{window.ToInt64():x}");
        }

        /// <summary>Keyboard focus to the held window (it lives on another thread, so input queues are joined briefly).</summary>
        public void FocusChild()
        {
            var window = Child;
            if (window == IntPtr.Zero)
            {
                return;
            }

            var theirs = GetWindowThreadProcessId(window, out _);
            var ours = GetCurrentThreadId();
            AttachThreadInput(ours, theirs, true);
            SetFocus(window);
            AttachThreadInput(ours, theirs, false);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Fit(0);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            FocusChild();
            base.OnMouseDown(e);
        }

        private void Fit(uint flags)
        {
            var window = Child;
            if (window != IntPtr.Zero)
            {
                SetWindowPos(window, IntPtr.Zero, 0, 0, ClientSize.Width, ClientSize.Height, SwpNoZOrder | flags);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _watch.Dispose();
            }

            base.Dispose(disposing);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr child, IntPtr parent);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr window, out NativeRect rect);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr window, ref NativePoint point);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr window, int command);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out int processId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint attach, uint attachTo, bool join);

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr window);
    }
}
