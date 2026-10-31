using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.CoreAudioApi;
using Recora.App.Audio;
using Recora.App.Logging;
using Recora.App.Native;
using Recora.App.Platform;
using Recora.App.Recording;
using Recora.App.UI;
using Recora.App.UI.Controls;
using Recora.Core;
using Recora.Core.Capture;
using Recora.Core.Encoding;
using Recora.Core.Hotkeys;
using Recora.Core.Library;
using Recora.Core.Recording;
using Recora.Core.Settings;

namespace Recora.App
{
    /// <summary>
    /// Wires the window, tray, hotkeys and recorder together. Owns the settings and saves them after every change;
    /// a recording reads them once, when it starts. Everything here runs on the UI thread.
    /// </summary>
    internal sealed class MainController : IDisposable
    {
        private static readonly TimeSpan DiskCheckInterval = TimeSpan.FromSeconds(5);

        private readonly MainForm _form;
        private readonly AppSettings _settings;
        private readonly string _settingsPath;
        private readonly TrayIcon _tray;
        private readonly HotkeyManager _hotkeys;
        private readonly Timer _timer;
        private readonly Timer _saveTimer;
        private readonly Timer _liveTimer;
        private readonly List<WasapiSource> _audio = new List<WasapiSource>();
        private CaptureEnvironment? _environment;
        private EncoderMemory? _memory;
        private Recorder? _recorder;
        private DateTime _lastDiskCheck;
        private SavedRecording? _lastSaved;
        private string? _lastError;
        private bool _exiting;
        private bool _syncingHome;
        private bool _toldAboutTray;
        private bool _startWithWindows;
        private bool _disposed;
        private int _liveTicks;
        private MMDevice? _speakerMeter;
        private MMDevice? _microphoneMeter;
        private DateTime _meterRetryAt;

        public MainController(MainForm form, AppSettings settings, string settingsPath)
        {
            _form = form;
            _settings = settings;
            _settingsPath = settingsPath;
            _startWithWindows = settings.StartWithWindows;
            Context = new ApplicationContext();

            _tray = new TrayIcon();
            _tray.OpenClicked += (_, _) => ShowWindow();
            _tray.RecordClicked += (_, _) => ToggleRecord();
            _tray.PauseClicked += (_, _) => TogglePause();
            _tray.FolderClicked += (_, _) => OpenOutputFolder();
            _tray.ExitClicked += (_, _) => RequestExit();
            _tray.BalloonClicked += (_, _) => RevealLastSaved();

            _hotkeys = new HotkeyManager();
            _hotkeys.Pressed += OnHotkey;

            _timer = new Timer { Interval = 500 };
            _timer.Tick += (_, _) => Tick();

            // Sliders fire on every step; one write after the user lets go is enough.
            _saveTimer = new Timer { Interval = 400 };
            _saveTimer.Tick += (_, _) => SaveSettings();

            // Home page life: sound levels ten times a second, the screen preview once a second, disk space every 5 s.
            _liveTimer = new Timer { Interval = 100 };
            _liveTimer.Tick += (_, _) => LiveTick();

            _form.RecordClicked += (_, _) => ToggleRecord();
            _form.PauseClicked += (_, _) => TogglePause();
            _form.ScreenshotClicked += (_, _) => Screenshot();
            _form.ModeSelected += (_, mode) => OnModeSelected(mode);
            _form.ShowRequested += (_, _) => ShowWindow();
            _form.FormClosing += OnFormClosing;
            _form.HandleCreated += (_, _) => ApplyCaptureExclusion();
            _form.Home.MonitorSelected += (_, device) =>
            {
                _settings.MonitorDevice = device;
                SettingsChanged();
            };
            _form.Home.SystemAudio.CheckedChanged += (_, _) => HomeAudioChanged();
            _form.Home.Microphone.CheckedChanged += (_, _) => HomeAudioChanged();
        }

        public ApplicationContext Context { get; }

        private string OutputFolder => _settings.OutputFolder;

        private bool Busy => _recorder != null && _recorder.State != SessionState.Idle && _recorder.State != SessionState.Failed;

        /// <summary>Shows the window (unless started with Windows), then detects GPUs and encoders in the background.</summary>
        public async void Start(bool minimized)
        {
            // The handle must exist even while hidden: a second launch reaches us through a window message.
            _ = _form.Handle;
            if (!minimized)
            {
                _form.Show();
            }

            _form.Record.Enabled = false;
            _form.Status.SetState("Đang kiểm tra máy…", Theme.Pause);
            _form.Status.RightText = OutputFolder;
            ApplyHotkeys();
            UpdateDisk();
            _liveTimer.Start();

            try
            {
                _environment = await Task.Run(() => CaptureEnvironment.Detect());
            }
            catch (Exception error)
            {
                Log.Error("environment detection failed", error);
                _form.Status.SetState("Không chạy được", Theme.Record);
                _form.Status.Details = Explain(error is RecoraException known ? known.Code : "unexpected");
                ShowMessage(_form.Status.Details, MessageBoxIcon.Error);
                return;
            }

            _memory = new EncoderMemory(_settings, _environment.Fingerprint);
            _recorder = new Recorder(_environment, _memory);
            _recorder.Changed += status =>
            {
                if (!_form.IsDisposed && _form.IsHandleCreated)
                {
                    _form.BeginInvoke(new Action(() => OnRecorderChanged(status)));
                }
            };
            BuildPages();
            RefreshHome();

            // Leftovers are listed before recording can start, so a new session's folder is never taken for one.
            var leftovers = RecoveryService.Scan(OutputFolder);
            _form.Record.Enabled = true;
            ShowStatus();
            SaveSettings();

            var ffmpeg = _environment.FfmpegPath;
            var recovered = await Task.Run(() => RecoveryService.Finish(ffmpeg, leftovers));
            if (recovered.Count > 0)
            {
                _lastSaved = new SavedRecording(recovered[recovered.Count - 1], TimeSpan.Zero, 0);
                _tray.Notify("Đã cứu bản ghi dở", $"{recovered.Count} video bị ngắt giữa chừng lần trước đã được lưu vào thư mục video.");
                _form.PageOf<LibraryPage>(PageKind.Library)?.Reload();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _saveTimer.Dispose();
            _liveTimer.Dispose();
            _timer.Dispose();
            ResetMeters();
            _hotkeys.Dispose();
            _tray.Dispose();
            CloseAudio();
        }

        private void BuildPages()
        {
            var environment = _environment!;
            var auto = AutoEncoder();
            var video = new VideoPage(_settings, kind => Available(environment, kind), auto.HasValue ? EncoderCatalog.DisplayName(auto.Value.Kind) : null);
            video.Changed += (_, _) => SettingsChanged();
            _form.SetPage(PageKind.Video, video);

            _form.SetPage(PageKind.Audio, BuildAudioPage());

            var hotkeys = new HotkeysPage(_settings);
            hotkeys.Changed += (_, _) =>
            {
                ApplyHotkeys();
                SettingsChanged();
            };
            _form.SetPage(PageKind.Hotkeys, hotkeys);
            ApplyHotkeys();

            var general = new GeneralPage(_settings, OutputFolder);
            general.Changed += (_, _) => SettingsChanged();
            general.ChangeFolder.Click += (_, _) => ChooseOutputFolder(general);
            general.OpenFolder.Click += (_, _) => OpenOutputFolder();
            _form.SetPage(PageKind.General, general);

            _form.SetPage(PageKind.Library, new LibraryPage(() => LibraryFiles.Scan(OutputFolder)));
            _form.SetPage(PageKind.About, new AboutPage(Program.Version, environment.FfmpegVersion));
        }

        private AudioPage BuildAudioPage()
        {
            var page = new AudioPage(_settings, AudioDevices.Speakers(), AudioDevices.Microphones());
            page.Changed += (_, _) => SettingsChanged();
            return page;
        }

        private void SettingsChanged()
        {
            _saveTimer.Stop();
            _saveTimer.Start();
            ResetMeters();
            UpdateDisk();
            RefreshHome();
            ApplyCaptureExclusion();

            if (_settings.StartWithWindows != _startWithWindows)
            {
                _startWithWindows = _settings.StartWithWindows;
                try
                {
                    StartupRegistration.Apply(_startWithWindows);
                }
                catch (Exception error) when (error is UnauthorizedAccessException || error is System.Security.SecurityException || error is IOException)
                {
                    Log.Warn("could not change the Run key: " + error.Message);
                }
            }
        }

        private void SaveSettings()
        {
            _saveTimer.Stop();
            try
            {
                SettingsStore.Save(_settingsPath, _settings);
                if (_memory != null)
                {
                    _memory.Dirty = false;
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Log.Warn("settings not saved: " + error.Message);
            }
        }

        private void HomeAudioChanged()
        {
            if (_syncingHome)
            {
                return;
            }

            _settings.SystemAudio = _form.Home.SystemAudio.Checked;
            _settings.Microphone = _form.Home.Microphone.Checked;
            _form.Home.SystemLevel.Active = _settings.SystemAudio;
            _form.Home.MicrophoneLevel.Active = _settings.Microphone;
            SaveSettings();
            if (_environment != null)
            {
                _form.SetPage(PageKind.Audio, BuildAudioPage());
            }
        }

        private void RefreshHome()
        {
            var hotkeys = new[] { _settings.HotkeyRecord, _settings.HotkeyPause, _settings.HotkeyScreenshot };
            _form.Home.SetHints(hotkeys[0], hotkeys[1], hotkeys[2]);
            _form.Record.Shortcut = _settings.HotkeyRecord;
            _form.Status.RightText = OutputFolder;

            _syncingHome = true;
            try
            {
                var environment = _environment;
                if (environment == null)
                {
                    _form.Home.Display(new HomeModel("Toàn màn hình", "Đang tìm màn hình…", "đang kiểm tra GPU và bộ mã hoá…", VideoPills(), _settings.SystemAudio, _settings.Microphone));
                    return;
                }

                var monitor = environment.MonitorFor(_settings.MonitorDevice);
                var bounds = monitor.Target.Bounds;
                var encoder = AutoEncoder();
                var encoderText = encoder == null
                    ? "không có bộ mã hoá nào dùng được"
                    : $"{EncoderCatalog.DisplayName(encoder.Value.Kind)} · {CodecName(encoder.Value.Codec)} · {(encoder.Value.IsHardware ? "ghi bằng GPU" : "ghi bằng CPU")}";

                _form.Home.ShowMonitors(
                    environment.Monitors.Select((m, i) => new MonitorTile(
                        m.Target.DeviceName,
                        i + 1,
                        new System.Drawing.Rectangle(m.Target.Bounds.X, m.Target.Bounds.Y, m.Target.Bounds.Width, m.Target.Bounds.Height),
                        m.Target.IsPrimary)).ToList(),
                    monitor.Target.DeviceName);
                _form.Home.Display(new HomeModel(
                    "Toàn màn hình",
                    $"{monitor.Name} · {bounds.Width}×{bounds.Height} · {_settings.Fps} khung/giây",
                    encoderText,
                    VideoPills(),
                    _settings.SystemAudio,
                    _settings.Microphone));
            }
            finally
            {
                _syncingHome = false;
            }
        }

        private IReadOnlyList<string> VideoPills()
        {
            var quality = _settings.Quality switch
            {
                QualityPreset.High => "Rất nét",
                QualityPreset.Small => "Nhẹ",
                _ => "Cân bằng",
            };
            var scale = _settings.Scale switch
            {
                OutputScale.P1080 => "1080p",
                OutputScale.P720 => "720p",
                OutputScale.P480 => "480p",
                _ => "độ phân giải gốc",
            };
            return new[] { quality, CodecName(_settings.Codec), _settings.Fps + " fps", _settings.Container.ToString().ToUpperInvariant(), scale, _settings.DrawCursor ? "có chuột" : "ẩn chuột" };
        }

        private static string CodecName(VideoCodec codec) => codec switch
        {
            VideoCodec.Hevc => "H.265",
            VideoCodec.Av1 => "AV1",
            _ => "H.264",
        };

        /// <summary>The encoder the next recording will try first on the chosen screen.</summary>
        private VideoEncoder? AutoEncoder()
        {
            var environment = _environment;
            if (environment == null || _memory == null)
            {
                return null;
            }

            var monitor = environment.MonitorFor(_settings.MonitorDevice);
            var ordered = _memory.Order(EncoderCatalog.Candidates(monitor.Target.Vendor, _settings.Codec, environment.Encoders), Preferred());
            return ordered.Count > 0 ? ordered[0] : (VideoEncoder?)null;
        }

        private EncoderKind? Preferred() => _settings.Encoder switch
        {
            EncoderChoice.Nvenc => EncoderKind.Nvenc,
            EncoderChoice.Amf => EncoderKind.Amf,
            EncoderChoice.Qsv => EncoderKind.Qsv,
            EncoderChoice.X264 => EncoderKind.X264,
            _ => null,
        };

        private static bool Available(CaptureEnvironment environment, EncoderKind kind) =>
            Enum.GetValues(typeof(VideoCodec)).Cast<VideoCodec>()
                .Select(codec => EncoderCatalog.FfmpegName(kind, codec))
                .Any(name => name != null && environment.Encoders.Contains(name))
            && environment.Monitors.Any(m => KindFits(kind, m.Target.Vendor));

        private static bool KindFits(EncoderKind kind, GpuVendor vendor) => kind switch
        {
            EncoderKind.Nvenc => vendor == GpuVendor.Nvidia,
            EncoderKind.Amf => vendor == GpuVendor.Amd,
            EncoderKind.Qsv => vendor == GpuVendor.Intel,
            _ => true,
        };

        private void ApplyHotkeys()
        {
            var keys = new Dictionary<HotkeyAction, string>
            {
                [HotkeyAction.Record] = _settings.HotkeyRecord,
                [HotkeyAction.Pause] = _settings.HotkeyPause,
                [HotkeyAction.Screenshot] = _settings.HotkeyScreenshot,
            };
            var failed = _hotkeys.Apply(keys);
            _form.PageOf<HotkeysPage>(PageKind.Hotkeys)?.ShowConflicts(failed.Select(action => action switch
            {
                HotkeyAction.Record => nameof(AppSettings.HotkeyRecord),
                HotkeyAction.Pause => nameof(AppSettings.HotkeyPause),
                _ => nameof(AppSettings.HotkeyScreenshot),
            }));

            if (failed.Count > 0)
            {
                Log.Warn("hotkeys not registered: " + string.Join(", ", failed));
            }
        }

        private void OnHotkey(HotkeyAction action)
        {
            switch (action)
            {
                case HotkeyAction.Record:
                    ToggleRecord();
                    break;
                case HotkeyAction.Pause:
                    TogglePause();
                    break;
                case HotkeyAction.Screenshot:
                    Screenshot();
                    break;
            }
        }

        private void OnModeSelected(CaptureModeChoice mode)
        {
            if (mode != CaptureModeChoice.FullScreen)
            {
                _form.SelectMode(CaptureModeChoice.FullScreen);
                ShowMessage("Chế độ này đang được hoàn thiện và sẽ có trong bản cập nhật tới. Hiện Recora ghi toàn màn hình.", MessageBoxIcon.Information);
            }
        }

        private void Screenshot() =>
            _tray.Notify("Chụp ảnh", "Chụp ảnh màn hình sẽ có trong bản cập nhật tới.");

        private async void ToggleRecord()
        {
            var recorder = _recorder;
            if (recorder == null)
            {
                _tray.Notify("Recora chưa sẵn sàng", "Recora đang kiểm tra GPU và bộ mã hoá, thử lại sau vài giây.");
                return;
            }

            switch (recorder.State)
            {
                case SessionState.Idle:
                case SessionState.Failed:
                    await StartRecording(recorder);
                    break;
                case SessionState.Recording:
                case SessionState.Paused:
                    await StopRecording(recorder);
                    break;

                // Starting and Finalizing take a second or two; a second press then is ignored rather than racing them.
            }
        }

        private async void TogglePause()
        {
            var recorder = _recorder;
            if (recorder == null)
            {
                return;
            }

            if (recorder.State == SessionState.Recording)
            {
                await recorder.PauseAsync();
            }
            else if (recorder.State == SessionState.Paused)
            {
                await recorder.ResumeAsync();
            }
        }

        private async Task StartRecording(Recorder recorder)
        {
            _lastError = null;
            _lastSaved = null;
            var environment = _environment!;
            var monitor = environment.MonitorFor(_settings.MonitorDevice);

            // WASAPI objects are opened off the UI thread: NAudio's capture thread then shares the apartment.
            var tracks = await Task.Run(() => OpenAudio());
            var request = new RecordingRequest(
                new MonitorSource(monitor.Target),
                _settings.Codec,
                Preferred(),
                _settings.Quality,
                _settings.Fps,
                _settings.Scale,
                _settings.DrawCursor,
                _settings.Container,
                OutputFolder,
                tracks);

            _timer.Start();
            await recorder.StartAsync(request);
            if (recorder.State != SessionState.Recording)
            {
                CloseAudio();
            }

            if (_memory != null && _memory.Dirty)
            {
                SaveSettings();
                RefreshHome();
            }
        }

        private async Task StopRecording(Recorder recorder)
        {
            var saved = await recorder.StopAsync();
            CloseAudio();
            if (saved == null)
            {
                return;
            }

            _lastSaved = saved;
            _form.PageOf<LibraryPage>(PageKind.Library)?.Reload();
            _tray.Notify("Đã lưu video", $"{Path.GetFileName(saved.Path)} · {LibraryIndex.FormatDuration(saved.Duration)} · {LibraryIndex.FormatBytes(saved.Bytes)}");
        }

        private IReadOnlyList<AudioTrackSpec> OpenAudio()
        {
            var inputs = new List<(string Name, IAudioInput Input, float Gain)>();
            if (_settings.SystemAudio)
            {
                var source = OpenSource(DataFlow.Render, _settings.SystemAudioDevice, "tiếng máy");
                if (source != null)
                {
                    inputs.Add(("Tiếng máy", source.Input, _settings.SystemVolume));
                }
            }

            if (_settings.Microphone)
            {
                var source = OpenSource(DataFlow.Capture, _settings.MicrophoneDevice, "micro");
                if (source != null)
                {
                    inputs.Add(("Micro", source.Input, _settings.MicrophoneVolume));
                }
            }

            if (inputs.Count == 0)
            {
                return new AudioTrackSpec[0];
            }

            if (_settings.SeparateTracks && inputs.Count == 2)
            {
                return inputs.Select(i => new AudioTrackSpec(i.Name, new[] { (i.Input, i.Gain) })).ToList();
            }

            return new[] { new AudioTrackSpec("Âm thanh", inputs.Select(i => (i.Input, i.Gain)).ToList()) };
        }

        private WasapiSource? OpenSource(DataFlow flow, string deviceId, string what)
        {
            var device = AudioDevices.Resolve(flow, deviceId);
            if (device == null)
            {
                Log.Warn($"no {what} device; recording without it");
                return null;
            }

            try
            {
                var source = flow == DataFlow.Render ? WasapiSource.Loopback(device) : WasapiSource.Microphone(device);
                source.Start();
                lock (_audio)
                {
                    _audio.Add(source);
                }

                return source;
            }
            catch (Exception error) when (error is COMException || error is InvalidOperationException || error is ArgumentException)
            {
                Log.Error($"could not open {what} {device.FriendlyName}", error);
                _form.BeginInvoke(new Action(() => _tray.Notify("Không mở được " + what, $"{device.FriendlyName} đang bận hoặc bị tắt. Video vẫn ghi, chỉ thiếu {what}.", ToolTipIcon.Warning)));
                return null;
            }
        }

        private void CloseAudio()
        {
            WasapiSource[] sources;
            lock (_audio)
            {
                sources = _audio.ToArray();
                _audio.Clear();
            }

            foreach (var source in sources)
            {
                try
                {
                    source.Dispose();
                }
                catch (Exception error) when (error is COMException || error is InvalidOperationException)
                {
                    Log.Warn("audio source close failed: " + error.Message);
                }
            }
        }

        private void OnRecorderChanged(RecorderStatus status)
        {
            // A failed resume stops and saves inside the recorder; remember that file too.
            if (status.OutputPath != null && _lastSaved?.Path != status.OutputPath)
            {
                _lastSaved = new SavedRecording(status.OutputPath, TimeSpan.Zero, 0);
            }

            if (status.ErrorCode != null)
            {
                _lastError = Explain(status.ErrorCode);
                if (_form.Visible)
                {
                    ShowMessage(_lastError, MessageBoxIcon.Warning);
                }
                else
                {
                    _tray.Notify("Không ghi được", _lastError, ToolTipIcon.Warning);
                }
            }

            if (status.State == SessionState.Idle || status.State == SessionState.Failed)
            {
                _timer.Stop();
            }

            ShowStatus();
        }

        private void Tick()
        {
            ShowStatus();
            var recorder = _recorder;
            if (recorder == null || recorder.State != SessionState.Recording || DateTime.UtcNow - _lastDiskCheck < DiskCheckInterval)
            {
                return;
            }

            _lastDiskCheck = DateTime.UtcNow;
            try
            {
                var free = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(OutputFolder))!).AvailableFreeSpace;
                if (DiskGuard.ShouldStop(free))
                {
                    Log.Warn($"disk almost full ({free} bytes), stopping");
                    _tray.Notify("Ổ đĩa sắp đầy", "Còn dưới 500 MB trống nên Recora đã dừng và lưu video.", ToolTipIcon.Warning);
                    _ = StopRecording(recorder);
                }
            }
            catch (IOException error)
            {
                Log.Warn("free space check failed: " + error.Message);
            }
        }

        private void ShowStatus()
        {
            var status = _recorder?.Status();
            var state = status?.State ?? SessionState.Idle;
            var clock = status == null ? string.Empty : Clock(status.Elapsed);

            switch (state)
            {
                case SessionState.Starting:
                    _form.Status.SetState("Đang mở bộ ghi…", Theme.Pause);
                    _form.Status.Details = string.Empty;
                    break;
                case SessionState.Recording:
                    _form.Status.SetState("Đang ghi " + clock, Theme.Record);
                    _form.Status.Details = $"{LibraryIndex.FormatBytes(status!.Bytes)} · {status.Fps:0} fps · {status.Encoder}";
                    break;
                case SessionState.Paused:
                    _form.Status.SetState("Tạm dừng " + clock, Theme.Pause);
                    _form.Status.Details = $"{LibraryIndex.FormatBytes(status!.Bytes)} · {_settings.HotkeyPause} để ghi tiếp";
                    break;
                case SessionState.Finalizing:
                    _form.Status.SetState("Đang lưu video…", Theme.Pause);
                    _form.Status.Details = string.Empty;
                    break;
                case SessionState.Failed:
                    _form.Status.SetState("Không ghi được", Theme.Record);
                    _form.Status.Details = _lastError ?? string.Empty;
                    break;
                default:
                    _form.Status.SetState("Sẵn sàng", Theme.Ok);
                    _form.Status.Details = _lastSaved != null
                        ? "Đã lưu " + Path.GetFileName(_lastSaved.Path)
                        : $"{_settings.HotkeyRecord} ghi · {_settings.HotkeyPause} tạm dừng · {_settings.HotkeyScreenshot} chụp ảnh";
                    break;
            }

            var recording = state == SessionState.Recording || state == SessionState.Starting;
            var busy = recording || state == SessionState.Paused || state == SessionState.Finalizing;
            var details = status == null || state == SessionState.Starting ? "đang mở bộ ghi…"
                : state == SessionState.Finalizing ? "đang lưu video…"
                : $"{LibraryIndex.FormatBytes(status.Bytes)} · {status.Fps:0} fps · {status.Encoder}";
            _form.Home.Preview.ShowRecording(busy, state == SessionState.Paused, string.IsNullOrEmpty(clock) ? "00:00:00" : clock, details);
            _form.Record.State = state == SessionState.Paused ? RecordState.Paused : recording ? RecordState.Recording : RecordState.Idle;
            _form.Record.Enabled = state != SessionState.Finalizing && _recorder != null;
            _form.Pause.Enabled = state == SessionState.Recording || state == SessionState.Paused;
            _form.Pause.IconKind = state == SessionState.Paused ? IconKind.Record : IconKind.Pause;
            _form.Pause.Text = (state == SessionState.Paused ? "Ghi tiếp" : "Tạm dừng") + " (" + _settings.HotkeyPause + ")";
            _form.Text = state == SessionState.Recording ? "Recora · đang ghi " + clock : state == SessionState.Paused ? "Recora · tạm dừng" : "Recora";
            _tray.Show(state, state == SessionState.Recording ? "Recora · đang ghi " + clock : state == SessionState.Paused ? "Recora · tạm dừng " + clock : "Recora");
        }

        private void LiveTick()
        {
            _liveTicks++;
            if (_liveTicks % 50 == 0)
            {
                UpdateDisk();
            }

            var homeVisible = _form.Visible && _form.WindowState != FormWindowState.Minimized && _form.CurrentPage == PageKind.Home;
            if (!homeVisible)
            {
                return;
            }

            UpdateMeters();

            // The preview is skipped while recording: the clock shows instead, and a game keeps every bit of GPU time.
            var idle = _recorder == null || _recorder.State == SessionState.Idle || _recorder.State == SessionState.Failed;
            if (idle && _environment != null && _liveTicks % 10 == 1)
            {
                var monitor = _environment.MonitorFor(_settings.MonitorDevice).Target.Bounds;
                var box = _form.Home.Preview.Size;
                var size = new System.Drawing.Size(Math.Max(1, box.Width), Math.Max(1, box.Width * monitor.Height / Math.Max(1, monitor.Width)));
                _form.Home.Preview.Image = ScreenPreview.Capture(new System.Drawing.Rectangle(monitor.X, monitor.Y, monitor.Width, monitor.Height), size);
            }
        }

        private void UpdateMeters()
        {
            if (_speakerMeter == null && _microphoneMeter == null && DateTime.UtcNow >= _meterRetryAt)
            {
                _meterRetryAt = DateTime.UtcNow.AddSeconds(3);
                _speakerMeter = _settings.SystemAudio ? AudioDevices.Resolve(DataFlow.Render, _settings.SystemAudioDevice) : null;
                _microphoneMeter = _settings.Microphone ? AudioDevices.Resolve(DataFlow.Capture, _settings.MicrophoneDevice) : null;
            }

            try
            {
                _form.Home.SystemLevel.Push(_speakerMeter?.AudioMeterInformation.MasterPeakValue ?? 0f);

                // A microphone only reports a level while some app captures it: Recora while recording, a call app, etc.
                _form.Home.MicrophoneLevel.Push(_microphoneMeter?.AudioMeterInformation.MasterPeakValue ?? 0f);
            }
            catch (COMException)
            {
                ResetMeters();
            }
        }

        private void ResetMeters()
        {
            _speakerMeter?.Dispose();
            _microphoneMeter?.Dispose();
            _speakerMeter = null;
            _microphoneMeter = null;
            _meterRetryAt = DateTime.MinValue;
        }

        private void UpdateDisk()
        {
            try
            {
                var root = Path.GetPathRoot(Path.GetFullPath(OutputFolder));
                if (string.IsNullOrEmpty(root))
                {
                    return;
                }

                var drive = new DriveInfo(root);
                var free = drive.AvailableFreeSpace;
                var used = drive.TotalSize <= 0 ? 0f : 1f - (float)free / drive.TotalSize;
                _form.Disk.SetUsage($"Ổ {root.TrimEnd('\\')} còn {LibraryIndex.FormatBytes(free)}", used, free < 5L * 1024 * 1024 * 1024);
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                _form.Disk.SetUsage("Không đọc được ổ lưu", 0, low: true);
            }
        }

        private static string Clock(TimeSpan elapsed) =>
            $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";

        private static string Explain(string code) => code switch
        {
            "disk_full" => "Ổ đĩa lưu video còn dưới 500 MB trống. Dọn bớt hoặc đổi thư mục lưu ở trang Chung.",
            "no_working_encoder" => "Không mở được bộ ghi nào trên máy này. Thử cập nhật driver card màn hình, hoặc chọn bộ mã hoá CPU ở trang Video.",
            "finalize_failed" => "Không ghép được file video cuối. Các đoạn đã ghi vẫn được giữ, Recora sẽ thử lưu lại khi mở lần sau.",
            "ffmpeg_missing" => "Thiếu ffmpeg.exe cạnh Recora.exe. Hãy tải lại Recora bản đầy đủ từ thaipro.store.",
            "region_too_small" => "Vùng ghi quá nhỏ, cần ít nhất 160×90 điểm ảnh.",
            _ => "Có lỗi không mong muốn. Xem nhật ký ở trang Giới thiệu và gửi kèm khi báo lỗi.",
        };

        private void ChooseOutputFolder(GeneralPage page)
        {
            using (var dialog = new FolderBrowserDialog { Description = "Chọn thư mục lưu video và ảnh chụp", SelectedPath = OutputFolder, ShowNewFolderButton = true })
            {
                if (dialog.ShowDialog(_form) != DialogResult.OK || string.IsNullOrEmpty(dialog.SelectedPath))
                {
                    return;
                }

                _settings.OutputFolder = dialog.SelectedPath;
                page.ShowFolder(dialog.SelectedPath);
                SettingsChanged();
                _form.PageOf<LibraryPage>(PageKind.Library)?.Reload();
            }
        }

        private void OpenOutputFolder()
        {
            Directory.CreateDirectory(OutputFolder);
            Process.Start(new ProcessStartInfo(OutputFolder) { UseShellExecute = true });
        }

        private void RevealLastSaved()
        {
            var saved = _lastSaved;
            if (saved != null && File.Exists(saved.Path))
            {
                Process.Start("explorer.exe", $"/select,\"{saved.Path}\"");
            }
        }

        private void ApplyCaptureExclusion()
        {
            if (!_form.IsHandleCreated)
            {
                return;
            }

            // Windows 10 2004+; older versions refuse the flag and Recora's window simply stays visible in videos.
            if (!NativeMethods.SetWindowDisplayAffinity(_form.Handle, _settings.HideRecoraFromVideo ? NativeMethods.WdaExcludeFromCapture : NativeMethods.WdaNone))
            {
                Log.Warn($"SetWindowDisplayAffinity failed: {Marshal.GetLastWin32Error()}");
            }
        }

        private void ShowWindow()
        {
            if (!_form.Visible)
            {
                _form.Show();
            }

            if (_form.WindowState == FormWindowState.Minimized)
            {
                _form.WindowState = FormWindowState.Normal;
            }

            _form.Activate();
            _form.BringToFront();
        }

        private void ShowMessage(string text, MessageBoxIcon icon)
        {
            if (_form.Visible)
            {
                MessageBox.Show(_form, text, "Recora", MessageBoxButtons.OK, icon);
            }
            else
            {
                _tray.Notify("Recora", text, icon == MessageBoxIcon.Error || icon == MessageBoxIcon.Warning ? ToolTipIcon.Warning : ToolTipIcon.Info);
            }
        }

        private async void OnFormClosing(object? sender, FormClosingEventArgs e)
        {
            if (_exiting)
            {
                return;
            }

            if (e.CloseReason == CloseReason.WindowsShutDown || e.CloseReason == CloseReason.TaskManagerClosing)
            {
                // No time to remux: FFmpeg dies with us and the segments are finished at the next start (BR-01).
                Log.Warn("closing for " + e.CloseReason + (Busy ? " while recording; segments left for recovery" : string.Empty));
                Exit(closeForm: false);
                return;
            }

            if (e.CloseReason == CloseReason.UserClosing && _settings.MinimizeToTrayOnClose)
            {
                e.Cancel = true;
                _form.Hide();
                if (!_toldAboutTray)
                {
                    _toldAboutTray = true;
                    _tray.Notify("Recora vẫn đang chạy", Busy ? "Vẫn đang ghi. Bấm biểu tượng dưới khay hoặc " + _settings.HotkeyRecord + " để dừng." : "Bấm " + _settings.HotkeyRecord + " để ghi, hoặc bấm biểu tượng dưới khay để mở lại.");
                }

                return;
            }

            e.Cancel = true;
            await ExitAfterStopping();
        }

        private async void RequestExit() => await ExitAfterStopping();

        private async Task ExitAfterStopping()
        {
            var recorder = _recorder;
            if (recorder != null && Busy)
            {
                ShowWindow();
                var answer = MessageBox.Show(_form, "Recora đang ghi. Dừng, lưu video rồi thoát?", "Recora", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                if (answer != DialogResult.OK)
                {
                    return;
                }

                await StopRecording(recorder);
            }

            Exit();
        }

        private void Exit(bool closeForm = true)
        {
            _exiting = true;
            SaveSettings();
            Dispose();
            if (closeForm)
            {
                _form.Close();
            }

            Context.ExitThread();
        }
    }
}
