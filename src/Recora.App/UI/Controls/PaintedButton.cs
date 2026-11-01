using System;
using System.Drawing;
using System.Windows.Forms;
using Recora.Core.Animation;

namespace Recora.App.UI.Controls
{
    /// <summary>
    /// Base for the app's hand-painted buttons: double buffering, hover and pressed state, keyboard activation and a
    /// focus ring, so every button behaves like a real one for keyboard and screen-reader users.
    /// </summary>
    internal abstract class PaintedButton : Control
    {
        private readonly Tween _hoverGlow = new Tween(0);
        private bool _hover;
        private bool _pressed;

        protected PaintedButton()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable,
                true);
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.PushButton;
            BackColor = Color.Transparent;
            Font = Theme.Body;
        }

        protected bool Hover => _hover;

        /// <summary>0 → 1 as the pointer arrives (fast), back to 0 as it leaves (a little slower): blend hover colours with it.</summary>
        protected float HoverAmount => (float)_hoverGlow.Value;

        protected bool Pressed => _pressed;

        protected float Dpi => DeviceDpi / 96f * Theme.UiScale;

        public override string Text
        {
            get => base.Text;
            set
            {
                base.Text = value;
                AccessibleName = value;
                Invalidate();
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Animator.Animate(this, _hoverGlow, 1, 120);
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _pressed = false;
            Animator.Animate(this, _hoverGlow, 0, 220);
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _pressed = true;
                Invalidate();
            }

            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }

            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            Invalidate();
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            Invalidate();
            base.OnLostFocus(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Smooth(e.Graphics);
            PaintButton(e.Graphics, new RectangleF(0, 0, Width, Height));

            if (Focused && ShowFocusCues)
            {
                Theme.StrokeRounded(e.Graphics, Theme.Accent, 1.5f * Dpi, new RectangleF(1, 1, Width - 3, Height - 3), 6 * Dpi);
            }
        }

        protected abstract void PaintButton(Graphics g, RectangleF bounds);

        protected void DrawCenteredText(Graphics g, string text, Font font, Color color, RectangleF box)
        {
            TextRenderer.DrawText(
                g,
                text,
                font,
                Rectangle.Round(box),
                color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
    }
}
