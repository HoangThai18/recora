using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Recora.App.Audio;
using Recora.App.UI.Controls;
using Recora.Core.Encoding;
using Recora.Core.Hotkeys;
using Recora.Core.Recording;
using Recora.Core.Settings;

namespace Recora.App.UI
{
    /// <summary>A page that edits <see cref="AppSettings"/> in place and raises <see cref="Changed"/> after every edit.</summary>
    internal abstract class SettingsPage : Page
    {
        protected SettingsPage(string title, AppSettings settings)
            : base(title)
        {
            Settings = settings;
        }

        public event EventHandler? Changed;

        protected AppSettings Settings { get; }

        protected void Notify() => Changed?.Invoke(this, EventArgs.Empty);

        /// <summary>A label, the control, and an optional muted hint, stacked inside a card.</summary>
        protected void Row(Card card, string label, Control control, string? hint = null)
        {
            card.Body.Controls.Add(Caption(label, Theme.BodyBold, Theme.Text, 6));
            control.Margin = new Padding(0, 0, 0, hint == null ? 14 : 4);
            card.Body.Controls.Add(control);
            if (hint != null)
            {
                card.Body.Controls.Add(Caption(hint, Theme.Small, Theme.Muted, 14));
            }
        }

        protected Segmented<T> Choice<T>(T value, Action<T> apply, params (T Value, string Label)[] options)
        {
            var control = new Segmented<T>(options, value) { Width = 420 };
            control.SelectedChanged += (_, _) =>
            {
                apply(control.Value);
                Notify();
            };
            return control;
        }

        protected ToggleSwitch Toggle(string text, bool value, Action<bool> apply)
        {
            var toggle = new ToggleSwitch(text, value) { Size = new Size(420, 30), Margin = new Padding(0, 0, 0, 8) };
            toggle.CheckedChanged += (_, _) =>
            {
                apply(toggle.Checked);
                Notify();
            };
            return toggle;
        }
    }

    internal sealed class VideoPage : SettingsPage
    {
        public VideoPage(AppSettings settings, Func<EncoderKind, bool> available, string? autoEncoder)
            : base("Video", settings)
        {
            var quality = AddCard("chất lượng");
            Row(quality, "Chất lượng", Choice(settings.Quality, v => settings.Quality = v,
                (QualityPreset.High, "Rất nét"), (QualityPreset.Balanced, "Cân bằng"), (QualityPreset.Small, "Nhẹ")),
                "Cân bằng: chữ và chuyển động sắc nét, file khoảng một nửa so với Rất nét.");
            Row(quality, "Khung hình mỗi giây", Choice(settings.Fps, v => settings.Fps = v,
                (24, "24"), (30, "30"), (60, "60"), (120, "120"), (144, "144")),
                "60 cho game và thao tác mượt; 30 cho bài giảng, file nhỏ hơn.");
            Row(quality, "Độ phân giải", Choice(settings.Scale, v => settings.Scale = v,
                (OutputScale.Original, "Gốc"), (OutputScale.P1080, "1080p"), (OutputScale.P720, "720p"), (OutputScale.P480, "480p")),
                "Chỉ thu nhỏ, không bao giờ phóng to.");

            var encoding = AddCard("mã hoá");
            Row(encoding, "Codec", Choice(settings.Codec, v => settings.Codec = v,
                (VideoCodec.H264, "H.264"), (VideoCodec.Hevc, "H.265"), (VideoCodec.Av1, "AV1")),
                "H.264 mở được ở mọi nơi; H.265 nhỏ hơn khoảng 30%; AV1 nhỏ nhất nhưng cần GPU đời mới. Máy không hỗ trợ thì Recora tự ghi H.264.");

            string Mark(EncoderKind kind, string name) => available(kind) ? name : name + " (không có)";
            Row(encoding, "Bộ mã hoá", Choice(settings.Encoder, v => settings.Encoder = v,
                (EncoderChoice.Auto, "Tự động"),
                (EncoderChoice.Nvenc, Mark(EncoderKind.Nvenc, "NVIDIA")),
                (EncoderChoice.Amf, Mark(EncoderKind.Amf, "AMD")),
                (EncoderChoice.Qsv, Mark(EncoderKind.Qsv, "Intel")),
                (EncoderChoice.X264, "CPU")),
                (autoEncoder == null ? string.Empty : "Tự động trên máy này: " + autoEncoder + ". ")
                + "Ghi bằng GPU không làm tụt FPS game. CPU (x264) chỉ nên dùng khi máy không có GPU nào ghi được.");
            Row(encoding, "Định dạng file", Choice(settings.Container, v => settings.Container = v,
                (ContainerFormat.Mp4, "MP4"), (ContainerFormat.Mkv, "MKV")),
                "MP4 mở được trên điện thoại, mạng xã hội, trình chỉnh sửa video.");

            var cursor = AddCard("con trỏ chuột");
            cursor.Body.Controls.Add(Toggle("Ghi con trỏ chuột", settings.DrawCursor, v => settings.DrawCursor = v));
        }
    }

    internal sealed class AudioPage : SettingsPage
    {
        public AudioPage(AppSettings settings, IReadOnlyList<AudioDeviceOption> speakers, IReadOnlyList<AudioDeviceOption> microphones)
            : base("Âm thanh", settings)
        {
            var system = AddCard("tiếng máy");
            system.Body.Controls.Add(Toggle("Ghi tiếng máy (loa, tai nghe)", settings.SystemAudio, v => settings.SystemAudio = v));
            Row(system, "Thiết bị phát", Devices(speakers, settings.SystemAudioDevice, id => settings.SystemAudioDevice = id));
            Row(system, "Âm lượng", Volume(settings.SystemVolume, v => settings.SystemVolume = v));

            var mic = AddCard("micro");
            mic.Body.Controls.Add(Toggle("Ghi micro", settings.Microphone, v => settings.Microphone = v));
            Row(mic, "Micro", Devices(microphones, settings.MicrophoneDevice, id => settings.MicrophoneDevice = id));
            Row(mic, "Âm lượng", Volume(settings.MicrophoneVolume, v => settings.MicrophoneVolume = v));

            var tracks = AddCard("track");
            tracks.Body.Controls.Add(Toggle("Tách tiếng máy và micro thành hai track riêng", settings.SeparateTracks, v => settings.SeparateTracks = v));
            tracks.Body.Controls.Add(Caption("Để chỉnh riêng tiếng game và giọng nói khi dựng video. Trình phát thường chỉ phát track đầu.", Theme.Small, Theme.Muted));
        }

        private ComboBox Devices(IReadOnlyList<AudioDeviceOption> devices, string selectedId, Action<string> apply)
        {
            var items = new List<AudioDeviceOption> { new AudioDeviceOption(string.Empty, "Mặc định của Windows") };
            items.AddRange(devices);
            var combo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.System,
                Font = Theme.Body,
                Width = 420,
                DisplayMember = nameof(AudioDeviceOption.Name),
                DataSource = items,
            };
            combo.HandleCreated += (_, _) => combo.SelectedIndex = Math.Max(0, items.FindIndex(d => d.Id == selectedId));
            combo.SelectionChangeCommitted += (_, _) =>
            {
                apply(((AudioDeviceOption)combo.SelectedItem).Id);
                Notify();
            };
            return combo;
        }

        private TrackBar Volume(float value, Action<float> apply)
        {
            var bar = new TrackBar
            {
                Minimum = 0,
                Maximum = 200,
                TickFrequency = 25,
                SmallChange = 5,
                LargeChange = 25,
                Value = (int)Math.Round(value * 100),
                Width = 420,
                BackColor = Theme.Surface,
            };
            bar.ValueChanged += (_, _) =>
            {
                apply(bar.Value / 100f);
                Notify();
            };
            return bar;
        }
    }

    internal sealed class HotkeysPage : SettingsPage
    {
        private readonly Dictionary<string, HotkeyBox> _boxes = new Dictionary<string, HotkeyBox>();

        public HotkeysPage(AppSettings settings)
            : base("Phím tắt", settings)
        {
            var card = AddCard("phím tắt toàn cục");
            Row(card, "Bắt đầu / dừng ghi", Box(nameof(settings.HotkeyRecord), settings.HotkeyRecord, Hotkey.Record, v => settings.HotkeyRecord = v));
            Row(card, "Tạm dừng / ghi tiếp", Box(nameof(settings.HotkeyPause), settings.HotkeyPause, Hotkey.Pause, v => settings.HotkeyPause = v));
            Row(card, "Chụp ảnh màn hình", Box(nameof(settings.HotkeyScreenshot), settings.HotkeyScreenshot, Hotkey.Screenshot, v => settings.HotkeyScreenshot = v),
                "Phím chạy cả khi đang ở trong game. Ô đỏ nghĩa là app khác đã giữ phím đó: chọn phím khác.");

            var reset = new FlatButton("Khôi phục mặc định") { Size = new Size(180, 34) };
            reset.Click += (_, _) =>
            {
                settings.HotkeyRecord = Hotkey.Record.ToString();
                settings.HotkeyPause = Hotkey.Pause.ToString();
                settings.HotkeyScreenshot = Hotkey.Screenshot.ToString();
                _boxes[nameof(settings.HotkeyRecord)].Hotkey = Hotkey.Record;
                _boxes[nameof(settings.HotkeyPause)].Hotkey = Hotkey.Pause;
                _boxes[nameof(settings.HotkeyScreenshot)].Hotkey = Hotkey.Screenshot;
                Notify();
            };
            card.Body.Controls.Add(reset);
        }

        public void ShowConflicts(IEnumerable<string> settingNames)
        {
            var names = new HashSet<string>(settingNames);
            foreach (var pair in _boxes)
            {
                pair.Value.Conflict = names.Contains(pair.Key);
            }
        }

        private HotkeyBox Box(string name, string current, Hotkey fallback, Action<string> apply)
        {
            var box = new HotkeyBox(Hotkey.TryParse(current, out var parsed) ? parsed : fallback);
            box.HotkeyChanged += (_, _) =>
            {
                apply(box.Hotkey.ToString());
                Notify();
            };
            _boxes[name] = box;
            return box;
        }
    }

    internal sealed class ScreenshotPage : SettingsPage
    {
        public ScreenshotPage(AppSettings settings)
            : base("Ảnh chụp", settings)
        {
            var card = AddCard("chụp ảnh màn hình");
            Row(card, "Định dạng", Choice(settings.ScreenshotFormat, v => settings.ScreenshotFormat = v,
                (ScreenshotFormat.Png, "PNG"), (ScreenshotFormat.Jpg, "JPG")),
                "PNG giữ nét từng điểm ảnh, hợp chụp chữ và giao diện; JPG nhẹ hơn, hợp ảnh game và phim.");
            card.Body.Controls.Add(Toggle("Chép ảnh vào clipboard để dán ngay", settings.ScreenshotToClipboard, v => settings.ScreenshotToClipboard = v));
            card.Body.Controls.Add(Toggle("Phát tiếng khi chụp", settings.ScreenshotSound, v => settings.ScreenshotSound = v));
            card.Body.Controls.Add(Caption(
                "Chụp đúng thứ chế độ hiện tại sẽ ghi: cả màn hình, một vùng (kéo chuột chọn) hay một cửa sổ (bấm vào nó). Ảnh lưu cạnh video và hiện trong Thư viện.",
                Theme.Small, Theme.Muted));
        }
    }

    internal sealed class GeneralPage : SettingsPage
    {
        private readonly Label _folder;

        public GeneralPage(AppSettings settings, string outputFolder)
            : base("Chung", settings)
        {
            var folder = AddCard("thư mục lưu");
            _folder = Caption(outputFolder, Theme.Mono, Theme.Text, 10);
            folder.Body.Controls.Add(_folder);
            var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Theme.Surface, Margin = Padding.Empty };
            ChangeFolder = new FlatButton("Đổi thư mục…") { Size = new Size(150, 34), Margin = new Padding(0, 0, 10, 0) };
            OpenFolder = new FlatButton("Mở thư mục") { Size = new Size(130, 34) };
            buttons.Controls.Add(ChangeFolder);
            buttons.Controls.Add(OpenFolder);
            folder.Body.Controls.Add(buttons);

            var behavior = AddCard("khi chạy");
            behavior.Body.Controls.Add(Toggle("Thu nhỏ xuống khay khi đóng cửa sổ", settings.MinimizeToTrayOnClose, v => settings.MinimizeToTrayOnClose = v));
            behavior.Body.Controls.Add(Toggle("Mở Recora cùng Windows", settings.StartWithWindows, v => settings.StartWithWindows = v));
            behavior.Body.Controls.Add(Toggle("Ẩn cửa sổ Recora khỏi video", settings.HideRecoraFromVideo, v => settings.HideRecoraFromVideo = v));

            var recording = AddCard("khi ghi");
            recording.Body.Controls.Add(Toggle("Đếm ngược 3 giây trước khi ghi", settings.Countdown, v => settings.Countdown = v));
            Row(recording, "Tự dừng sau", Choice(settings.AutoStopMinutes, v => settings.AutoStopMinutes = v,
                (0, "Không"), (5, "5 phút"), (10, "10 phút"), (30, "30 phút"), (60, "1 giờ"), (120, "2 giờ")),
                "Hữu ích khi ghi một buổi học hay trận đấu rồi để máy chạy.");
        }

        public FlatButton ChangeFolder { get; }

        public FlatButton OpenFolder { get; }

        public void ShowFolder(string path) => _folder.Text = path;
    }
}
