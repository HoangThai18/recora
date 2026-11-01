using System.Drawing;
using System.Windows.Forms;

namespace Recora.App.UI.Controls
{
    /// <summary>
    /// A rounded surface panel with an optional title, the card look of Windows 11 Settings.
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
            // The padding depends on DPI; set it here too, so a card measured before its handle exists isn't short.
            ApplyPadding();
            var content = Body.GetPreferredSize(new System.Drawing.Size(Width - Padding.Horizontal, 0));
            Height = content.Height + Padding.Vertical;
        }

        protected override void OnHandleCreated(System.EventArgs e)
        {
            base.OnHandleCreated(e);
            Refit();
        }

        protected override void OnDpiChangedAfterParent(System.EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            Refit();
        }

        private void Refit()
        {
            if (AutoHeight)
            {
                FitHeight();
            }
            else
            {
                ApplyPadding();
            }
        }

        private void ApplyPadding()
        {
            var dpi = DeviceDpi / 96f * Theme.UiScale;
            int S(int value) => (int)(value * dpi);
            Padding = new Padding(S(20), S(string.IsNullOrEmpty(_title) ? 18 : 46), S(20), S(16));
        }

        private static string Capitalize(string title) => title.Length == 0 ? title : char.ToUpper(title[0]) + title.Substring(1);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            var dpi = DeviceDpi / 96f * Theme.UiScale;
            var box = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);

            Theme.FillRounded(g, Theme.Surface, box, 12 * dpi);
            Theme.StrokeRounded(g, Theme.Border, 1f * dpi, box, 12 * dpi);

            if (!string.IsNullOrEmpty(_title))
            {
                var header = Rectangle.Round(new RectangleF(20 * dpi, 14 * dpi, Width - 40 * dpi, 24 * dpi));
                TextRenderer.DrawText(g, Capitalize(_title), Theme.BodyBold, header, Theme.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }
        }
    }
}
