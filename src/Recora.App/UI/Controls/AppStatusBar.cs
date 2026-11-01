using System.Drawing;
using System.Windows.Forms;
using Recora.Core.Animation;

namespace Recora.App.UI.Controls
{
    /// <summary>The blue strip at the bottom (VS Code style): state dot and text on the left, output folder on the right.</summary>
    internal sealed class AppStatusBar : Control
    {
        private string _state = "Sẵn sàng";
        private readonly Tween _dotMix = new Tween(1);
        private Color _dot = Theme.Ok;
        private Color _previousDot = Theme.Ok;
        private string _details = string.Empty;
        private string _right = string.Empty;

        public AppStatusBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Accent;
            AccessibleRole = AccessibleRole.StatusBar;
        }

        public void SetState(string state, Color dot)
        {
            _state = state;
            AccessibleName = state;
            if (dot != _dot)
            {
                // The state colour crossfades: green "ready" warming into red "recording".
                _previousDot = Theme.Blend(_previousDot, _dot, _dotMix.Value);
                _dot = dot;
                _dotMix.Jump(0);
                Animator.Animate(this, _dotMix, 1, 320);
            }

            Invalidate();
        }

        public string Details
        {
            get => _details;
            set
            {
                _details = value;
                Invalidate();
            }
        }

        public string RightText
        {
            get => _right;
            set
            {
                _right = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            var dpi = DeviceDpi / 96f * Theme.UiScale;
            var dot = 8 * dpi;

            using (var halo = new SolidBrush(Color.FromArgb(70, Color.White)))
            using (var brush = new SolidBrush(Theme.Blend(_previousDot, _dot, _dotMix.Value)))
            {
                g.FillEllipse(halo, 12 * dpi - 2 * dpi, (Height - dot) / 2 - 2 * dpi, dot + 4 * dpi, dot + 4 * dpi);
                g.FillEllipse(brush, 12 * dpi, (Height - dot) / 2, dot, dot);
            }

            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            var stateWidth = TextRenderer.MeasureText(g, _state, Theme.BodyBold, Size.Empty, flags).Width;
            var x = (int)(28 * dpi);
            TextRenderer.DrawText(g, _state, Theme.BodyBold, new Rectangle(x, 0, stateWidth + 4, Height), Theme.StatusText, flags);

            var rightWidth = string.IsNullOrEmpty(_right) ? 0 : TextRenderer.MeasureText(g, _right, Theme.MonoSmall, Size.Empty, flags).Width;
            var rightBox = new Rectangle(Width - rightWidth - (int)(14 * dpi), 0, rightWidth + 2, Height);
            var detailsBox = new Rectangle(x + stateWidth + (int)(14 * dpi), 0, rightBox.X - x - stateWidth - (int)(28 * dpi), Height);

            TextRenderer.DrawText(g, _details, Theme.MonoSmall, detailsBox, Color.FromArgb(225, Color.White), flags | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, _right, Theme.MonoSmall, rightBox, Color.FromArgb(225, Color.White), flags);
        }
    }

    /// <summary>A keycap ("F12") followed by what it does, drawn in the monospace dev style.</summary>
    internal sealed class HotkeyHint : Control
    {
        private readonly string _keys;
        private readonly string _action;

        public HotkeyHint(string keys, string action)
        {
            _keys = keys;
            _action = action;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            AccessibleName = keys + ": " + action;
            AccessibleRole = AccessibleRole.StaticText;
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            var dpi = DeviceDpi / 96f * Theme.UiScale;
            var key = TextRenderer.MeasureText(_keys, Theme.Mono);
            var action = TextRenderer.MeasureText(_action, Theme.Body);
            return new Size(key.Width + action.Width + (int)(30 * dpi), (int)(30 * dpi));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            var dpi = DeviceDpi / 96f * Theme.UiScale;
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            var keyWidth = TextRenderer.MeasureText(g, _keys, Theme.Mono, Size.Empty, flags).Width + 14 * dpi;
            var cap = new RectangleF(1, 3 * dpi, keyWidth, Height - 7 * dpi);

            Theme.FillRounded(g, Theme.Background, cap, 5 * dpi);
            Theme.StrokeRounded(g, Theme.Border, 1f * dpi, cap, 5 * dpi);
            using (var pen = new Pen(Theme.Border, 2f * dpi))
            {
                g.DrawLine(pen, cap.X + 4 * dpi, cap.Bottom, cap.Right - 4 * dpi, cap.Bottom);
            }

            TextRenderer.DrawText(g, _keys, Theme.Mono, Rectangle.Round(cap), Theme.Accent, flags);
            var actionBox = new Rectangle((int)(cap.Right + 8 * dpi), 0, Width - (int)(cap.Right + 8 * dpi), Height);
            TextRenderer.DrawText(g, _action, Theme.Body, actionBox, Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
        }
    }
}
