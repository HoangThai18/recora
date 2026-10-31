using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Recora.App.UI.Controls
{
    /// <summary>
    /// Pick one of a few options shown side by side ("Rất nét | Cân bằng | Nhẹ"). Arrow keys move the choice, like a
    /// radio group; each option can carry a hint shown under the control.
    /// </summary>
    internal sealed class Segmented<T> : Control
    {
        private readonly IReadOnlyList<(T Value, string Label)> _options;
        private int _selected;
        private int _hover = -1;

        public Segmented(IEnumerable<(T Value, string Label)> options, T selected)
        {
            _options = options.ToList();
            _selected = Math.Max(0, _options.ToList().FindIndex(o => EqualityComparer<T>.Default.Equals(o.Value, selected)));
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Font = Theme.Body;
            BackColor = Theme.Surface;
            TabStop = true;
            AccessibleRole = AccessibleRole.List;
            Cursor = Cursors.Hand;
            Height = 34;
        }

        public event EventHandler? SelectedChanged;

        public T Value
        {
            get => _options[_selected].Value;
            set
            {
                var index = _options.ToList().FindIndex(o => EqualityComparer<T>.Default.Equals(o.Value, value));
                if (index >= 0 && index != _selected)
                {
                    _selected = index;
                    Invalidate();
                }
            }
        }

        private float Dpi => DeviceDpi / 96f * Theme.UiScale;

        public override Size GetPreferredSize(Size proposedSize)
        {
            var width = _options.Sum(o => TextRenderer.MeasureText(o.Label, Font).Width + (int)(28 * Dpi));
            return new Size(width + 4, (int)(34 * Dpi));
        }

        protected override bool IsInputKey(Keys keyData) => keyData == Keys.Left || keyData == Keys.Right || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left && _selected > 0)
            {
                Select(_selected - 1);
            }
            else if (e.KeyCode == Keys.Right && _selected < _options.Count - 1)
            {
                Select(_selected + 1);
            }

            base.OnKeyDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var hit = HitTest(e.X);
            if (hit != _hover)
            {
                _hover = hit;
                Invalidate();
            }

            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            var hit = HitTest(e.X);
            if (hit >= 0)
            {
                Select(hit);
            }

            base.OnMouseDown(e);
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
            var g = e.Graphics;
            Theme.Smooth(g);
            var dpi = Dpi;
            var outer = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.FillRounded(g, Theme.Background, outer, 8 * dpi);
            Theme.StrokeRounded(g, Focused ? Theme.Accent : Theme.Border, 1f * dpi, outer, 8 * dpi);

            var cells = Cells();
            for (var i = 0; i < _options.Count; i++)
            {
                var cell = RectangleF.Inflate(cells[i], -3 * dpi, -3 * dpi);
                if (i == _selected)
                {
                    Theme.FillRounded(g, Theme.Surface, cell, 6 * dpi);
                    Theme.StrokeRounded(g, Theme.Accent, 1.2f * dpi, cell, 6 * dpi);
                }
                else if (i == _hover)
                {
                    Theme.FillRounded(g, Theme.AccentHover, cell, 6 * dpi);
                }

                TextRenderer.DrawText(g, _options[i].Label, i == _selected ? Theme.BodyBold : Theme.Body, Rectangle.Round(cell),
                    i == _selected ? Theme.Accent : Theme.Text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
        }

        private void Select(int index)
        {
            if (index == _selected)
            {
                return;
            }

            _selected = index;
            AccessibleDescription = _options[index].Label;
            Invalidate();
            SelectedChanged?.Invoke(this, EventArgs.Empty);
        }

        private int HitTest(int x)
        {
            var cells = Cells();
            for (var i = 0; i < cells.Count; i++)
            {
                if (x >= cells[i].Left && x < cells[i].Right)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Cells sized by their labels and stretched to fill the width proportionally.</summary>
        private List<RectangleF> Cells()
        {
            var widths = _options.Select(o => (float)TextRenderer.MeasureText(o.Label, Font).Width + 28 * Dpi).ToList();
            var scale = (Width - 2) / Math.Max(1f, widths.Sum());
            var cells = new List<RectangleF>();
            var x = 1f;
            foreach (var w in widths)
            {
                cells.Add(new RectangleF(x, 0, w * scale, Height));
                x += w * scale;
            }

            return cells;
        }
    }
}
