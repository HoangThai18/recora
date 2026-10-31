using System.Drawing;
using System.Windows.Forms;

namespace Recora.App.UI.Controls
{
    /// <summary>
    /// A white rounded panel with an optional "// title" header in monospace, the window-card look of thaipro.store.
    /// Children go in <see cref="Body"/>.
    /// </summary>
    internal sealed class Card : Panel
    {
        private readonly string _title;

        public Card(string title)
        {
            _title = title;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Background;

            Body = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = false,
                BackColor = Theme.Surface,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            Controls.Add(Body);
        }

        public FlowLayoutPanel Body { get; }

        /// <summary>When set, <see cref="FitHeight"/> sizes the card to its content instead of a fixed height.</summary>
        public bool AutoHeight { get; set; }

        public void FitHeight()
        {
            var content = Body.GetPreferredSize(new System.Drawing.Size(Width - Padding.Horizontal, 0));
            Height = content.Height + Padding.Vertical;
        }

        protected override void OnHandleCreated(System.EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyPadding();
        }

        protected override void OnDpiChangedAfterParent(System.EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            ApplyPadding();
        }

        private void ApplyPadding()
        {
            var dpi = DeviceDpi / 96f * Theme.UiScale;
            int S(int value) => (int)(value * dpi);
            Padding = new Padding(S(18), S(string.IsNullOrEmpty(_title) ? 16 : 40), S(18), S(14));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            var dpi = DeviceDpi / 96f * Theme.UiScale;
            var box = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);

            Theme.FillRounded(g, Theme.Surface, box, 10 * dpi);
            Theme.StrokeRounded(g, Theme.Border, 1f * dpi, box, 10 * dpi);

            if (!string.IsNullOrEmpty(_title))
            {
                var header = Rectangle.Round(new RectangleF(18 * dpi, 12 * dpi, Width - 36 * dpi, 22 * dpi));
                TextRenderer.DrawText(g, "// " + _title, Theme.MonoSmall, header, Theme.Muted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }
    }
}
