using System;
using System.Drawing;
using System.Windows.Forms;
using Recora.App.UI.Controls;

namespace Recora.App.UI
{
    internal enum PageKind
    {
        Home,
        Video,
        Audio,
        Screenshot,
        Hotkeys,
        General,
        Library,
        About,
    }

    /// <summary>A page in the content area: a title row, then cards stacked top to bottom with scrolling.</summary>
    internal class Page : Panel
    {
        public Page(string title)
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            AutoScroll = true;
            Padding = new Padding(22, 18, 22, 18);

            Stack = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Theme.Background,
                Margin = Padding.Empty,
            };

            Stack.Controls.Add(new Label
            {
                Text = title,
                Font = Theme.Title,
                ForeColor = Theme.Text,
                AutoSize = true,
                Margin = new Padding(2, 0, 0, 12),
            });

            Controls.Add(Stack);
            Resize += (_, _) => FitCards();
        }

        protected FlowLayoutPanel Stack { get; }

        protected Card AddCard(string title, int height)
        {
            var card = new Card(title) { Height = height, Margin = new Padding(0, 0, 0, 14) };
            Stack.Controls.Add(card);
            FitCards();
            return card;
        }

        protected static Label Caption(string text, Font font, Color color, int bottom = 6) => new Label
        {
            Text = text,
            Font = font,
            ForeColor = color,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, bottom),
            BackColor = Theme.Surface,
        };

        private void FitCards()
        {
            var width = ClientSize.Width - Padding.Horizontal - SystemInformation.VerticalScrollBarWidth;
            foreach (Control control in Stack.Controls)
            {
                if (control is Card card)
                {
                    card.Width = Math.Max(300, width);
                }
            }
        }
    }

    /// <summary>What the home page shows; the app fills it from settings and the encoder probe.</summary>
    internal sealed record HomeModel(string Mode, string SourceDetail, string Encoder, string VideoSummary, bool SystemAudio, bool Microphone);

    internal sealed class HomePage : Page
    {
        private readonly Label _mode;
        private readonly Label _source;
        private readonly Label _encoder;
        private readonly Label _video;

        public HomePage()
            : base("Trang chủ")
        {
            var source = AddCard("nguồn ghi", 170);
            _mode = Caption(string.Empty, Theme.Title, Theme.Accent, 2);
            _source = Caption(string.Empty, Theme.Body, Theme.Muted, 2);
            _encoder = Caption(string.Empty, Theme.Mono, Theme.Text, 14);
            source.Body.Controls.Add(_mode);
            source.Body.Controls.Add(_source);
            source.Body.Controls.Add(_encoder);

            var hints = new FlowLayoutPanel { AutoSize = true, WrapContents = true, BackColor = Theme.Surface, Margin = Padding.Empty };
            hints.Controls.Add(Hint("F12", "Ghi / Dừng"));
            hints.Controls.Add(Hint("Shift+F12", "Tạm dừng"));
            hints.Controls.Add(Hint("F11", "Chụp ảnh"));
            source.Body.Controls.Add(hints);

            var audio = AddCard("âm thanh", 118);
            SystemAudio = new ToggleSwitch("Tiếng máy (loa, tai nghe)", true) { Size = new Size(280, 30), Margin = new Padding(0, 0, 0, 6) };
            Microphone = new ToggleSwitch("Micro", true) { Size = new Size(280, 30), Margin = Padding.Empty };
            audio.Body.Controls.Add(SystemAudio);
            audio.Body.Controls.Add(Microphone);

            var video = AddCard("video", 112);
            _video = Caption(string.Empty, Theme.Body, Theme.Text, 10);
            ChangeQuality = new FlatButton("Đổi chất lượng") { Size = new Size(150, 34), Margin = Padding.Empty };
            video.Body.Controls.Add(_video);
            video.Body.Controls.Add(ChangeQuality);
        }

        public ToggleSwitch SystemAudio { get; }

        public ToggleSwitch Microphone { get; }

        public FlatButton ChangeQuality { get; }

        public void Display(HomeModel model)
        {
            _mode.Text = model.Mode;
            _source.Text = model.SourceDetail;
            _encoder.Text = "> " + model.Encoder;
            _video.Text = model.VideoSummary;
            SystemAudio.Checked = model.SystemAudio;
            Microphone.Checked = model.Microphone;
        }

        private static HotkeyHint Hint(string keys, string action)
        {
            var hint = new HotkeyHint(keys, action) { Margin = new Padding(0, 0, 18, 0) };
            hint.Size = hint.GetPreferredSize(Size.Empty);
            return hint;
        }
    }

    /// <summary>Pages whose settings arrive in a later phase show what they will hold.</summary>
    internal sealed class PlaceholderPage : Page
    {
        public PlaceholderPage(string title, string description)
            : base(title)
        {
            var card = AddCard(string.Empty, 90);
            card.Body.Controls.Add(Caption(description, Theme.Body, Theme.Muted));
        }
    }
}
