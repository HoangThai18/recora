using System;
using System.Drawing;
using System.Windows.Forms;
using Recora.Core.Animation;

namespace Recora.App.UI.Controls
{
    /// <summary>An on/off switch with its label on the right; the whole row is clickable.</summary>
    internal sealed class ToggleSwitch : PaintedButton
    {
        private readonly Tween _knob;
        private bool _checked;

        public ToggleSwitch(string text, bool isChecked = false)
        {
            Text = text;
            _checked = isChecked;
            _knob = new Tween(isChecked ? 1 : 0);
            AccessibleRole = AccessibleRole.CheckButton;
        }

        public event EventHandler? CheckedChanged;

        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked == value)
                {
                    return;
                }

                _checked = value;
                AccessibleDescription = value ? "Bật" : "Tắt";
                Animator.Animate(this, _knob, value ? 1 : 0, 180);
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            var text = TextRenderer.MeasureText(Text, Font);
            return new Size(text.Width + (int)(56 * Dpi), Math.Max(text.Height, (int)(28 * Dpi)));
        }

        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            base.OnClick(e);
        }

        protected override void PaintButton(Graphics g, RectangleF b)
        {
            var on = (float)_knob.Value;
            var track = new RectangleF(b.X + 2 * Dpi, b.Y + (b.Height - 20 * Dpi) / 2, 38 * Dpi, 20 * Dpi);
            var off = Theme.Blend(Theme.Border, Theme.Muted, HoverAmount * 0.35);
            Theme.FillRounded(g, Theme.Blend(off, Theme.Accent, on), track, track.Height / 2);

            // The knob slides and stretches a little mid-way, like a drop of water.
            var knob = track.Height - 6 * Dpi;
            var stretch = knob * 0.35f * (float)(1 - System.Math.Abs(on * 2 - 1));
            var left = track.X + 3 * Dpi;
            var right = track.Right - knob - 3 * Dpi;
            var knobX = left + (right - left) * on - stretch / 2;
            Theme.FillRounded(g, Color.White, new RectangleF(knobX, track.Y + 3 * Dpi, knob + stretch, knob), knob / 2);

            var textBox = Rectangle.Round(new RectangleF(track.Right + 10 * Dpi, b.Y, b.Width - track.Width - 12 * Dpi, b.Height));
            TextRenderer.DrawText(g, Text, Font, textBox, Enabled ? Theme.Text : Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
    }
}
