using System.Drawing;
using System.Windows.Forms;

namespace Recora.App.UI.Controls
{
    /// <summary>A capture mode in the top bar: icon above label, a soft blue tile when selected.</summary>
    internal sealed class ModeButton : PaintedButton
    {
        private bool _selected;

        public ModeButton(IconKind icon, string text)
        {
            IconKind = icon;
            Text = text;
            Font = Theme.Small;
            AccessibleRole = AccessibleRole.RadioButton;
        }

        public IconKind IconKind { get; }

        public bool Selected
        {
            get => _selected;
            set
            {
                _selected = value;
                AccessibleDescription = value ? "Đang chọn" : null;
                Invalidate();
            }
        }

        protected override void PaintButton(Graphics g, RectangleF b)
        {
            var tile = RectangleF.Inflate(b, -2 * Dpi, -3 * Dpi);

            if (_selected)
            {
                Theme.FillRounded(g, Theme.AccentSoft, tile, 8 * Dpi);
                Theme.StrokeRounded(g, Theme.Accent, 1.2f * Dpi, tile, 8 * Dpi);
            }
            else if (Hover)
            {
                Theme.FillRounded(g, Theme.AccentHover, tile, 8 * Dpi);
            }

            var color = _selected ? Theme.Accent : Theme.Text;
            var iconSize = 22 * Dpi;
            Icons.Draw(g, IconKind, new RectangleF(b.X + (b.Width - iconSize) / 2, b.Y + 9 * Dpi, iconSize, iconSize), color);
            DrawCenteredText(g, Text, Theme.Small, color, new RectangleF(b.X, b.Y + 34 * Dpi, b.Width, b.Height - 38 * Dpi));
        }
    }

    /// <summary>A page in the left sidebar: icon and label, an accent bar marks the open page.</summary>
    internal sealed class NavButton : PaintedButton
    {
        private bool _selected;

        public NavButton(IconKind icon, string text)
        {
            IconKind = icon;
            Text = text;
            AccessibleRole = AccessibleRole.PageTab;
        }

        public IconKind IconKind { get; }

        public bool Selected
        {
            get => _selected;
            set
            {
                _selected = value;
                Invalidate();
            }
        }

        protected override void PaintButton(Graphics g, RectangleF b)
        {
            var row = new RectangleF(b.X + 8 * Dpi, b.Y + 2 * Dpi, b.Width - 16 * Dpi, b.Height - 4 * Dpi);

            if (_selected)
            {
                Theme.FillRounded(g, Theme.Surface, row, 8 * Dpi);
                Theme.FillRounded(g, Theme.Accent, new RectangleF(row.X, row.Y + 7 * Dpi, 3 * Dpi, row.Height - 14 * Dpi), 1.5f * Dpi);
            }
            else if (Hover)
            {
                Theme.FillRounded(g, Theme.AccentHover, row, 8 * Dpi);
            }

            var color = _selected ? Theme.Accent : Theme.Text;
            var iconSize = 18 * Dpi;
            Icons.Draw(g, IconKind, new RectangleF(row.X + 12 * Dpi, row.Y + (row.Height - iconSize) / 2, iconSize, iconSize), color);

            var textBox = Rectangle.Round(new RectangleF(row.X + 40 * Dpi, row.Y, row.Width - 44 * Dpi, row.Height));
            TextRenderer.DrawText(g, Text, _selected ? Theme.BodyBold : Theme.Body, textBox, color,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
    }

    internal enum RecordState
    {
        Idle,
        Recording,
        Paused,
    }

    /// <summary>The big red button: a dot to start, a square to stop. Pausing is a separate button next to it.</summary>
    internal sealed class RecordButton : PaintedButton
    {
        private RecordState _state;
        private string _shortcut = "F12";

        public RecordButton()
        {
            Text = "Ghi (F12)";
        }

        /// <summary>The record hotkey shown in the tooltip and accessible name.</summary>
        public string Shortcut
        {
            get => _shortcut;
            set
            {
                _shortcut = value;
                State = _state;
            }
        }

        public RecordState State
        {
            get => _state;
            set
            {
                _state = value;
                Text = (value == RecordState.Idle ? "Ghi" : "Dừng") + " (" + _shortcut + ")";
                Invalidate();
            }
        }

        protected override void PaintButton(Graphics g, RectangleF b)
        {
            var diameter = System.Math.Min(b.Width, b.Height) - 6 * Dpi;
            var circle = new RectangleF(b.X + (b.Width - diameter) / 2, b.Y + (b.Height - diameter) / 2, diameter, diameter);
            var fill = Pressed || Hover ? Theme.RecordHover : Theme.Record;

            using (var shadow = new SolidBrush(Color.FromArgb(40, Theme.Record)))
            {
                g.FillEllipse(shadow, RectangleF.Inflate(circle, 3 * Dpi, 3 * Dpi));
            }

            using (var brush = new SolidBrush(fill))
            {
                g.FillEllipse(brush, circle);
            }

            var glyph = RectangleF.Inflate(circle, -diameter * 0.27f, -diameter * 0.27f);
            Icons.Draw(g, _state == RecordState.Idle ? IconKind.Record : IconKind.Stop, glyph, Color.White);
        }
    }

    /// <summary>A round secondary action (pause, screenshot) beside the record button.</summary>
    internal sealed class IconButton : PaintedButton
    {
        private IconKind _icon;
        private Color _tint = Theme.Text;

        public IconButton(IconKind icon, string text)
        {
            _icon = icon;
            Text = text;
        }

        public IconKind IconKind
        {
            get => _icon;
            set
            {
                _icon = value;
                Invalidate();
            }
        }

        public Color Tint
        {
            get => _tint;
            set
            {
                _tint = value;
                Invalidate();
            }
        }

        protected override void PaintButton(Graphics g, RectangleF b)
        {
            var diameter = System.Math.Min(b.Width, b.Height) - 8 * Dpi;
            var circle = new RectangleF(b.X + (b.Width - diameter) / 2, b.Y + (b.Height - diameter) / 2, diameter, diameter);
            var enabledColor = Enabled ? _tint : Theme.Border;

            using (var brush = new SolidBrush(Hover && Enabled ? Theme.AccentHover : Theme.Surface))
            {
                g.FillEllipse(brush, circle);
            }

            using (var pen = new Pen(Theme.Border, 1.2f * Dpi))
            {
                g.DrawEllipse(pen, circle);
            }

            Icons.Draw(g, _icon, RectangleF.Inflate(circle, -diameter * 0.26f, -diameter * 0.26f), enabledColor);
        }
    }

    /// <summary>A secondary text button ("Mở thư mục", "Kiểm tra lại").</summary>
    internal sealed class FlatButton : PaintedButton
    {
        public FlatButton(string text)
        {
            Text = text;
            Font = Theme.Body;
        }

        public bool Primary { get; set; }

        public override Size GetPreferredSize(Size proposedSize)
        {
            var text = TextRenderer.MeasureText(Text, Font);
            return new Size(text.Width + (int)(28 * Dpi), (int)(34 * Dpi));
        }

        protected override void PaintButton(Graphics g, RectangleF b)
        {
            var box = RectangleF.Inflate(b, -1 * Dpi, -1 * Dpi);
            var fill = Primary ? (Hover ? Theme.FromHex(0x15528D) : Theme.Accent) : (Hover ? Theme.AccentHover : Theme.Surface);
            Theme.FillRounded(g, fill, box, 7 * Dpi);

            if (!Primary)
            {
                Theme.StrokeRounded(g, Theme.Border, 1.2f * Dpi, box, 7 * Dpi);
            }

            DrawCenteredText(g, Text, Font, Primary ? Color.White : (Enabled ? Theme.Text : Theme.Muted), box);
        }
    }
}
