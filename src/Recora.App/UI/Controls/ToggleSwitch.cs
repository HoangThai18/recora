using System;
using System.Drawing;
using System.Windows.Forms;

namespace Recora.App.UI.Controls
{
    /// <summary>An on/off switch with its label on the right; the whole row is clickable.</summary>
    internal sealed class ToggleSwitch : PaintedButton
    {
        private bool _checked;

        public ToggleSwitch(string text, bool isChecked = false)
        {
            Text = text;
            _checked = isChecked;
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
                Invalidate();
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
            var track = new RectangleF(b.X + 2 * Dpi, b.Y + (b.Height - 20 * Dpi) / 2, 38 * Dpi, 20 * Dpi);
            Theme.FillRounded(g, _checked ? Theme.Accent : Theme.Border, track, track.Height / 2);

            var knob = track.Height - 6 * Dpi;
            var knobX = _checked ? track.Right - knob - 3 * Dpi : track.X + 3 * Dpi;
            g.FillEllipse(Brushes.White, knobX, track.Y + 3 * Dpi, knob, knob);

            var textBox = Rectangle.Round(new RectangleF(track.Right + 10 * Dpi, b.Y, b.Width - track.Width - 12 * Dpi, b.Height));
            TextRenderer.DrawText(g, Text, Font, textBox, Enabled ? Theme.Text : Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
    }
}
