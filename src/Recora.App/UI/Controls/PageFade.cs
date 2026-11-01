using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Recora.Core.Animation;

namespace Recora.App.UI.Controls
{
    /// <summary>
    /// Page change: for a moment the content area shows a picture of the old page fading out over the new one, which
    /// rises a few pixels into place. Then the overlay goes away and the real, live page is underneath.
    /// WinForms cannot make a page itself translucent, hence the two snapshots.
    /// </summary>
    internal sealed class PageFade : Control
    {
        private readonly Tween _progress = new Tween(1);
        private readonly object _key = new object();
        private Bitmap? _before;
        private Bitmap? _after;

        public PageFade()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Visible = false;
        }

        public void Play(Control content, Bitmap before)
        {
            Finish();
            _before = before;
            _after = Snapshot(content);
            if (_after == null)
            {
                Finish();
                return;
            }

            Bounds = content.ClientRectangle;
            Visible = true;
            BringToFront();
            _progress.Jump(0);
            Animator.Animate(this, _progress, 1, 200);
            Animator.Loop(this, _key, () =>
            {
                if (_progress.IsRunning)
                {
                    return true;
                }

                BeginInvoke(new Action(Finish));
                return false;
            });
        }

        /// <summary>The content area as it looks now (its visible children drawn into one picture).</summary>
        public static Bitmap? Snapshot(Control content)
        {
            if (content.Width <= 0 || content.Height <= 0)
            {
                return null;
            }

            content.PerformLayout();
            var bitmap = new Bitmap(content.Width, content.Height);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(content.BackColor);
            }

            foreach (Control child in content.Controls)
            {
                if (child.Visible && !(child is PageFade) && child.Width > 0 && child.Height > 0)
                {
                    child.DrawToBitmap(bitmap, child.Bounds);
                }
            }

            return bitmap;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_before == null || _after == null)
            {
                return;
            }

            var t = (float)_progress.Value;
            var rise = (int)Math.Round((1 - t) * 12 * DeviceDpi / 96f);
            e.Graphics.Clear(BackColor);
            e.Graphics.DrawImageUnscaled(_after, 0, rise);

            using (var attributes = new ImageAttributes())
            {
                attributes.SetColorMatrix(new ColorMatrix { Matrix33 = 1 - t });
                e.Graphics.DrawImage(_before, new Rectangle(0, 0, _before.Width, _before.Height), 0, 0, _before.Width, _before.Height, GraphicsUnit.Pixel, attributes);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _before?.Dispose();
                _after?.Dispose();
            }

            base.Dispose(disposing);
        }

        private void Finish()
        {
            Visible = false;
            _before?.Dispose();
            _after?.Dispose();
            _before = null;
            _after = null;
        }
    }
}
