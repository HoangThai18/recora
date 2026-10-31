using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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

            // Pages are built hidden; text and DPI are final only once shown.
            VisibleChanged += (_, _) =>
            {
                if (Visible)
                {
                    FitCards();
                }
            };
        }

        protected FlowLayoutPanel Stack { get; }

        /// <summary>Adds a card; with no height it grows to fit its content.</summary>
        protected Card AddCard(string title, int height = 0)
        {
            var card = new Card(title) { Height = height > 0 ? height : 60, AutoHeight = height <= 0, Margin = new Padding(0, 0, 0, 14) };
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

        protected void RefitCards() => FitCards();

        private void FitCards()
        {
            var width = ClientSize.Width - Padding.Horizontal - SystemInformation.VerticalScrollBarWidth;
            foreach (Control control in Stack.Controls)
            {
                if (control is Card card)
                {
                    card.Width = Math.Max(300, width);
                    if (card.AutoHeight)
                    {
                        card.FitHeight();
                    }
                }
            }
        }
    }

    /// <summary>What the home page shows; the app fills it from settings and the encoder probe.</summary>
    /// <summary>What the home page shows; the app fills it from settings and the encoder probe.</summary>
    internal sealed record HomeModel(
        string Mode,
        string SourceDetail,
        string Encoder,
        IReadOnlyList<string> VideoPills,
        bool SystemAudio,
        bool Microphone);

    /// <summary>
    /// The first thing the user sees: a live picture of the screen that will be recorded (a big clock while recording),
    /// the screens to pick from, sound sources with live levels, and the video settings at a glance.
    /// </summary>
    internal sealed class HomePage : Page
    {
        private readonly Label _mode;
        private readonly Label _source;
        private readonly Label _encoder;
        private readonly FlowLayoutPanel _pills;
        private readonly SourceRow _row;
        private readonly Card _sourceCard;
        private readonly Label _pickerLabel;
        private bool _filling;

        public HomePage()
            : base("Trang chủ")
        {
            var source = AddCard("nguồn ghi");
            _sourceCard = source;
            Preview = new PreviewBox();
            _mode = Caption(string.Empty, Theme.Title, Theme.Accent, 2);
            _source = Caption(string.Empty, Theme.Body, Theme.Muted, 2);
            _encoder = Caption(string.Empty, Theme.Mono, Theme.Text, 12);
            Monitors = new MonitorMap { Size = new Size(240, 64), Margin = new Padding(0, 0, 0, 12) };
            Monitors.Picked += (_, device) =>
            {
                if (!_filling)
                {
                    MonitorSelected?.Invoke(this, device);
                }
            };

            _pickerLabel = Caption("Màn hình", Theme.Small, Theme.Muted, 4);
            Devices = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.System,
                Font = Theme.Body,
                Width = 300,
                Margin = new Padding(0, 0, 0, 12),
                AccessibleName = "Webcam hoặc thiết bị cần ghi",
            };
            Devices.SelectionChangeCommitted += (_, _) =>
            {
                if (Devices.SelectedItem is string device)
                {
                    DeviceSelected?.Invoke(this, device);
                }
            };
            AudioFormats = new Segmented<Core.Ffmpeg.AudioFormat>(
                new[] { (Core.Ffmpeg.AudioFormat.M4a, "M4A"), (Core.Ffmpeg.AudioFormat.Mp3, "MP3"), (Core.Ffmpeg.AudioFormat.Wav, "WAV") },
                Core.Ffmpeg.AudioFormat.M4a)
            {
                Width = 240,
                Margin = new Padding(0, 0, 0, 12),
            };

            var details = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Surface, Margin = Padding.Empty };
            details.Controls.AddRange(new Control[] { _mode, _source, _encoder, _pickerLabel, Monitors, Devices, AudioFormats });
            _row = new SourceRow(Preview, details) { Margin = Padding.Empty };
            source.Body.Controls.Add(_row);
            source.Body.SizeChanged += (_, _) => FitRow(source);
            ShowPicker(PickerKind.Monitors);

            var audio = AddCard("âm thanh");
            SystemAudio = new ToggleSwitch("Tiếng máy (loa, tai nghe)", true) { Size = new Size(280, 30), Margin = Padding.Empty };
            Microphone = new ToggleSwitch("Micro", true) { Size = new Size(280, 30), Margin = Padding.Empty };
            SystemLevel = new LevelMeter { Size = new Size(220, 30), Margin = new Padding(12, 0, 0, 0) };
            MicrophoneLevel = new LevelMeter { Size = new Size(220, 30), Margin = new Padding(12, 0, 0, 0) };
            audio.Body.Controls.Add(Row(SystemAudio, SystemLevel, 6));
            audio.Body.Controls.Add(Row(Microphone, MicrophoneLevel, 0));

            var video = AddCard("video");
            _pills = new FlowLayoutPanel { AutoSize = true, WrapContents = true, BackColor = Theme.Surface, Margin = new Padding(0, 0, 0, 12) };
            ChangeQuality = new FlatButton("Đổi chất lượng") { Size = new Size(150, 34), Margin = Padding.Empty };
            video.Body.Controls.Add(_pills);
            video.Body.Controls.Add(ChangeQuality);
            video.Body.SizeChanged += (_, _) => _pills.MaximumSize = new Size(Math.Max(100, video.Body.ClientSize.Width), 0);
        }

        public event EventHandler<string>? MonitorSelected;

        public event EventHandler<string>? DeviceSelected;

        public enum PickerKind
        {
            Monitors,
            Devices,
            AudioFormats,
            None,
        }

        public ComboBox Devices { get; }

        public Segmented<Core.Ffmpeg.AudioFormat> AudioFormats { get; }

        /// <summary>The control under the source text: the screens, the webcams, or the sound file formats.</summary>
        public void ShowPicker(PickerKind kind)
        {
            Monitors.Visible = kind == PickerKind.Monitors;
            Devices.Visible = kind == PickerKind.Devices;
            AudioFormats.Visible = kind == PickerKind.AudioFormats;
            _pickerLabel.Visible = kind != PickerKind.None;
            _pickerLabel.Text = kind switch
            {
                PickerKind.Devices => "Thiết bị",
                PickerKind.AudioFormats => "Định dạng",
                _ => "Màn hình",
            };
            FitRow(_sourceCard);
            RefitCards();
        }

        /// <summary>Fills the webcam list; an empty list shows a single disabled hint.</summary>
        public void ShowDevices(IReadOnlyList<string> devices, string selected)
        {
            Devices.Items.Clear();
            foreach (var device in devices)
            {
                Devices.Items.Add(device);
            }

            Devices.Enabled = devices.Count > 0;
            if (devices.Count == 0)
            {
                Devices.Items.Add("Không tìm thấy webcam nào");
                Devices.SelectedIndex = 0;
                return;
            }

            var index = devices.ToList().FindIndex(d => d == selected);
            Devices.SelectedIndex = index >= 0 ? index : 0;
        }

        public PreviewBox Preview { get; }

        public MonitorMap Monitors { get; }

        public ToggleSwitch SystemAudio { get; }

        public ToggleSwitch Microphone { get; }

        public LevelMeter SystemLevel { get; }

        public LevelMeter MicrophoneLevel { get; }

        public FlatButton ChangeQuality { get; }

        public void Display(HomeModel model)
        {
            _mode.Text = model.Mode;
            _source.Text = model.SourceDetail;
            _encoder.Text = "> " + model.Encoder;
            SystemAudio.Checked = model.SystemAudio;
            Microphone.Checked = model.Microphone;
            SystemLevel.Active = model.SystemAudio;
            MicrophoneLevel.Active = model.Microphone;

            _pills.SuspendLayout();
            _pills.Controls.Clear();
            for (var i = 0; i < model.VideoPills.Count; i++)
            {
                _pills.Controls.Add(new Pill(model.VideoPills[i], strong: i == 0) { AutoSize = true, Margin = new Padding(0, 0, 8, 6) });
            }

            _pills.ResumeLayout();
            FitRow(_sourceCard);
            RefitCards();
        }

        /// <summary>Fills the screen picker without raising <see cref="MonitorSelected"/>.</summary>
        public void ShowMonitors(IReadOnlyList<MonitorTile> monitors, string selectedDevice)
        {
            _filling = true;
            Monitors.SetTiles(monitors, selectedDevice);
            _filling = false;
        }

        private static FlowLayoutPanel Row(Control toggle, Control meter, int bottom)
        {
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Theme.Surface, Margin = new Padding(0, 0, 0, bottom) };
            row.Controls.Add(toggle);
            row.Controls.Add(meter);
            return row;
        }

        private void FitRow(Card card)
        {
            _row.FitTo(card.Body.ClientSize.Width);
            if (card.AutoHeight)
            {
                card.FitHeight();
            }
        }

        /// <summary>Preview on the left at 16:9, details on the right; stacks when the window is narrow.</summary>
        private sealed class SourceRow : Panel
        {
            private readonly Control _left;
            private readonly Control _right;

            public SourceRow(Control left, Control right)
            {
                _left = left;
                _right = right;
                BackColor = Theme.Surface;
                Controls.Add(left);
                Controls.Add(right);
            }

            public void FitTo(int width)
            {
                if (width <= 0)
                {
                    return;
                }

                var dpi = DeviceDpi / 96f * Theme.UiScale;
                int S(float v) => (int)(v * dpi);
                var gap = S(22);
                var stacked = width < S(560);
                var leftWidth = stacked ? Math.Min(width, S(420)) : Math.Min(S(330), Math.Max(S(220), (width - gap) * 44 / 100));
                var leftHeight = leftWidth * 9 / 16;
                var rightWidth = stacked ? width : width - leftWidth - gap;

                foreach (Control child in _right.Controls)
                {
                    if (child is FlowLayoutPanel flow && flow.AutoSize)
                    {
                        flow.MaximumSize = new Size(rightWidth, 0);
                    }
                }

                var rightHeight = _right.GetPreferredSize(new Size(rightWidth, 0)).Height;
                _left.Bounds = new Rectangle(0, 0, leftWidth, leftHeight);
                _right.Bounds = stacked
                    ? new Rectangle(0, leftHeight + S(14), rightWidth, rightHeight)
                    : new Rectangle(leftWidth + gap, 0, rightWidth, rightHeight);
                Size = new Size(width, stacked ? leftHeight + S(14) + rightHeight : Math.Max(leftHeight, rightHeight));
            }
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
