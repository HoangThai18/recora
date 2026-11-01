using System;
using System.Drawing;
using System.Windows.Forms;
using Recora.App.UI.Controls;

namespace Recora.App.UI
{
    /// <summary>
    /// "Dựng phim": before Clipora runs, a card that explains it and offers to download or open it; once it runs, the
    /// whole page is Clipora's window (held by <see cref="Host"/>) under a slim bar.
    /// </summary>
    internal sealed class EditPage : Page
    {
        private readonly Label _status;
        private readonly ProgressStrip _progress;
        private readonly Panel _bar;
        private readonly Label _barText;

        public EditPage()
            : base("Dựng phim")
        {
            var intro = AddCard("dựng phim bằng clipora");
            intro.Body.Controls.Add(Caption(
                "Quay xong dựng luôn trong Recora: timeline nhiều track, cắt ghép, chỉnh màu, hiệu ứng, tiêu đề, phụ đề, trộn âm. "
                + "Bấm Dựng ở Thư viện là video nằm sẵn trên timeline.",
                Theme.Body, Theme.Text, 10));
            intro.Body.Controls.Add(Caption(
                "Trình dựng là Clipora (miễn phí, mã nguồn mở) chạy ngay trong cửa sổ này. Máy chưa có thì Recora tải bản không cần cài, "
                + "khoảng 70 MB, kiểm chữ ký SHA-256 rồi mới mở.",
                Theme.Small, Theme.Muted, 14));
            Open = new FlatButton("Mở trình dựng") { Primary = true, Size = new Size(230, 36), Margin = new Padding(0, 0, 0, 10) };
            intro.Body.Controls.Add(Open);
            _progress = new ProgressStrip { Size = new Size(420, 8), Margin = new Padding(0, 0, 0, 8), Visible = false };
            intro.Body.Controls.Add(_progress);
            _status = Caption(string.Empty, Theme.Small, Theme.Muted, 0);
            intro.Body.Controls.Add(_status);

            Host = new WindowHost { Dock = DockStyle.Fill, Visible = false };
            _bar = new MainForm.BorderedPanel(bottomBorder: true) { Dock = DockStyle.Top, Height = 44, BackColor = Theme.Surface, Visible = false, Padding = new Padding(14, 6, 10, 6) };
            _barText = new Label { AutoSize = false, Dock = DockStyle.Fill, Font = Theme.Small, ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleLeft, BackColor = Theme.Surface };
            Detach = new FlatButton("Tách ra cửa sổ riêng") { Dock = DockStyle.Right, Width = 190 };
            _bar.Controls.Add(_barText);
            _bar.Controls.Add(Detach);
            Controls.Add(Host);
            Controls.Add(_bar);
        }

        public FlatButton Open { get; }

        public FlatButton Detach { get; }

        public WindowHost Host { get; }

        public bool Running => Host.Visible;

        public void ShowIntro(string button, string status, bool busy)
        {
            Host.Visible = false;
            _bar.Visible = false;
            Stack.Visible = true;
            AutoScroll = true;
            Padding = new Padding(22, 18, 22, 18);
            Open.Text = button;
            Open.Enabled = !busy;
            _status.Text = status;
            _progress.Visible = false;
            RefitCards();
        }

        public void ShowProgress(long done, long total, string status)
        {
            _progress.Visible = true;
            _progress.Value = total > 0 ? (float)done / total : 0f;
            _status.Text = status;
        }

        public void ShowRunning(string text)
        {
            Stack.Visible = false;
            AutoScroll = false;
            Padding = Padding.Empty;
            _barText.Text = text;
            _bar.Visible = true;
            Host.Visible = true;
            Host.BringToFront();
        }

        /// <summary>A thin rounded bar for the download.</summary>
        private sealed class ProgressStrip : Control
        {
            private float _value;

            public ProgressStrip()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = Theme.Surface;
            }

            public float Value
            {
                get => _value;
                set
                {
                    _value = Math.Max(0, Math.Min(1, value));
                    Invalidate();
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Theme.Smooth(e.Graphics);
                var bar = new RectangleF(0, 0, Width - 1, Height - 1);
                Theme.FillRounded(e.Graphics, Theme.Background, bar, bar.Height / 2);
                if (_value > 0)
                {
                    Theme.FillRounded(e.Graphics, Theme.Accent, new RectangleF(0, 0, Math.Max(bar.Height, bar.Width * _value), bar.Height), bar.Height / 2);
                }
            }
        }
    }
}
