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

        private IntPtr _child;
        private long _style;
        private long _exStyle;

        public WindowHost()
        {
            BackColor = Theme.Background;
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
            Log.Info($"embedded window 0x{window.ToInt64():x} into 0x{Handle.ToInt64():x}");
        }

        /// <summary>Back to a normal window of its own, where it was on screen, so nothing open in it is lost.</summary>
        public void Release()
        {
            var window = Child;
            _child = IntPtr.Zero;
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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr child, IntPtr parent);

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
