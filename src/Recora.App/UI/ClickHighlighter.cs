using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Recora.App.Logging;

namespace Recora.App.UI
{
    /// <summary>
    /// While recording, a ring expands where the mouse is clicked (amber for left, red for right) so viewers of a
    /// tutorial see each click. The ring is an ordinary top-most window: it is meant to be captured.
    /// </summary>
    internal sealed class ClickHighlighter : IDisposable
    {
        private const int WhMouseLl = 14;
        private const int WmLButtonDown = 0x0201;
        private const int WmRButtonDown = 0x0204;
        private readonly HookProc _proc;
        private readonly Ripple _ripple = new Ripple();
        private IntPtr _hook;

        public ClickHighlighter()
        {
            // The delegate must outlive the hook, or the GC frees what Windows still calls.
            _proc = OnMouse;
            using (var module = Process.GetCurrentProcess().MainModule)
            {
                _hook = SetWindowsHookEx(WhMouseLl, _proc, GetModuleHandle(module?.ModuleName), 0);
            }

            if (_hook == IntPtr.Zero)
            {
                Log.Warn($"click highlight: SetWindowsHookEx failed ({Marshal.GetLastWin32Error()})");
            }
        }

        public void Dispose()
        {
            if (_hook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }

            _ripple.Dispose();
        }

        private IntPtr OnMouse(int code, IntPtr message, IntPtr data)
        {
            if (code >= 0)
            {
                var kind = message.ToInt32();
                if (kind == WmLButtonDown || kind == WmRButtonDown)
                {
                    var point = (MouseHook)Marshal.PtrToStructure(data, typeof(MouseHook));
                    _ripple.Play(new Point(point.X, point.Y), kind == WmLButtonDown ? Theme.Pause : Theme.Record);
                }
            }

            return CallNextHookEx(_hook, code, message, data);
        }

        private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseHook
        {
            public int X;
            public int Y;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public IntPtr Extra;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int hook, HookProc proc, IntPtr module, uint thread);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string? name);

        /// <summary>The ring itself: click-through, never focused, animated for a third of a second.</summary>
        private sealed class Ripple : Form
        {
            private const int Diameter = 72;
            private readonly Timer _timer = new Timer { Interval = 15 };
            private readonly Stopwatch _clock = new Stopwatch();
            private Color _color;

            public Ripple()
            {
                FormBorderStyle = FormBorderStyle.None;
                StartPosition = FormStartPosition.Manual;
                ShowInTaskbar = false;
                TopMost = true;
                AutoScaleMode = AutoScaleMode.None;
                BackColor = Color.Magenta;
                TransparencyKey = Color.Magenta;
                ClientSize = new System.Drawing.Size(Diameter, Diameter);
                DoubleBuffered = true;
                _timer.Tick += (_, _) =>
                {
                    if (_clock.ElapsedMilliseconds > 350)
                    {
                        _timer.Stop();
                        Hide();
                    }
                    else
                    {
                        Invalidate();
                    }
                };
            }

            protected override bool ShowWithoutActivation => true;

            protected override CreateParams CreateParams
            {
                get
                {
                    var parameters = base.CreateParams;
                    parameters.ExStyle |= 0x80000 | 0x20 | 0x80 | 0x08000000;
                    return parameters;
                }
            }

            public void Play(Point at, Color color)
            {
                _color = color;
                Location = new Point(at.X - Diameter / 2, at.Y - Diameter / 2);
                _clock.Restart();
                if (!Visible)
                {
                    Show();
                }

                Invalidate();
                _timer.Start();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var progress = Math.Min(1f, _clock.ElapsedMilliseconds / 350f);
                var radius = 10 + (Diameter / 2 - 6) * progress;
                e.Graphics.SmoothingMode = SmoothingMode.None;
                using (var pen = new Pen(_color, 4f - 2f * progress))
                {
                    e.Graphics.DrawEllipse(pen, Diameter / 2 - radius, Diameter / 2 - radius, radius * 2, radius * 2);
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _timer.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }
}
