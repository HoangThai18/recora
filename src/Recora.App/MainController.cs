using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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
using Recora.Core.Ffmpeg;
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
        private readonly List<WasapiSource> _replayAudio = new List<WasapiSource>();
        private readonly Timer _replayRestart;
        private ReplayBuffer? _replay;
        private bool _replayBusy;
        private bool _replaySaving;
        private string? _replaySignature;
        private DateTime _replayRetryAt;
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
        private IReadOnlyList<string> _videoDevices = new string[0];
        private RegionFrame? _frame;
        private ClickHighlighter? _clicks;
        private WebcamOverlay? _overlay;
        private bool _activeAudioOnly;
        private bool _preparing;
        private Countdown? _countdown;

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

            // Settings that change what the replay buffer captures restart it once the user stops clicking.
            _replayRestart = new Timer { Interval = 1500 };
            _replayRestart.Tick += async (_, _) =>
            {
                _replayRestart.Stop();
                if (_replay != null && _replay.IsRunning && _replaySignature != ReplaySignature())
                {
                    await StopReplay();
                    ResumeReplay();
                }
            };

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
            _form.Home.DeviceSelected += (_, device) =>
            {
                _settings.VideoDevice = device;
                SettingsChanged();
            };
            _form.Home.AudioFormats.SelectedChanged += (_, _) =>
            {
                _settings.AudioFormat = _form.Home.AudioFormats.Value;
                SettingsChanged();
            };
            _form.Home.Replay.CheckedChanged += async (_, _) =>
            {
                if (_syncingHome)
                {
                    return;
                }

                _settings.ReplayEnabled = _form.Home.Replay.Checked;
                SettingsChanged();
                if (_settings.ReplayEnabled)
                {
                    ResumeReplay();
                }
                else
                {
                    await StopReplay();
                    ShowStatus();
                }
            };
            _form.Home.ReplayLength.SelectedChanged += (_, _) =>
            {
                _settings.ReplaySeconds = _form.Home.ReplayLength.Value;
                SettingsChanged();
            };
            _form.Home.SaveReplay.Click += (_, _) => SaveReplay();
            _form.SelectMode(ToChoice(_settings.Mode));
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
            _replay = new ReplayBuffer(_environment, _memory);
            _recorder.Changed += status =>
            {
                if (!_form.IsDisposed && _form.IsHandleCreated)
                {
                    _form.BeginInvoke(new Action(() => OnRecorderChanged(status)));
                }
            };
            BuildPages();
            if (_form.Mode == CaptureModeChoice.Device || _settings.WebcamOverlay)
            {
                RefreshDevices();
            }

            RefreshHome();

            // Leftovers are listed before recording can start, so a new session's folder is never taken for one.
            var leftovers = RecoveryService.Scan(OutputFolder);
            ReplayBuffer.CleanStale(OutputFolder);
            _form.Record.Enabled = true;
            ShowStatus();
            SaveSettings();
            ResumeReplay();

            CheckForUpdateLater();
            var ffmpeg = _environment.FfmpegPath;
            var result = await Task.Run(() => RecoveryService.Finish(ffmpeg, leftovers));
            var recovered = result.Saved;
            if (result.NeedSpace > 0)
            {
                var saved = recovered.Count > 0 ? $"Đã cứu {recovered.Count} video. " : string.Empty;
                _tray.Notify("Còn bản ghi chưa lưu được", $"{saved}{result.NeedSpace} video ghi dở lần trước cần thêm chỗ trống để lưu. Dọn bớt ổ đĩa rồi mở lại Recora.", ToolTipIcon.Warning);
            }
            else if (recovered.Count > 0)
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
            CloseFrame();
            CloseExtras();
            _replayRestart.Dispose();
            _replay?.Stop();
            CloseAudio(_replayAudio);
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

            var screenshots = new ScreenshotPage(_settings);
            screenshots.Changed += (_, _) => SettingsChanged();
            _form.SetPage(PageKind.Screenshot, screenshots);

            _form.SetPage(PageKind.Library, new LibraryPage(() => LibraryFiles.Scan(OutputFolder)));
            var about = new AboutPage(Program.Version, environment.FfmpegVersion);
            about.CheckUpdate.Click += async (_, _) =>
            {
                about.CheckUpdate.Enabled = false;
                about.ShowUpdate("Đang hỏi…");
                var result = await UpdateService.CheckAsync();
                about.CheckUpdate.Enabled = true;
                about.ShowUpdate(result.Failed
                    ? "Không hỏi được trang phát hành, thử lại sau."
                    : result.Update == null ? "Bạn đang dùng bản mới nhất." : $"Có bản {result.Update.Version}: bấm Trang tải để tải về.");
            };
            _form.SetPage(PageKind.About, about);
        }

        private AudioPage BuildAudioPage()
        {
            var page = new AudioPage(_settings, AudioDevices.Speakers(), AudioDevices.Microphones());
            page.Changed += (_, _) => SettingsChanged();
            return page;
        }

        /// <summary>One quiet check a minute after start; a newer release shows as a tray note linking to the download page.</summary>
        private async void CheckForUpdateLater()
        {
            await Task.Delay(TimeSpan.FromMinutes(1));
            var update = (await UpdateService.CheckAsync()).Update;
            if (update != null && !_disposed)
            {
                _tray.Notify(
                    "Có Recora bản " + update.Version,
                    "Bấm vào đây để mở trang tải bản mới.",
                    ToolTipIcon.Info,
                    () => Process.Start(new ProcessStartInfo(AboutPage.Homepage) { UseShellExecute = true }));
            }
        }

        private void SettingsChanged()
        {
            _saveTimer.Stop();
            _saveTimer.Start();
            ResetMeters();
            UpdateDisk();
            RefreshHome();
            ApplyCaptureExclusion();
            if (_replay != null && _replay.IsRunning && _replaySignature != ReplaySignature())
            {
                _replayRestart.Stop();
                _replayRestart.Start();
            }

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
                var hotkey = _settings.HotkeyRecord;
                var mode = _form.Mode;
                var device = _videoDevices.Contains(_settings.VideoDevice) ? _settings.VideoDevice : _videoDevices.FirstOrDefault();
                var (title, detail) = mode switch
                {
                    CaptureModeChoice.Game => ("Game", $"Bấm {hotkey} trong game: Recora ghi đúng màn hình đang chơi · {_settings.Fps} khung/giây"),
                    CaptureModeChoice.Region => ("Chọn vùng", $"Bấm Ghi ({hotkey}) rồi kéo chuột chọn vùng, hoặc bấm vào một cửa sổ"),
                    CaptureModeChoice.Window => ("Cửa sổ", $"Bấm Ghi ({hotkey}) rồi bấm cửa sổ cần ghi; video bám theo cửa sổ dù bị che"),
                    CaptureModeChoice.Device => ("Webcam", device == null ? "Chưa tìm thấy webcam hay thẻ capture nào" : device + " · kèm micro nếu bật"),
                    CaptureModeChoice.AudioOnly => ("Âm thanh", "Chỉ ghi tiếng máy và micro, không ghi hình"),
                    _ => ("Toàn màn hình", $"{monitor.Name} · {bounds.Width}×{bounds.Height} · {_settings.Fps} khung/giây"),
                };

                _form.Home.ShowPicker(mode switch
                {
                    CaptureModeChoice.Device => HomePage.PickerKind.Devices,
                    CaptureModeChoice.AudioOnly => HomePage.PickerKind.AudioFormats,
                    CaptureModeChoice.Region or CaptureModeChoice.Window => HomePage.PickerKind.None,
                    _ => HomePage.PickerKind.Monitors,
                });
                _form.Home.ShowDevices(_videoDevices, _settings.VideoDevice);
                _form.Home.AudioFormats.Value = _settings.AudioFormat;
                _form.Home.Preview.SetPlaceholder(
                    mode == CaptureModeChoice.Device ? IconKind.Webcam : mode == CaptureModeChoice.AudioOnly ? IconKind.Audio : IconKind.Monitor,
                    mode == CaptureModeChoice.Device ? device : mode == CaptureModeChoice.AudioOnly ? "chỉ ghi âm thanh" : null);
                if (mode == CaptureModeChoice.Device || mode == CaptureModeChoice.AudioOnly)
                {
                    _form.Home.Preview.Image = null;
                }

                var audioOnly = mode == CaptureModeChoice.AudioOnly;
                _form.Home.Replay.Checked = _settings.ReplayEnabled;
                _form.Home.ReplayLength.Value = _settings.ReplaySeconds;
                ShowReplayState();
                _form.Home.Display(new HomeModel(
                    title,
                    detail,
                    audioOnly ? $"{_settings.AudioFormat.ToString().ToUpperInvariant()} · 48 kHz stereo" : encoderText,
                    audioOnly ? AudioPills() : VideoPills(),
                    _settings.SystemAudio,
                    _settings.Microphone));
            }
            finally
            {
                _syncingHome = false;
            }
        }

        private IReadOnlyList<string> AudioPills()
        {
            var bitrate = _settings.AudioFormat == AudioFormat.Wav ? "không nén" : AudioRecordingCommand.Codec(_settings.AudioFormat, _settings.Quality)[3].Replace("k", " kbps");
            var sources = _settings.SystemAudio && _settings.Microphone ? "tiếng máy + micro" : _settings.SystemAudio ? "tiếng máy" : _settings.Microphone ? "micro" : "chưa chọn nguồn";
            return new[] { _settings.AudioFormat.ToString().ToUpperInvariant(), bitrate, sources };
        }

        private IReadOnlyList<string> VideoPills()
        {
            var quality = _settings.Quality switch
            {
                QualityPreset.High => "Rất nét",
                QualityPreset.Small => "Nhẹ",
                QualityPreset.Saver => "Tiết kiệm",
                _ => "Cân bằng",
            };
            var scale = _settings.Scale switch
            {
                OutputScale.P1080 => "1080p",
                OutputScale.P720 => "720p",
                OutputScale.P480 => "480p",
                _ => "độ phân giải gốc",
            };
            var pills = new List<string> { quality, CodecName(_settings.Codec), _settings.Fps + " fps", _settings.Container.ToString().ToUpperInvariant(), scale, _settings.DrawCursor ? "có chuột" : "ẩn chuột" };
            if (_settings.MaxGigabytesPerHour > 0)
            {
                pills.Insert(1, $"tối đa {_settings.MaxGigabytesPerHour} GB/giờ");
            }

            return pills;
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
                [HotkeyAction.SaveReplay] = _settings.HotkeyReplay,
            };
            var failed = _hotkeys.Apply(keys);
            _form.PageOf<HotkeysPage>(PageKind.Hotkeys)?.ShowConflicts(failed.Select(action => action switch
            {
                HotkeyAction.Record => nameof(AppSettings.HotkeyRecord),
                HotkeyAction.Pause => nameof(AppSettings.HotkeyPause),
                HotkeyAction.SaveReplay => nameof(AppSettings.HotkeyReplay),
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
                case HotkeyAction.SaveReplay:
                    SaveReplay();
                    break;
            }
        }

        private void OnModeSelected(CaptureModeChoice mode)
        {
            _settings.Mode = ToSetting(mode);
            SettingsChanged();
            if (mode == CaptureModeChoice.Device)
            {
                RefreshDevices();
            }
        }

        private static CaptureMode ToSetting(CaptureModeChoice mode) => (CaptureMode)Enum.Parse(typeof(CaptureMode), mode.ToString());

        private static CaptureModeChoice ToChoice(CaptureMode mode) => (CaptureModeChoice)Enum.Parse(typeof(CaptureModeChoice), mode.ToString());

        /// <summary>Asks FFmpeg for the webcams and capture cards (a fraction of a second, off the UI thread).</summary>
        private async void RefreshDevices() => await RefreshDevicesAsync();

        private async Task RefreshDevicesAsync()
        {
            var environment = _environment;
            if (environment == null)
            {
                return;
            }

            try
            {
                _videoDevices = await Task.Run(() =>
                    DshowDevices.ParseVideo(FfmpegProcess.Run(environment.FfmpegPath, DshowDevices.ListCommand(), TimeSpan.FromSeconds(15)).Error));
                Log.Info("video devices: " + (_videoDevices.Count == 0 ? "none" : string.Join(", ", _videoDevices)));
            }
            catch (Exception error) when (error is IOException || error is InvalidOperationException || error is System.ComponentModel.Win32Exception)
            {
                Log.Warn("could not list video devices: " + error.Message);
                _videoDevices = new string[0];
            }

            RefreshHome();
        }

        /// <summary>F11: a still of what the current mode records, saved next to the videos.</summary>
        private async void Screenshot()
        {
            if (_environment == null || _preparing)
            {
                return;
            }

            _preparing = true;
            try
            {
                await TakeScreenshot();
            }
            finally
            {
                _preparing = false;
            }
        }

        private async Task TakeScreenshot()
        {

            var mode = _form.Mode;
            Rectangle area;
            if (mode == CaptureModeChoice.Region || mode == CaptureModeChoice.Window)
            {
                var pick = PickTarget(mode == CaptureModeChoice.Region ? PickKind.Region : PickKind.Window);
                if (pick == null)
                {
                    return;
                }

                area = pick.Bounds;

                // Let the overlays leave the screen before reading it.
                await Task.Delay(150);
            }
            else
            {
                var bounds = (mode == CaptureModeChoice.Game ? GameMonitor() : SelectedMonitor()).Target.Bounds;
                area = new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            }

            try
            {
                var shot = ScreenshotService.Take(area, OutputFolder, _settings.ScreenshotFormat, _settings.ScreenshotToClipboard, _settings.ScreenshotSound);
                _tray.Notify("Đã chụp ảnh", $"{Path.GetFileName(shot.Path)} · {shot.Size.Width}×{shot.Size.Height}{(shot.Copied ? " · đã chép vào clipboard" : string.Empty)}");
                _lastSaved = new SavedRecording(shot.Path, TimeSpan.Zero, new FileInfo(shot.Path).Length);
                _form.PageOf<LibraryPage>(PageKind.Library)?.Reload();
                ShowStatus();
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is System.ComponentModel.Win32Exception || error is ExternalException)
            {
                Log.Error("screenshot failed", error);
                ShowMessage("Không chụp được ảnh: " + error.Message, MessageBoxIcon.Warning);
            }
        }

        private MonitorInfo SelectedMonitor() => _environment!.MonitorFor(_settings.MonitorDevice);

        /// <summary>The screen of the window in front (the game), or the chosen screen when Recora itself is in front.</summary>
        private MonitorInfo GameMonitor()
        {
            var foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero || foreground == _form.Handle)
            {
                return SelectedMonitor();
            }

            return _environment!.MonitorFor(Screen.FromHandle(foreground).DeviceName);
        }

        private MonitorInfo MonitorAt(Rectangle area)
        {
            var center = new Point(area.X + area.Width / 2, area.Y + area.Height / 2);
            return _environment!.MonitorFor(Screen.FromPoint(center).DeviceName);
        }

        /// <summary>Shows the region/window picker with Recora's window out of the way.</summary>
        private PickResult? PickTarget(PickKind kind)
        {
            var wasVisible = _form.Visible && _form.WindowState != FormWindowState.Minimized;
            if (wasVisible)
            {
                _form.Hide();
            }

            try
            {
                return CapturePicker.Pick(kind, _form.Handle);
            }
            finally
            {
                if (wasVisible)
                {
                    _form.Show();
                }
            }
        }

        private async void ToggleRecord()
        {
            var recorder = _recorder;
            if (recorder == null)
            {
                _tray.Notify("Recora chưa sẵn sàng", "Recora đang kiểm tra GPU và bộ mã hoá, thử lại sau vài giây.");
                return;
            }

            // A second press during the countdown cancels it; during picking or opening it is ignored, never a
            // second recording started on top of the first.
            if (_countdown != null)
            {
                _countdown.Cancel();
                return;
            }

            if (_preparing)
            {
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
            _preparing = true;
            try
            {
                // One capture at a time: the replay buffer steps aside for the recording and comes back after it.
                await StopReplay();
                await PrepareAndStart(recorder);
            }
            finally
            {
                _preparing = false;
                _countdown = null;
                if (!Busy)
                {
                    ResumeReplay();
                }
            }
        }

        private async Task PrepareAndStart(Recorder recorder)
        {
            _lastError = null;
            _lastSaved = null;
            var mode = _form.Mode;
            var primary = _environment!.Monitors.FirstOrDefault(m => m.Target.IsPrimary) ?? SelectedMonitor();
            CaptureSource source;
            Rectangle? frame = null;
            AudioFormat? audioOnly = null;

            switch (mode)
            {
                case CaptureModeChoice.Game:
                    source = new MonitorSource(GameMonitor().Target);
                    break;

                case CaptureModeChoice.Region:
                case CaptureModeChoice.Window:
                    var pick = PickTarget(mode == CaptureModeChoice.Region ? PickKind.Region : PickKind.Window);
                    if (pick == null)
                    {
                        return;
                    }

                    var monitor = MonitorAt(pick.Bounds);
                    var rect = new PixelRect(pick.Bounds.X, pick.Bounds.Y, pick.Bounds.Width, pick.Bounds.Height);
                    if (mode == CaptureModeChoice.Window && pick.Window != IntPtr.Zero)
                    {
                        source = new WindowSource(pick.Window.ToInt64(), pick.WindowTitle ?? string.Empty, rect, monitor.Target);
                    }
                    else
                    {
                        source = new RegionSource(monitor.Target, rect);
                        var clipped = source.Area;
                        frame = new Rectangle(clipped.X, clipped.Y, clipped.Width, clipped.Height);
                    }

                    break;

                case CaptureModeChoice.Device:
                    if (_videoDevices.Count == 0)
                    {
                        RefreshDevices();
                        ShowMessage("Không tìm thấy webcam hay thiết bị ghi hình nào. Cắm webcam rồi chọn lại chế độ Webcam.", MessageBoxIcon.Information);
                        return;
                    }

                    var device = _videoDevices.Contains(_settings.VideoDevice) ? _settings.VideoDevice : _videoDevices[0];
                    source = new DeviceSource(device, new PixelRect(0, 0, 1920, 1080), primary.Target);
                    break;

                case CaptureModeChoice.AudioOnly:
                    if (!_settings.SystemAudio && !_settings.Microphone)
                    {
                        ShowMessage("Bật tiếng máy hoặc micro ở trang chủ để ghi âm thanh.", MessageBoxIcon.Information);
                        return;
                    }

                    source = new MonitorSource(primary.Target);
                    audioOnly = _settings.AudioFormat;
                    break;

                default:
                    source = new MonitorSource(SelectedMonitor().Target);
                    break;
            }

            if (_settings.Countdown && mode != CaptureModeChoice.AudioOnly)
            {
                var screen = source is DeviceSource ? SelectedMonitor().Target.Bounds : source.Monitor.Bounds;
                _countdown = Countdown.Begin(new Rectangle(screen.X, screen.Y, screen.Width, screen.Height));
                var ranOut = await _countdown.Done;
                _countdown = null;
                if (!ranOut)
                {
                    return;
                }
            }

            // WASAPI objects are opened off the UI thread: NAudio's capture thread then shares the apartment.
            var tracks = await Task.Run(() => OpenAudio(_audio, forceMixed: audioOnly != null));
            if (audioOnly != null && tracks.Count == 0)
            {
                CloseAudio();
                ShowMessage("Không mở được loa hay micro nào để ghi âm thanh.", MessageBoxIcon.Warning);
                return;
            }

            _activeAudioOnly = audioOnly != null;
            var request = Request(source, tracks, audioOnly);

            // Click rings and the floating webcam live on the screen, so they only reach the video in screen and
            // region captures; a window capture records that window alone.
            var screenModes = mode != CaptureModeChoice.Device && mode != CaptureModeChoice.AudioOnly && mode != CaptureModeChoice.Window;
            if (screenModes && _settings.WebcamOverlay && _videoDevices.Count == 0)
            {
                await RefreshDevicesAsync();
            }

            StartExtras(screenModes, source);
            _timer.Start();
            await recorder.StartAsync(request);
            if (recorder.State != SessionState.Recording)
            {
                CloseAudio();
                CloseExtras();
            }
            else if (frame.HasValue)
            {
                _frame = new RegionFrame(frame.Value);
                _frame.Show();
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
            CloseFrame();
            CloseExtras();
            ResumeReplay();
            if (saved == null)
            {
                return;
            }

            _lastSaved = saved;
            _form.PageOf<LibraryPage>(PageKind.Library)?.Reload();
            _tray.Notify("Đã lưu video", $"{Path.GetFileName(saved.Path)} · {LibraryIndex.FormatDuration(saved.Duration)} · {LibraryIndex.FormatBytes(saved.Bytes)}");
        }

        private RecordingRequest Request(CaptureSource source, IReadOnlyList<AudioTrackSpec> tracks, AudioFormat? audioOnly = null) =>
            new RecordingRequest(
                source,
                _settings.Codec,
                Preferred(),
                _settings.Quality,
                _settings.Fps,
                _settings.Scale,
                _settings.DrawCursor,
                _settings.Container,
                OutputFolder,
                tracks,
                audioOnly,
                _settings.MaxGigabytesPerHour);

        private IReadOnlyList<AudioTrackSpec> OpenAudio(List<WasapiSource> into, bool forceMixed = false)
        {
            var inputs = new List<(string Name, IAudioInput Input, float Gain)>();
            if (_settings.SystemAudio)
            {
                var source = OpenSource(DataFlow.Render, _settings.SystemAudioDevice, "tiếng máy", into);
                if (source != null)
                {
                    inputs.Add(("Tiếng máy", source.Input, _settings.SystemVolume));
                }
            }

            if (_settings.Microphone)
            {
                var source = OpenSource(DataFlow.Capture, _settings.MicrophoneDevice, "micro", into);
                if (source != null)
                {
                    inputs.Add(("Micro", source.Input, _settings.MicrophoneVolume));
                }
            }

            if (inputs.Count == 0)
            {
                return new AudioTrackSpec[0];
            }

            if (_settings.SeparateTracks && inputs.Count == 2 && !forceMixed)
            {
                return inputs.Select(i => new AudioTrackSpec(i.Name, new[] { (i.Input, i.Gain) })).ToList();
            }

            return new[] { new AudioTrackSpec("Âm thanh", inputs.Select(i => (i.Input, i.Gain)).ToList()) };
        }

        private WasapiSource? OpenSource(DataFlow flow, string deviceId, string what, List<WasapiSource> into)
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
                var name = device.FriendlyName;
                source.Lost += _ => _form.BeginInvoke(new Action(() =>
                    _tray.Notify("Mất " + what, $"{name} vừa bị rút hoặc tắt. Video vẫn ghi tiếp, phần sau không có {what}.", ToolTipIcon.Warning)));
                source.Start();
                lock (into)
                {
                    into.Add(source);
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

        /// <summary>Click rings and the floating webcam, shown before capture starts so they are in the first frames.</summary>
        private void StartExtras(bool screenMode, CaptureSource source)
        {
            CloseExtras();
            if (!screenMode)
            {
                return;
            }

            if (_settings.ClickHighlight)
            {
                _clicks = new ClickHighlighter();
            }

            var device = _videoDevices.Contains(_settings.VideoDevice) ? _settings.VideoDevice : _videoDevices.FirstOrDefault();
            if (_settings.WebcamOverlay && device == null)
            {
                _tray.Notify("Không có webcam", "Webcam nổi đang bật nhưng không tìm thấy webcam nào. Video vẫn ghi bình thường.", ToolTipIcon.Warning);
            }

            if (_settings.WebcamOverlay && device != null && _environment != null)
            {
                try
                {
                    // Inside the recorded area, or it would not be in the video.
                    var area = source.Area;
                    _overlay = new WebcamOverlay(_environment.FfmpegPath, device, new Rectangle(area.X, area.Y, area.Width, area.Height));
                    _overlay.Show();
                }
                catch (Exception error) when (error is System.ComponentModel.Win32Exception || error is InvalidOperationException)
                {
                    Log.Error("webcam overlay failed", error);
                    _overlay = null;
                }
            }
        }

        private void CloseExtras()
        {
            _clicks?.Dispose();
            _clicks = null;
            _overlay?.Close();
            _overlay?.Dispose();
            _overlay = null;
        }

        private void CloseFrame()
        {
            _frame?.Close();
            _frame?.Dispose();
            _frame = null;
        }

        private void CloseAudio() => CloseAudio(_audio);

        private static void CloseAudio(List<WasapiSource> list)
        {
            WasapiSource[] sources;
            lock (list)
            {
                sources = list.ToArray();
                list.Clear();
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

        /// <summary>
        /// Starts the replay buffer when it is wanted and the screen is free: at launch, after a recording, after a
        /// failed one, and when the user switches it on. Captures the chosen screen whatever the mode.
        /// </summary>
        private async void ResumeReplay()
        {
            var replay = _replay;
            if (replay == null || _disposed || _exiting || !_settings.ReplayEnabled || replay.IsRunning || _replayBusy || Busy || _preparing)
            {
                return;
            }

            _replayBusy = true;
            try
            {
                var source = new MonitorSource(SelectedMonitor().Target);
                var tracks = await Task.Run(() => OpenAudio(_replayAudio));
                var request = Request(source, tracks);
                var seconds = _settings.ReplaySeconds;
                _replaySignature = ReplaySignature();
                await Task.Run(() => replay.Start(request, seconds));
                if (_memory != null && _memory.Dirty)
                {
                    SaveSettings();
                }
            }
            catch (Exception error) when (error is RecoraException || error is IOException || error is UnauthorizedAccessException)
            {
                Log.Error("replay buffer failed to start", error);
                CloseAudio(_replayAudio);
                _replayRetryAt = DateTime.UtcNow.AddSeconds(30);
                _tray.Notify("Không giữ được khoảnh khắc", Explain(error is RecoraException known ? known.Code : "unexpected"), ToolTipIcon.Warning);
            }
            finally
            {
                _replayBusy = false;
            }

            // F12 may have been pressed while the buffer was opening: the recording wins.
            if (Busy || _preparing || !_settings.ReplayEnabled)
            {
                await StopReplay();
            }

            ShowStatus();
            ShowReplayState();
        }

        private async Task StopReplay()
        {
            var replay = _replay;
            if (replay == null)
            {
                return;
            }

            // A start or a save in progress finishes first; stopping under it would delete the chunks it reads.
            for (var i = 0; i < 150 && (_replayBusy || _replaySaving); i++)
            {
                await Task.Delay(100);
            }

            _replayRestart.Stop();
            if (replay.IsRunning || replay.HasDied)
            {
                _replayBusy = true;
                try
                {
                    await Task.Run(() => replay.Stop());
                }
                finally
                {
                    _replayBusy = false;
                }
            }

            CloseAudio(_replayAudio);
            ShowReplayState();
        }

        /// <summary>Alt+F10: the last N seconds become a video in the library; the buffer keeps running.</summary>
        private async void SaveReplay()
        {
            var replay = _replay;
            if (replay == null)
            {
                _tray.Notify("Recora chưa sẵn sàng", "Recora đang kiểm tra GPU và bộ mã hoá, thử lại sau vài giây.");
                return;
            }

            if (!replay.IsRunning)
            {
                if (Busy)
                {
                    _tray.Notify("Đang ghi", $"Recora đang ghi cả màn hình: bấm {_settings.HotkeyRecord} để dừng và lưu.");
                }
                else
                {
                    _tray.Notify("Lưu khoảnh khắc đang tắt", "Bật \"Giữ sẵn khoảnh khắc vừa qua\" ở Trang chủ, Recora sẽ giữ sẵn các giây vừa qua để lưu khi cần.");
                }

                return;
            }

            if (_replaySaving)
            {
                return;
            }

            _replaySaving = true;
            ShowReplayState();
            try
            {
                var folder = OutputFolder;
                var container = _settings.Container;
                var saved = await Task.Run(() => replay.Save(folder, container));
                if (saved == null)
                {
                    _tray.Notify("Chưa có gì để lưu", "Recora vừa mới bắt đầu giữ khoảnh khắc, đợi vài giây rồi bấm lại.");
                    return;
                }

                _lastSaved = saved;
                _form.PageOf<LibraryPage>(PageKind.Library)?.Reload();
                _tray.Notify("Đã lưu khoảnh khắc", $"{Path.GetFileName(saved.Path)} · {LibraryIndex.FormatDuration(saved.Duration)} · {LibraryIndex.FormatBytes(saved.Bytes)}");
            }
            catch (Exception error) when (error is RecoraException || error is IOException || error is UnauthorizedAccessException)
            {
                Log.Error("replay save failed", error);
                _tray.Notify("Không lưu được khoảnh khắc", Explain(error is RecoraException known ? known.Code : "unexpected"), ToolTipIcon.Warning);
            }
            finally
            {
                _replaySaving = false;
                ShowReplayState();
                ShowStatus();
            }
        }

        /// <summary>Once a second: restart a buffer whose FFmpeg died, stop it when the disk fills up.</summary>
        private async void WatchReplay()
        {
            var replay = _replay;
            if (replay == null || _replayBusy || _replaySaving)
            {
                return;
            }

            if (replay.IsRunning)
            {
                var free = Finalizer.FreeBytes(OutputFolder);
                if (free.HasValue && DiskGuard.ShouldStop(free.Value))
                {
                    await StopReplay();
                    _tray.Notify("Ổ đĩa sắp đầy", "Còn dưới 500 MB trống nên Recora đã ngừng giữ khoảnh khắc.", ToolTipIcon.Warning);
                    ShowStatus();
                }
            }
            else if (replay.HasDied)
            {
                Log.Warn("replay buffer's ffmpeg exited; restarting");
                await StopReplay();
                _replayRetryAt = DateTime.UtcNow.AddSeconds(10);
            }
            else if (_settings.ReplayEnabled && !Busy && !_preparing && DateTime.UtcNow >= _replayRetryAt && _replayRetryAt != DateTime.MinValue)
            {
                _replayRetryAt = DateTime.MinValue;
                ResumeReplay();
            }

            ShowReplayState();
        }

        private void ShowReplayState()
        {
            var replay = _replay;
            var length = ReplayLengthText(_settings.ReplaySeconds);
            var hint = $"Bấm {_settings.HotkeyReplay} (cả khi đang trong game) để lưu {length} vừa qua của màn hình đang chọn thành video. "
                + $"Bấm {_settings.HotkeyRecord} vẫn ghi thường như cũ.";
            string state;
            if (replay == null)
            {
                state = "> đang kiểm tra máy…";
            }
            else if (_replaySaving)
            {
                state = "> đang lưu khoảnh khắc…";
            }
            else if (replay.IsRunning)
            {
                var held = replay.Held;
                state = $"> đang giữ {(int)held.TotalMinutes}:{held.Seconds:00} / {length} · {replay.EncoderName}";
            }
            else if (Busy || _preparing)
            {
                state = "> tạm nghỉ trong lúc ghi thường";
            }
            else if (_replayBusy)
            {
                state = "> đang mở bộ ghi…";
            }
            else
            {
                state = _settings.ReplayEnabled ? "> chưa chạy được, Recora sẽ thử lại" : "> đang tắt";
            }

            _form.Home.ShowReplay(hint, state, replay != null && replay.IsRunning && !_replaySaving);
        }

        /// <summary>Everything that changes what the buffer records; a change restarts it.</summary>
        private string ReplaySignature() => string.Join("|", new object[]
        {
            _settings.MonitorDevice, _settings.Codec, _settings.Encoder, _settings.Quality, _settings.Fps, _settings.Scale,
            _settings.DrawCursor, _settings.MaxGigabytesPerHour, _settings.SystemAudio, _settings.SystemAudioDevice,
            _settings.SystemVolume, _settings.Microphone, _settings.MicrophoneDevice, _settings.MicrophoneVolume,
            _settings.SeparateTracks, _settings.OutputFolder, _settings.ReplaySeconds,
        });

        private static string ReplayLengthText(int seconds) => seconds < 60 ? seconds + " giây" : seconds / 60 + " phút";

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
                CloseFrame();
                CloseExtras();
                _timer.Stop();
            }

            // A recording that failed mid-way hands the screen back to the replay buffer.
            if (status.State == SessionState.Failed)
            {
                ResumeReplay();
            }

            ShowStatus();
        }

        private void Tick()
        {
            ShowStatus();
            var recorder = _recorder;
            if (recorder != null && recorder.State == SessionState.Recording && _settings.AutoStopMinutes > 0
                && recorder.Status().Elapsed >= TimeSpan.FromMinutes(_settings.AutoStopMinutes))
            {
                Log.Info($"auto-stop after {_settings.AutoStopMinutes} min");
                _tray.Notify("Đã tự dừng", $"Recora đã ghi đủ {_settings.AutoStopMinutes} phút theo cài đặt và lưu video.");
                _ = StopRecording(recorder);
                return;
            }

            if (recorder == null || recorder.State != SessionState.Recording || DateTime.UtcNow - _lastDiskCheck < DiskCheckInterval)
            {
                return;
            }

            _lastDiskCheck = DateTime.UtcNow;
            try
            {
                var free = Finalizer.FreeBytes(OutputFolder);
                if (free.HasValue && DiskGuard.ShouldStop(free.Value))
                {
                    Log.Warn($"disk almost full ({free} bytes), stopping");
                    _tray.Notify("Ổ đĩa sắp đầy", "Còn dưới 500 MB trống nên Recora đã dừng ghi. Các đoạn đã ghi được giữ lại.", ToolTipIcon.Warning);
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
                    _form.Status.Details = _activeAudioOnly
                        ? $"{LibraryIndex.FormatBytes(status!.Bytes)} · {status.Encoder}"
                        : $"{LibraryIndex.FormatBytes(status!.Bytes)} · {status.Fps:0} fps · {status.Encoder}";
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
                    if (_replay != null && _replay.IsRunning)
                    {
                        _form.Status.SetState("Đang giữ khoảnh khắc", Theme.Ok);
                        _form.Status.Details = $"{_settings.HotkeyReplay} lưu {ReplayLengthText(_settings.ReplaySeconds)} vừa qua · {_settings.HotkeyRecord} ghi thường";
                        break;
                    }

                    _form.Status.SetState("Sẵn sàng", Theme.Ok);
                    _form.Status.Details = _lastSaved != null
                        ? "Đã lưu " + Path.GetFileName(_lastSaved.Path)
                        : $"{_settings.HotkeyRecord} ghi · {_settings.HotkeyPause} tạm dừng · {_settings.HotkeyScreenshot} chụp ảnh";
                    break;
            }

            var recording = state == SessionState.Recording || state == SessionState.Starting;
            _frame?.SetRecording(state != SessionState.Paused);
            var busy = recording || state == SessionState.Paused || state == SessionState.Finalizing;
            var details = status == null || state == SessionState.Starting ? "đang mở bộ ghi…"
                : state == SessionState.Finalizing ? "đang lưu video…"
                : _activeAudioOnly ? $"{LibraryIndex.FormatBytes(status.Bytes)} · {status.Encoder}"
                : $"{LibraryIndex.FormatBytes(status.Bytes)} · {status.Fps:0} fps · {status.Encoder}";
            _form.Home.Preview.ShowRecording(busy, state == SessionState.Paused, string.IsNullOrEmpty(clock) ? "00:00:00" : clock, details);
            _form.Record.State = state == SessionState.Paused ? RecordState.Paused : recording ? RecordState.Recording : RecordState.Idle;
            _form.Record.Enabled = state != SessionState.Finalizing && _recorder != null;
            _form.Pause.Enabled = state == SessionState.Recording || state == SessionState.Paused;
            _form.Pause.IconKind = state == SessionState.Paused ? IconKind.Record : IconKind.Pause;
            _form.Pause.Text = (state == SessionState.Paused ? "Ghi tiếp" : "Tạm dừng") + " (" + _settings.HotkeyPause + ")";
            _form.Text = state == SessionState.Recording ? "Recora · đang ghi " + clock : state == SessionState.Paused ? "Recora · tạm dừng" : "Recora";
            var idleTip = _replay != null && _replay.IsRunning ? "Recora · đang giữ khoảnh khắc (" + _settings.HotkeyReplay + " để lưu)" : "Recora";
            _tray.Show(state, state == SessionState.Recording ? "Recora · đang ghi " + clock : state == SessionState.Paused ? "Recora · tạm dừng " + clock : idleTip);
        }

        private void LiveTick()
        {
            _liveTicks++;
            if (_liveTicks % 50 == 0)
            {
                UpdateDisk();
            }

            if (_liveTicks % 10 == 0)
            {
                WatchReplay();
            }

            var homeVisible = _form.Visible && _form.WindowState != FormWindowState.Minimized && _form.CurrentPage == PageKind.Home;
            if (!homeVisible)
            {
                return;
            }

            UpdateMeters();

            // The preview is skipped while recording: the clock shows instead, and a game keeps every bit of GPU time.
            var idle = _recorder == null || _recorder.State == SessionState.Idle || _recorder.State == SessionState.Failed;
            var screenMode = _form.Mode != CaptureModeChoice.Device && _form.Mode != CaptureModeChoice.AudioOnly;
            if (idle && screenMode && _environment != null && _liveTicks % 10 == 1)
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
            "finalize_no_space" => "Ổ đĩa không đủ chỗ để lưu video. Các đoạn đã ghi vẫn được giữ: dọn bớt ổ đĩa rồi mở lại Recora, video sẽ được lưu.",
            "finalize_failed" => "Không ghép được file video cuối. Các đoạn đã ghi vẫn được giữ, Recora sẽ thử lưu lại khi mở lần sau.",
            "ffmpeg_missing" => "Thiếu ffmpeg.exe cạnh Recora.exe. Hãy tải lại Recora bản đầy đủ từ thaipro.store.",
            "device_unavailable" => "Không mở được webcam: có thể app khác (Zalo, Zoom, trình duyệt) đang dùng nó. Tắt app đó rồi thử lại.",
            "audio_failed" => "Không ghi được âm thanh ở định dạng này. Thử M4A ở trang chủ chế độ Âm thanh.",
            "region_too_small" => "Vùng ghi quá nhỏ, cần ít nhất 160×90 điểm ảnh.",
            "replay_save_failed" => "Không ghép được khoảnh khắc thành video. Thử lại, hoặc xem nhật ký ở trang Giới thiệu.",
            _ => "Có lỗi không mong muốn. Xem nhật ký ở trang Giới thiệu và gửi kèm khi báo lỗi.",
        };

        private void ChooseOutputFolder(GeneralPage page)
        {
            // Recovery and the disk check both look at the output folder: it can't move under a live recording.
            if (Busy)
            {
                ShowMessage("Dừng ghi trước khi đổi thư mục lưu.", MessageBoxIcon.Information);
                return;
            }

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

            // Starting or saving takes seconds: let it finish instead of killing FFmpeg halfway.
            for (var waited = 0; recorder != null && (recorder.State == SessionState.Starting || recorder.State == SessionState.Finalizing) && waited < 1200; waited++)
            {
                await Task.Delay(250);
            }

            if (recorder != null && Busy)
            {
                ShowWindow();
                var answer = MessageBox.Show(_form, "Recora đang ghi. Dừng, lưu video rồi thoát?", "Recora", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                if (answer != DialogResult.OK)
                {
                    return;
                }

                _exiting = true;
                await StopRecording(recorder);
            }

            _exiting = true;
            await StopReplay();
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

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
    }
}
