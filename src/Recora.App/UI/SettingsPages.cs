using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Recora.App.Audio;
using Recora.App.UI.Controls;
using Recora.Core.Audio;
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
            Segmented<int>? fps = null;
            Segmented<VideoCodec>? codec = null;
            Row(quality, "Chất lượng", Choice(settings.Quality, v =>
                {
                    settings.Quality = v;
                    if (v == QualityPreset.Saver)
                    {
                        // The two settings that save the most, set where the user can see (and undo) them.
                        settings.Codec = VideoCodec.Hevc;
                        settings.Fps = Math.Min(settings.Fps, 30);
                        codec!.Value = settings.Codec;
                        fps!.Value = settings.Fps;
                    }
                },
                (QualityPreset.High, "Rất nét"), (QualityPreset.Balanced, "Cân bằng"), (QualityPreset.Small, "Nhẹ"), (QualityPreset.Saver, "Tiết kiệm")),
                "Cân bằng: chữ và chuyển động sắc nét, file khoảng một nửa so với Rất nét. Tiết kiệm: file nhỏ nhất, chọn luôn H.265 và 30 khung/giây.");
            fps = Choice(settings.Fps, v => settings.Fps = v, (24, "24"), (30, "30"), (60, "60"), (120, "120"), (144, "144"));
            Row(quality, "Khung hình mỗi giây", fps, "60 cho game và thao tác mượt; 30 cho bài giảng, file nhỏ hơn.");
            Row(quality, "Độ phân giải", Choice(settings.Scale, v => settings.Scale = v,
                (OutputScale.Original, "Gốc"), (OutputScale.P1080, "1080p"), (OutputScale.P720, "720p"), (OutputScale.P480, "480p")),
                "Chỉ thu nhỏ, không bao giờ phóng to. Màn 2K, 4K ghi 1080p thì file nhỏ đi gần một nửa.");
            Row(quality, "Giới hạn dung lượng", Choice(settings.MaxGigabytesPerHour, v => settings.MaxGigabytesPerHour = v,
                (0, "Không"), (1, "1 GB/giờ"), (2, "2 GB/giờ"), (4, "4 GB/giờ"), (8, "8 GB/giờ")),
                "Mức trần cho cả hình lẫn tiếng: cảnh đứng yên vẫn nhỏ hơn nhiều, cảnh chuyển động nhanh bị ép cho vừa trần nên kém nét hơn.");

            var encoding = AddCard("mã hoá");
            codec = Choice(settings.Codec, v => settings.Codec = v,
                (VideoCodec.H264, "H.264"), (VideoCodec.Hevc, "H.265"), (VideoCodec.Av1, "AV1"));
            Row(encoding, "Codec", codec,
                "H.264 mở được ở mọi nơi; H.265 nhỏ hơn khoảng 30%; AV1 nhỏ nhất nhưng cần GPU đời mới. Máy không hỗ trợ thì Recora tự ghi H.264.");

            string Mark(EncoderKind kind, string name) => available(kind) ? name : name + " (không có)";
            Row(encoding, "Bộ mã hoá", Choice(settings.Encoder, v => settings.Encoder = v,
                (EncoderChoice.Auto, "Tự động"),
                (EncoderChoice.Nvenc, Mark(EncoderKind.Nvenc, "NVIDIA")),
                (EncoderChoice.Amf, Mark(EncoderKind.Amf, "AMD")),
                (EncoderChoice.Qsv, Mark(EncoderKind.Qsv, "Intel")),
                (EncoderChoice.X264, "CPU")),
                (autoEncoder == null ? string.Empty : "Tự động trên máy này: " + autoEncoder + ". ")
                + "Ghi bằng GPU nhẹ hơn CPU rất nhiều, nhất là với game. CPU (x264) chỉ nên dùng khi máy không có GPU nào ghi được.");
            encoding.Body.Controls.Add(Toggle("Ưu tiên FPS game khi ghi bằng CPU (720p, 30 fps)", settings.LightOnCpu, v => settings.LightOnCpu = v));
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
            Row(mic, "Micro", Devices(microphones, settings.MicrophoneDevice, id => settings.MicrophoneDevice = id),
                "Tai nghe Bluetooth: ghi micro của tai nghe làm nó chuyển sang chế độ gọi, tiếng game nhỏ và rè. Để Mặc định thì Recora tự dùng micro khác nếu máy có.");
            Row(mic, "Âm lượng", Volume(settings.MicrophoneVolume, v => settings.MicrophoneVolume = v));

            var test = AddCard("thử loa và micro");
            TestMicrophone = new FlatButton("Thu thử micro 5 giây") { Primary = true, Size = new Size(200, 34), Margin = new Padding(0, 0, 10, 0) };
            TestSpeaker = new FlatButton("Phát tiếng thử trái, phải") { Size = new Size(220, 34), Margin = Padding.Empty };
            var testButtons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Theme.Surface, Margin = new Padding(0, 0, 0, 10) };
            testButtons.Controls.AddRange(new Control[] { TestMicrophone, TestSpeaker });
            test.Body.Controls.Add(testButtons);
            TestLevel = new LevelMeter { Size = new Size(420, 30), Margin = new Padding(0, 0, 0, 6) };
            test.Body.Controls.Add(TestLevel);
            _testStatus = Caption(
                "Thu thử: nói vài câu như khi đang chơi, Recora phát lại đúng tiếng sẽ có trong video (đã qua chỉnh micro bên dưới).",
                Theme.Small, Theme.Muted, 0);
            test.Body.Controls.Add(_testStatus);

            var chain = settings.MicProcessing;
            var tune = AddCard("chỉnh micro");
            var gainLabel = Caption(string.Empty, Theme.Small, Theme.Muted, 14);
            void ShowGain() => gainLabel.Text = chain.GainDb == 0 ? "Không tăng. Kéo lên nếu micro tai nghe quá nhỏ." : $"+{chain.GainDb:0} dB";
            Row(tune, "Tăng âm micro", Slider(0, (int)MicProcessingSettings.MaxGainDb, (int)chain.GainDb, v =>
            {
                chain.GainDb = v;
                ShowGain();
            }));
            tune.Body.Controls.Add(gainLabel);
            ShowGain();
            tune.Body.Controls.Add(Toggle("Lọc tiếng ù, tiếng gõ bàn (dưới 80 Hz)", chain.HighPass, v => chain.HighPass = v));
            tune.Body.Controls.Add(Toggle("Chặn ồn nền khi không nói (quạt, bàn phím)", chain.NoiseGate, v => chain.NoiseGate = v));
            var gateLabel = Caption(string.Empty, Theme.Small, Theme.Muted, 14);
            void ShowGate() => gateLabel.Text = $"Ngưỡng {chain.GateThresholdDb:0} dB: tiếng nhỏ hơn bị chặn. Mất chữ đầu câu thì kéo sang trái.";
            var gate = Slider((int)MicProcessingSettings.MinGateThresholdDb, (int)MicProcessingSettings.MaxGateThresholdDb, (int)chain.GateThresholdDb, v =>
            {
                chain.GateThresholdDb = v;
                ShowGate();
            });
            gate.Margin = new Padding(0, 0, 0, 4);
            tune.Body.Controls.Add(gate);
            tune.Body.Controls.Add(gateLabel);
            ShowGate();
            tune.Body.Controls.Add(Toggle("Nén tiếng cho đều (nói nhỏ và hét gần bằng nhau)", chain.Compressor, v => chain.Compressor = v));
            tune.Body.Controls.Add(Toggle("Chống rè khi nói to", chain.Limiter, v => chain.Limiter = v));

            var tracks = AddCard("track");
            tracks.Body.Controls.Add(Toggle("Tách tiếng máy và micro thành hai track riêng", settings.SeparateTracks, v => settings.SeparateTracks = v));
            tracks.Body.Controls.Add(Caption("Để chỉnh riêng tiếng game và giọng nói khi dựng video. Trình phát thường chỉ phát track đầu.", Theme.Small, Theme.Muted));
        }

        private readonly Label _testStatus;

        public FlatButton TestMicrophone { get; }

        public FlatButton TestSpeaker { get; }

        public LevelMeter TestLevel { get; }

        public void ShowTest(string text, bool running)
        {
            _testStatus.Text = text;
            TestMicrophone.Enabled = !running;
            TestSpeaker.Enabled = !running;
            RefitCards();
        }

        private TrackBar Slider(int minimum, int maximum, int value, Action<int> apply)
        {
            var bar = new TrackBar
            {
                TickStyle = TickStyle.None,
                Minimum = minimum,
                Maximum = maximum,
                TickFrequency = 5,
                SmallChange = 1,
                LargeChange = 5,
                Value = Math.Max(minimum, Math.Min(maximum, value)),
                Width = 420,
                BackColor = Theme.Surface,
            };
            bar.ValueChanged += (_, _) =>
            {
                apply(bar.Value);
                Notify();
            };
            return bar;
        }

        private ComboBox Devices(IReadOnlyList<AudioDeviceOption> devices, string selectedId, Action<string> apply)
        {
            var items = new List<AudioDeviceOption> { new AudioDeviceOption(string.Empty, "Mặc định của Windows") };
            items.AddRange(devices);
            var combo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Surface,
                ForeColor = Theme.Text,
                Font = Theme.Body,
                Width = 420,
                DisplayMember = nameof(AudioDeviceOption.Name),
            };

            // Not DataSource: it stays empty until the combo has a BindingContext, which can be after its handle exists.
            combo.Items.AddRange(items.Cast<object>().ToArray());
            combo.SelectedIndex = Math.Max(0, items.FindIndex(d => d.Id == selectedId));
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
                TickStyle = TickStyle.None,
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
            Row(card, "Chụp ảnh màn hình", Box(nameof(settings.HotkeyScreenshot), settings.HotkeyScreenshot, Hotkey.Screenshot, v => settings.HotkeyScreenshot = v));
            Row(card, "Lưu khoảnh khắc vừa qua", Box(nameof(settings.HotkeyReplay), settings.HotkeyReplay, Hotkey.SaveReplay, v => settings.HotkeyReplay = v),
                "Phím chạy cả khi đang ở trong game. Ô đỏ nghĩa là app khác đã giữ phím đó (Alt+F10 trùng NVIDIA ShadowPlay nếu đang bật): chọn phím khác.");

            var reset = new FlatButton("Khôi phục mặc định") { Size = new Size(180, 34) };
            reset.Click += (_, _) =>
            {
                settings.HotkeyRecord = Hotkey.Record.ToString();
                settings.HotkeyPause = Hotkey.Pause.ToString();
                settings.HotkeyScreenshot = Hotkey.Screenshot.ToString();
                settings.HotkeyReplay = Hotkey.SaveReplay.ToString();
                _boxes[nameof(settings.HotkeyReplay)].Hotkey = Hotkey.SaveReplay;
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
            _folder = Caption(outputFolder, Theme.Body, Theme.Text, 10);
            folder.Body.Controls.Add(_folder);
            var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Theme.Surface, Margin = Padding.Empty };
            ChangeFolder = new FlatButton("Đổi thư mục…") { Size = new Size(150, 34), Margin = new Padding(0, 0, 10, 0) };
            OpenFolder = new FlatButton("Mở thư mục") { Size = new Size(130, 34), Margin = Padding.Empty };
            buttons.Controls.Add(ChangeFolder);
            buttons.Controls.Add(OpenFolder);
            folder.Body.Controls.Add(buttons);

            var look = AddCard("giao diện");
            Row(look, "Màu", Choice(settings.Theme, v => settings.Theme = v,
                (ThemeMode.System, "Theo Windows"), (ThemeMode.Light, "Sáng"), (ThemeMode.Dark, "Tối")),
                "Theo Windows: tự đổi sáng, tối cùng cài đặt màu của Windows.");

            var behavior = AddCard("khi chạy");
            behavior.Body.Controls.Add(Toggle("Thu nhỏ xuống khay khi đóng cửa sổ", settings.MinimizeToTrayOnClose, v => settings.MinimizeToTrayOnClose = v));
            behavior.Body.Controls.Add(Toggle("Mở Recora cùng Windows", settings.StartWithWindows, v => settings.StartWithWindows = v));
            behavior.Body.Controls.Add(Toggle("Ẩn cửa sổ Recora khỏi video", settings.HideRecoraFromVideo, v => settings.HideRecoraFromVideo = v));

            var recording = AddCard("khi ghi");
            recording.Body.Controls.Add(Toggle("Hiện thanh đang ghi trên màn hình", settings.RecordingOverlay, v => settings.RecordingOverlay = v));
            Row(recording, "Vị trí thanh đang ghi", Choice(settings.OverlayCorner, v => settings.OverlayCorner = v,
                (OverlayCorner.TopLeft, "Trên trái"), (OverlayCorner.TopRight, "Trên phải"), (OverlayCorner.BottomLeft, "Dưới trái"), (OverlayCorner.BottomRight, "Dưới phải")),
                "Thanh có đồng hồ, dung lượng, nút tạm dừng và dừng; kéo để đổi chỗ. Không bao giờ có trong video. Game toàn màn hình độc quyền (exclusive fullscreen) che mất thanh, chuyển game sang Borderless để thấy.");
            recording.Body.Controls.Add(Toggle("Đếm ngược 3 giây trước khi ghi", settings.Countdown, v => settings.Countdown = v));
            recording.Body.Controls.Add(Toggle("Hiện vòng tròn chỗ bấm chuột", settings.ClickHighlight, v => settings.ClickHighlight = v));
            recording.Body.Controls.Add(Toggle("Webcam nổi ở góc màn hình khi ghi", settings.WebcamOverlay, v => settings.WebcamOverlay = v));
            recording.Body.Controls.Add(Caption("Webcam nổi dùng webcam chọn ở chế độ Webcam; kéo để đổi chỗ. Vòng tròn và webcam nổi có trong video.", Theme.Small, Theme.Muted, 12));
            Row(recording, "Tự dừng sau", Choice(settings.AutoStopMinutes, v => settings.AutoStopMinutes = v,
                (0, "Không"), (5, "5 phút"), (10, "10 phút"), (30, "30 phút"), (60, "1 giờ"), (120, "2 giờ")),
                "Hữu ích khi ghi một buổi học hay trận đấu rồi để máy chạy.");
        }

        public FlatButton ChangeFolder { get; }

        public FlatButton OpenFolder { get; }

        public void ShowFolder(string path) => _folder.Text = path;
    }
}
