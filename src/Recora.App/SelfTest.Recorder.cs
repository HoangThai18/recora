using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Recora.App.Audio;
using Recora.App.Logging;
using Recora.App.Recording;
using Recora.Core.Audio;
using Recora.Core.Capture;
using Recora.Core.Encoding;
using Recora.Core.Recording;
using Recora.Core.Settings;

namespace Recora.App
{
    /// <summary>
    /// The recorder the app uses, driven without UI: a recording with a pause (two segments joined into one MP4),
    /// a crash in the middle of a recording, and the recovery of what the crash left behind (BR-01).
    /// </summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// Records over a black board for 2 s, pauses, turns the board red for 1 s, turns it back black, resumes over a
        /// white board for 2 s, stops. CI then checks the MP4 is the two segments joined (length = their sum) and holds
        /// not a single red frame: nothing shown during the pause got into the video.
        /// </summary>
        private static void RecordWithPause(string directory, SelfTestReport report)
        {
            var folder = Path.Combine(directory, "recorder");
            var (recorder, environment) = NewRecorder();
            var request = Request(environment, folder);
            string? encoder = null;

            using (var board = new SyncBoard())
            {
                board.Show();
                board.Refresh();
                Pump(TimeSpan.FromMilliseconds(300));

                void Paint(System.Drawing.Color color) => board.Invoke(new Action(() =>
                {
                    board.BackColor = color;
                    board.Refresh();
                }));

                var run = Task.Run(async () =>
                {
                    await recorder.StartAsync(request).ConfigureAwait(false);
                    ThrowIfNot(recorder, SessionState.Recording, "start");
                    encoder = recorder.Status().Encoder;
                    Thread.Sleep(2000);
                    await recorder.PauseAsync().ConfigureAwait(false);
                    ThrowIfNot(recorder, SessionState.Paused, "pause");
                    Paint(System.Drawing.Color.Red);
                    Thread.Sleep(1000);
                    Paint(System.Drawing.Color.Black);
                    Thread.Sleep(300);
                    await recorder.ResumeAsync().ConfigureAwait(false);
                    ThrowIfNot(recorder, SessionState.Recording, "resume");
                    Paint(System.Drawing.Color.White);
                    Thread.Sleep(2000);
                    return await recorder.StopAsync().ConfigureAwait(false);
                });

                // The board must keep painting while the recorder works on the thread pool.
                while (!run.IsCompleted)
                {
                    Application.DoEvents();
                    Thread.Sleep(5);
                }

                var saved = run.GetAwaiter().GetResult();
                report.PausedOutput = saved?.Path;
                report.PausedSeconds = saved?.Duration.TotalSeconds ?? 0;
                report.PausedEncoder = encoder;
            }

            var sessions = Path.Combine(folder, ".recora");
            report.LeftoverSessions = Directory.Exists(sessions) ? Directory.GetDirectories(sessions).Length : 0;
            if (report.PausedOutput == null)
            {
                throw new InvalidOperationException("recorder produced no file");
            }
        }

        /// <summary><c>--selftest-crash &lt;dir&gt;</c>: starts recording into dir, then kills this process mid-recording.</summary>
        public static int Crash(string directory)
        {
            Directory.CreateDirectory(directory);
            Log.Initialize(directory, echoToConsole: true);
            var (recorder, environment) = NewRecorder();
            Task.Run(() => recorder.StartAsync(Request(environment, directory))).GetAwaiter().GetResult();
            if (recorder.State != SessionState.Recording)
            {
                Log.Error("crash test: recording did not start");
                return 1;
            }

            Thread.Sleep(3000);
            Log.Info($"crash test: killing Recora while recording ({recorder.Status().Bytes} bytes muxed)");
            Process.GetCurrentProcess().Kill();
            return 2;
        }

        /// <summary><c>--recover &lt;dir&gt;</c>: what the app does at startup, then prints the files it saved.</summary>
        public static int Recover(string directory)
        {
            Log.Initialize(directory, echoToConsole: true);
            var recovered = RecoveryService.RecoverAll(FfmpegLocator.Find(), directory).Saved;
            foreach (var path in recovered)
            {
                Console.WriteLine("recovered " + path);
            }

            Log.Info($"recover: {recovered.Count} file(s)");
            return recovered.Count > 0 ? 0 : 1;
        }

        /// <summary><c>--settings-roundtrip &lt;dir&gt;</c>: saves non-default settings, loads them back, compares every field.</summary>
        public static int SettingsRoundtrip(string directory)
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "settings.json");
            var changed = new AppSettings
            {
                OutputFolder = Path.Combine(directory, "Video của tôi"),
                Mode = CaptureMode.Window,
                MonitorDevice = @"\\.\DISPLAY2",
                Quality = QualityPreset.High,
                Codec = VideoCodec.Hevc,
                Encoder = EncoderChoice.Qsv,
                Fps = 144,
                Scale = OutputScale.P720,
                Container = ContainerFormat.Mkv,
                DrawCursor = false,
                SystemAudio = false,
                SystemVolume = 0.5f,
                Microphone = true,
                MicrophoneVolume = 1.5f,
                SeparateTracks = true,
                HotkeyRecord = "Ctrl+Alt+R",
                HotkeyPause = "Ctrl+Alt+P",
                HotkeyScreenshot = "Ctrl+Alt+S",
                ScreenshotFormat = ScreenshotFormat.Jpg,
                ScreenshotToClipboard = false,
                MinimizeToTrayOnClose = false,
                StartWithWindows = true,
                HideRecoraFromVideo = false,
                Countdown = true,
                MaxGigabytesPerHour = 4,
                ReplayEnabled = true,
                ReplaySeconds = 120,
                HotkeyReplay = "Ctrl+Alt+F10",
                MicProcessing = new MicProcessingSettings { GainDb = 9, HighPass = true, NoiseGate = true, GateThresholdDb = -52, Compressor = true, Limiter = false },
            };

            SettingsStore.Save(path, changed);
            var loaded = SettingsStore.Load(path);
            var differences = typeof(AppSettings).GetProperties()
                .Where(p => p.CanRead && p.PropertyType != typeof(EncoderProbeCache))
                .Where(p => !SameValue(p.GetValue(changed), p.GetValue(loaded)))
                .Select(p => $"{p.Name}: {Describe(p.GetValue(changed))} -> {Describe(p.GetValue(loaded))}")
                .ToList();

            foreach (var line in differences)
            {
                Console.WriteLine("lost " + line);
            }

            Console.WriteLine(differences.Count == 0 ? "settings roundtrip OK" : "settings roundtrip FAILED");
            return differences.Count == 0 ? 0 : 1;
        }

        /// <summary>Values compare by Equals; nested settings objects (the microphone chain) compare field by field.</summary>
        private static bool SameValue(object? a, object? b)
        {
            if (a == null || b == null || a.GetType().IsValueType || a is string)
            {
                return Equals(a, b);
            }

            return a.GetType() == b.GetType()
                && a.GetType().GetProperties().Where(p => p.CanRead && p.GetIndexParameters().Length == 0).All(p => Equals(p.GetValue(a), p.GetValue(b)));
        }

        private static string Describe(object? value) => value == null || value.GetType().IsValueType || value is string
            ? value?.ToString() ?? "null"
            : string.Join(", ", value.GetType().GetProperties().Select(p => p.Name + "=" + p.GetValue(value)));

        private static (Recorder Recorder, CaptureEnvironment Environment) NewRecorder()
        {
            var environment = CaptureEnvironment.Detect();
            var settings = new AppSettings();
            return (new Recorder(environment, new EncoderMemory(settings, environment.Fingerprint)), environment);
        }

        private static RecordingRequest Request(CaptureEnvironment environment, string folder)
        {
            var monitor = environment.MonitorFor(string.Empty);
            var tone = new ToneInput(440, 0.2f);
            return new RecordingRequest(
                new MonitorSource(monitor.Target),
                VideoCodec.H264,
                null,
                QualityPreset.Balanced,
                30,
                OutputScale.Original,
                DrawCursor: true,
                ContainerFormat.Mp4,
                folder,
                new[] { new AudioTrackSpec("Âm thanh", new (IAudioInput, float)[] { (tone, 1f) }) });
        }

        private static void ThrowIfNot(Recorder recorder, SessionState expected, string step)
        {
            if (recorder.State != expected)
            {
                throw new InvalidOperationException($"{step}: recorder is {recorder.State}, expected {expected}");
            }
        }
    }
}
