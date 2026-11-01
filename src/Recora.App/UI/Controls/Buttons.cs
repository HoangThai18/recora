using System;
using System.Drawing;
using System.Windows.Forms;
using Recora.Core.Animation;

namespace Recora.App.UI.Controls
{
    /// <summary>A capture mode in the top bar: icon above label, a soft blue tile when selected.</summary>
    internal sealed class ModeButton : PaintedButton
    {
        private readonly Tween _selection = new Tween(0);
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
                Animator.Animate(this, _selection, value ? 1 : 0, 200);
            }
        }

        protected override void PaintButton(Graphics g, RectangleF b)
        {
            var chosen = (float)_selection.Value;
            var tile = RectangleF.Inflate(b, -2 * Dpi, -3 * Dpi);

            var hover = HoverAmount * (1 - chosen);
            if (hover > 0.01f)
            {
                Theme.FillRounded(g, Theme.Fade(Theme.AccentHover, hover), tile, 8 * Dpi);
            }

            if (chosen > 0.01f)
            {
                // The tile grows out from the centre as it is chosen.
                var grown = RectangleF.Inflate(tile, -tile.Width * 0.12f * (1 - chosen), -tile.Height * 0.12f * (1 - chosen));
                Theme.FillRounded(g, Theme.Fade(Theme.AccentSoft, chosen), grown, 8 * Dpi);
                Theme.StrokeRounded(g, Theme.Fade(Theme.Accent, chosen), 1.2f * Dpi, grown, 8 * Dpi);
            }

            var color = Theme.Blend(Theme.Text, Theme.Accent, chosen);
            var iconSize = 22 * Dpi;
            var lift = 2 * Dpi * HoverAmount;
            Icons.Draw(g, IconKind, new RectangleF(b.X + (b.Width - iconSize) / 2, b.Y + 9 * Dpi - lift, iconSize, iconSize), color);
            DrawCenteredText(g, Text, Theme.Small, color, new RectangleF(b.X, b.Y + 34 * Dpi, b.Width, b.Height - 38 * Dpi));
        }
    }

    /// <summary>A page in the left sidebar: icon and label, an accent bar marks the open page.</summary>
    internal sealed class NavButton : PaintedButton
    {
        private readonly Tween _selection = new Tween(0);
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
                Animator.Animate(this, _selection, value ? 1 : 0, 220);
            }
        }

        protected override void PaintButton(Graphics g, RectangleF b)
        {
            var chosen = (float)_selection.Value;
            var row = new RectangleF(b.X + 8 * Dpi, b.Y + 2 * Dpi, b.Width - 16 * Dpi, b.Height - 4 * Dpi);

            var hover = HoverAmount * (1 - chosen);
            if (hover > 0.01f)
            {
                Theme.FillRounded(g, Theme.Fade(Theme.AccentHover, hover), row, 8 * Dpi);
            }

            if (chosen > 0.01f)
            {
                // The white row fades in and the accent bar grows from the middle.
                Theme.FillRounded(g, Theme.Fade(Theme.Surface, chosen), row, 8 * Dpi);
                var barHeight = (row.Height - 14 * Dpi) * chosen;
                Theme.FillRounded(g, Theme.Accent, new RectangleF(row.X, row.Y + (row.Height - barHeight) / 2, 3 * Dpi, barHeight), 1.5f * Dpi);
            }

            var color = Theme.Blend(Theme.Text, Theme.Accent, chosen);
            var iconSize = 18 * Dpi;
            var nudge = 2 * Dpi * HoverAmount * (1 - chosen);
            Icons.Draw(g, IconKind, new RectangleF(row.X + 12 * Dpi + nudge, row.Y + (row.Height - iconSize) / 2, iconSize, iconSize), color);

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
        private static readonly TimeSpan Breath = TimeSpan.FromMilliseconds(1600);
        private readonly object _pulse = new object();
        private readonly Tween _morph = new Tween(0);
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
                Animator.Animate(this, _morph, value == RecordState.Idle ? 0 : 1, 260);
                StartBreathing();
            }
        }

        /// <summary>Keeps the halo moving while recording and on screen; the animator stops it when hidden or idle.</summary>
        private void StartBreathing()
        {
            if (_state == RecordState.Recording && !Animator.IsLooping(_pulse))
            {
                Animator.Loop(this, _pulse, () => _state == RecordState.Recording);
            }
        }

        protected override void PaintButton(Graphics g, RectangleF b)
        {
            StartBreathing();
            var diameter = Math.Min(b.Width, b.Height) - 6 * Dpi;
            var press = Pressed ? 0.94f : 1f;
            diameter *= press;
            var circle = new RectangleF(b.X + (b.Width - diameter) / 2, b.Y + (b.Height - diameter) / 2, diameter, diameter);
            var fill = Theme.Blend(Theme.Record, Theme.RecordHover, Pressed ? 1 : HoverAmount);

            // While recording the halo breathes, so the window says "recording" even from the corner of an eye.
            var breathing = _state == RecordState.Recording && Animator.Enabled ? (float)Easing.Pulse(Animator.Now, Breath) : 0f;
            var halo = (3 + 5 * breathing) * Dpi;
            using (var shadow = new SolidBrush(Color.FromArgb((int)(40 + 30 * (1 - breathing)), Theme.Record)))
            {
                g.FillEllipse(shadow, RectangleF.Inflate(circle, halo, halo));
            }

            using (var brush = new SolidBrush(fill))
            {
                g.FillEllipse(brush, circle);
            }

            // The dot turns into the stop square: both are drawn, crossfading as the state changes.
            var morph = (float)_morph.Value;
            var glyph = RectangleF.Inflate(circle, -diameter * 0.27f, -diameter * 0.27f);
            if (morph < 0.99f)
            {
                Icons.Draw(g, IconKind.Record, RectangleF.Inflate(glyph, -glyph.Width * 0.3f * morph, -glyph.Height * 0.3f * morph), Theme.Fade(Color.White, 1 - morph));
            }

            if (morph > 0.01f)
            {
                Icons.Draw(g, IconKind.Stop, RectangleF.Inflate(glyph, -glyph.Width * 0.3f * (1 - morph), -glyph.Height * 0.3f * (1 - morph)), Theme.Fade(Color.White, morph));
            }
        }
    }

    /// <summary>A round secondary action (pause, screenshot) beside the record button.</summary>
    internal sealed class IconButton : PaintedButton, IThemeColors
    {
        private IconKind _icon;
        // Null follows the palette's text colour, so switching light/dark recolours the icon too.
        private Color? _tint;

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
            get => _tint ?? Theme.Text;
            set
            {
                _tint = value;
                Invalidate();
            }
        }

        public void Remap(Func<Color, Color> map)
        {
            if (_tint.HasValue)
            {
                _tint = map(_tint.Value);
            }
        }

        protected override void PaintButton(Graphics g, RectangleF b)
        {
            var diameter = (Math.Min(b.Width, b.Height) - 8 * Dpi) * (Pressed ? 0.94f : 1f);
            var circle = new RectangleF(b.X + (b.Width - diameter) / 2, b.Y + (b.Height - diameter) / 2, diameter, diameter);
            var enabledColor = Enabled ? Tint : Theme.Blend(Theme.Border, Theme.Muted, 0.45);

            using (var brush = new SolidBrush(Enabled ? Theme.Blend(Theme.Surface, Theme.AccentHover, HoverAmount) : Theme.Surface))
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
            var shrink = Pressed ? 1.5f * Dpi : 0f;
            var box = RectangleF.Inflate(b, -1 * Dpi - shrink, -1 * Dpi - shrink);
            var fill = Primary
                ? Theme.Blend(Theme.AccentStrong, Theme.AccentPressed, HoverAmount)
                : Theme.Blend(Theme.Surface, Theme.AccentHover, HoverAmount);
            if (!Enabled)
            {
                // A switched-off primary button must not look clickable.
                fill = Primary ? Theme.Blend(fill, Theme.Surface, 0.55) : Theme.Blend(fill, Theme.Background, 0.5);
            }

            Theme.FillRounded(g, fill, box, 8 * Dpi);

            if (!Primary)
            {
                Theme.StrokeRounded(g, Theme.Blend(Theme.Border, Theme.Accent, HoverAmount * 0.6), 1.2f * Dpi, box, 8 * Dpi);
            }

            DrawCenteredText(g, Text, Font, Primary ? Color.FromArgb(Enabled ? 255 : 200, Color.White) : (Enabled ? Theme.Text : Theme.Muted), box);
        }
    }
}
