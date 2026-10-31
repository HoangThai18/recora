using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using Recora.App.Native;

namespace Recora.App.UI
{
    /// <summary>
    /// 3, 2, 1 in the middle of the screen about to be recorded. Excluded from capture, so even a slow start can't put
    /// it in the video; Esc cancels the recording.
    /// </summary>
    internal sealed class Countdown : Form
    {
        private readonly TaskCompletionSource<bool> _done = new TaskCompletionSource<bool>();
        private readonly Timer _timer = new Timer { Interval = 1000 };
        private int _left;

        private Countdown(Rectangle screen, int seconds)
        {
            _left = seconds;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.Magenta;
            TransparencyKey = Color.Magenta;
            var size = Math.Min(screen.Width, screen.Height) / 4;
            Bounds = new Rectangle(screen.X + (screen.Width - size) / 2, screen.Y + (screen.Height - size) / 2, size, size);
            _timer.Tick += (_, _) =>
            {
                _left--;
                if (_left <= 0)
                {
                    Finish(true);
                }
                else
                {
                    Invalidate();
                }
            };
        }

        /// <summary>Returns true when the countdown ran out, false if the user pressed Esc.</summary>
        public static Task<bool> Run(Rectangle screen, int seconds = 3)
        {
            var form = new Countdown(screen, seconds);
            form.Show();
            form.Activate();
            return form._done.Task;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            NativeMethods.SetWindowDisplayAffinity(Handle, NativeMethods.WdaExcludeFromCapture);
            _timer.Start();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Finish(false);
            }

            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var box = new RectangleF(4, 4, Width - 8, Height - 8);
            using (var fill = new SolidBrush(Color.FromArgb(235, Theme.FromHex(0x13263D))))
            using (var ring = new Pen(Theme.Record, Width / 28f))
            {
                g.FillEllipse(fill, box);
                g.DrawEllipse(ring, RectangleF.Inflate(box, -Width / 40f, -Width / 40f));
            }

            using (var font = new Font("Segoe UI Semibold", Height / 3.2f, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                TextRenderer.DrawText(g, _left.ToString(), font, Rectangle.Round(box), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
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

        private void Finish(bool ranOut)
        {
            _timer.Stop();
            Close();
            _done.TrySetResult(ranOut);
        }
    }
}
