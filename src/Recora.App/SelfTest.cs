using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Recora.App.Audio;
using Recora.App.Logging;
using Recora.App.Native;
using Recora.App.Platform;
using Recora.App.Recording;
using Recora.Core.Audio;
using Recora.Core.Capture;
using Recora.Core.Encoding;
using Recora.Core.Ffmpeg;
using Recora.Core.Recording;

namespace Recora.App
{
    /// <summary>
    /// <c>Recora.exe --selftest &lt;dir&gt;</c>: the real recording path without UI, for CI. Probes capture backends and
    /// encoders in app order, records about three seconds of screen plus a test tone through the audio pipe, stops with
    /// <c>q</c>, finalizes to MP4 and writes <c>selftest.json</c>. Exit code 0 only if an MP4 came out.
    /// </summary>
    internal static partial class SelfTest
    {
        private static readonly TimeSpan Target = TimeSpan.FromSeconds(3);

        /// <summary>
        /// The ducking opt-out talks to Windows through hand-declared COM interfaces: a wrong vtable would crash or
        /// fail here on every capture endpoint the runner has, instead of in a user's game.
        /// </summary>
        private static void CheckDuckingOptOut()
        {
            using (var enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator())
            {
                foreach (var flow in new[] { NAudio.CoreAudioApi.DataFlow.Capture, NAudio.CoreAudioApi.DataFlow.Render })
                {
                    foreach (var device in enumerator.EnumerateAudioEndPoints(flow, NAudio.CoreAudioApi.DeviceState.Active))
                    {
                        var ok = Audio.Ducking.OptOut(device.ID);
                        Console.WriteLine($"ducking opt-out on {flow} {device.FriendlyName}: {ok}; hands-free: {Audio.AudioDevices.IsHandsFree(device)}");
                        if (!ok)
                        {
                            throw new InvalidOperationException("ducking opt-out failed on " + device.FriendlyName);
                        }
                    }
                }
            }
        }

        public static int Run(string directory)
        {
            Directory.CreateDirectory(directory);
            Log.Initialize(directory, echoToConsole: true);
            var report = new SelfTestReport();

            try
            {
                CheckDuckingOptOut();
                Execute(directory, report);
            }
            catch (Exception error)
            {
                report.Error = error.ToString();
                Log.Error("selftest failed", error);
            }

            WriteReport(Path.Combine(directory, "selftest.json"), report);
            Log.Info(report.Ok ? "selftest OK" : "selftest FAILED");
            return report.Ok ? 0 : 1;
        }

        private static void Execute(string directory, SelfTestReport report)
        {
            var ffmpeg = FfmpegLocator.Find();
            report.FfmpegVersion = FfmpegProcess.Run(ffmpeg, ProbeCommand.Version(), TimeSpan.FromSeconds(20))
                .Output.Split('\n').FirstOrDefault()?.Trim() ?? string.Empty;

            var filters = ProbeCommand.ParseFilters(FfmpegProcess.Run(ffmpeg, ProbeCommand.Filters(), TimeSpan.FromSeconds(20)).Output);
            var encoders = ProbeCommand.ParseEncoders(FfmpegProcess.Run(ffmpeg, ProbeCommand.Encoders(), TimeSpan.FromSeconds(20)).Output);
            var caps = ProbeCommand.Capabilities(filters);
            report.HasDdagrab = caps.HasDdagrab;
            report.HasGfxcapture = caps.HasGfxcapture;

            var monitors = MonitorService.List();
            foreach (var info in monitors)
            {
                report.Monitors.Add($"{info.Name}: {info.Target.DeviceName} {info.Target.Bounds} adapter \"{info.AdapterName}\" ({info.Target.Vendor}) dda output {info.Target.DdaOutputIndex?.ToString() ?? "-"}{(info.Target.IsPrimary ? " primary" : string.Empty)}");
            }

            // CI runners have no sound card: listing must work and say so, recording goes on with the test tone.
            var speakers = AudioDevices.Speakers();
            var microphones = AudioDevices.Microphones();
            report.AudioDevices = speakers.Count + microphones.Count == 0
                ? "không có thiết bị âm thanh"
                : $"{speakers.Count} loa ({string.Join(", ", speakers.Select(d => d.Name))}), {microphones.Count} micro";

            var primary = monitors.FirstOrDefault(m => m.Target.IsPrimary) ?? monitors[0];
            var source = new MonitorSource(primary.Target);
            var plan = FirstWorkingPlan(ffmpeg, source, caps, encoders, report)
                ?? throw new InvalidOperationException("no capture backend and encoder pair worked");

            report.Backend = plan.Backend.ToString();
            report.Encoder = plan.Encoder.FfmpegName;

            var segment = Path.Combine(directory, "segment-001.mkv");
            RecordSegment(ffmpeg, plan, segment, report);

            var list = Path.Combine(directory, "segments.txt");
            File.WriteAllText(list, FinalizeCommand.ConcatList(new[] { new ConcatEntry(segment, report.AudioSeconds) }), new UTF8Encoding(false));
            var output = Path.Combine(directory, "selftest.mp4");
            var finalize = FfmpegProcess.Run(ffmpeg, FinalizeCommand.Build(list, output, ContainerFormat.Mp4, plan.Encoder.Codec), TimeSpan.FromMinutes(1));
            report.FinalizeExitCode = finalize.ExitCode;
            report.FinalizeErrorTail = finalize.ExitCode == 0 ? null : LastLines(finalize.Error, 6);
            report.Output = output;
            report.OutputBytes = File.Exists(output) ? new FileInfo(output).Length : 0;
            report.Ok = finalize.ExitCode == 0 && report.OutputBytes > 0 && report.SegmentExitCode == 0;

            // The same recorder the app uses, with a pause in the middle.
            RecordWithPause(directory, report);
            report.Ok = report.Ok && report.PausedOutput != null;

            // FFmpeg dying under a live recording, as when a game switches to exclusive fullscreen.
            RecordThroughCaptureLoss(directory, report);
            report.Ok = report.Ok && report.HealedOutput != null;
        }

        private static RecordingPlan? FirstWorkingPlan(
            string ffmpeg, CaptureSource source, CaptureCapabilities caps, IReadOnlyCollection<string> encoders, SelfTestReport report)
        {
            var backends = new List<CaptureBackend>();
            if (caps.HasDdagrab)
            {
                backends.Add(CaptureBackend.Ddagrab);
            }

            if (caps.HasGfxcapture)
            {
                backends.Add(CaptureBackend.Gfxcapture);
            }

            backends.Add(CaptureBackend.Gdigrab);

            foreach (var backend in backends)
            {
                foreach (var encoder in EncoderCatalog.Candidates(source.Monitor.Vendor, VideoCodec.H264, encoders))
                {
                    var plan = new RecordingPlan(source, backend, encoder, QualityPreset.Balanced, 30, OutputScale.Original, DrawCursor: true, AudioLayout.Mixed);
                    var clock = Stopwatch.StartNew();
                    var probe = FfmpegProcess.Run(ffmpeg, ProbeCommand.EncoderTest(plan), TimeSpan.FromSeconds(30));
                    var ok = probe.ExitCode == 0;

                    report.Attempts.Add(new SelfTestAttempt
                    {
                        Backend = backend.ToString(),
                        Encoder = encoder.FfmpegName,
                        Ok = ok,
                        Milliseconds = clock.ElapsedMilliseconds,
                        Error = ok ? null : LastLines(probe.Error, 3),
                    });

                    if (ok)
                    {
                        return plan;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Records over a black full-screen board. 1.5 s after FFmpeg opens the audio pipe (audio time zero) the board turns
        /// white and the test tone starts at the same instant; CI then finds the first white frame and the first loud
        /// sample in the MP4 and compares their times, which measures audio/video sync directly.
        /// </summary>
        private static void RecordSegment(string ffmpeg, RecordingPlan plan, string segment, SelfTestReport report)
        {
            var marker = TimeSpan.FromSeconds(1.5);
            var tone = new ToneInput(1000, 0.3f) { OnsetFrame = (long)(marker.TotalSeconds * RecordingCommand.AudioSampleRate) };

            // The tone goes through the whole microphone chain, so the sync measured below also proves the chain adds
            // no delay (and the gate opens on the first loud sample).
            var chain = new MicProcessingSettings { HighPass = true, NoiseGate = true, Compressor = true, Limiter = true };
            var microphone = new ProcessedInput(tone, new MicProcessor(chain, RecordingCommand.AudioSampleRate));
            var track = new AudioTrackSpec("mix", new (IAudioInput, float)[] { (microphone, 1f) });
            long connectedAt = 0;

            using (var board = new SyncBoard())
            using (var pump = new AudioPump(new[] { track }))
            {
                board.Show();
                board.Refresh();
                Pump(TimeSpan.FromMilliseconds(300));

                pump.Failed += (name, error) => report.AudioError = $"{name}: {error.Message}";
                pump.Connected += (_, ticks) => Interlocked.Exchange(ref connectedAt, ticks);
                pump.Start();

                using (var process = FfmpegProcess.Start(ffmpeg, RecordingCommand.Build(plan, segment, pump.PipePaths), "selftest"))
                {
                    var clock = Stopwatch.StartNew();
                    var flashed = false;

                    // Stops by the audio clock: Target after FFmpeg opened the pipe. FFmpeg's out_time would lag behind
                    // by the encoder's delay and make the file a second longer than asked.
                    while (!process.HasExited && clock.Elapsed < TimeSpan.FromSeconds(30)
                        && (!flashed || Stopwatch.GetTimestamp() - Interlocked.Read(ref connectedAt) < (long)(Target.TotalSeconds * Stopwatch.Frequency)))
                    {
                        var zero = Interlocked.Read(ref connectedAt);
                        var due = zero + (long)(marker.TotalSeconds * Stopwatch.Frequency);
                        if (!flashed && zero != 0 && Stopwatch.GetTimestamp() >= due)
                        {
                            board.Flash();
                            report.FlashLateMilliseconds = (Stopwatch.GetTimestamp() - due) * 1000.0 / Stopwatch.Frequency;
                            flashed = true;
                        }

                        Application.DoEvents();
                        Thread.Sleep(1);
                    }

                    report.SecondsToReachTarget = clock.Elapsed.TotalSeconds;
                    report.AudioDrained = pump.Drain(TimeSpan.FromSeconds(3));
                    report.AudioSeconds = pump.FramesSent[0] / (double)RecordingCommand.AudioSampleRate;
                    process.RequestStop();

                    if (!process.WaitForExit(TimeSpan.FromSeconds(15)))
                    {
                        report.Error = "ffmpeg did not stop after q";
                        process.Kill();
                        process.WaitForExit(TimeSpan.FromSeconds(5));
                    }

                    report.SegmentExitCode = process.HasExited ? process.ExitCode : -1;
                    report.Frames = process.LastProgress?.Frame ?? 0;
                    report.DroppedFrames = process.LastProgress?.DroppedFrames ?? 0;
                    report.SegmentErrorTail = process.HasExited && process.ExitCode == 0 ? null : process.ErrorTail;
                }

                pump.Stop();
            }
        }

        private static void Pump(TimeSpan duration)
        {
            var clock = Stopwatch.StartNew();
            while (clock.Elapsed < duration)
            {
                Application.DoEvents();
                Thread.Sleep(5);
            }
        }

        private static string LastLines(string text, int count) =>
            string.Join(" | ", text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Reverse().Take(count).Reverse());

        private static void WriteReport(string path, SelfTestReport report)
        {
            using (var file = File.Create(path))
            using (var writer = JsonReaderWriterFactory.CreateJsonWriter(file, Encoding.UTF8, ownsStream: false, indent: true))
            {
                new DataContractJsonSerializer(typeof(SelfTestReport)).WriteObject(writer, report);
            }
        }
    }

    [DataContract]
    internal sealed class SelfTestReport
    {
        [DataMember(Order = 0)] public bool Ok { get; set; }
        [DataMember(Order = 1)] public string FfmpegVersion { get; set; } = string.Empty;
        [DataMember(Order = 2)] public bool HasDdagrab { get; set; }
        [DataMember(Order = 3)] public bool HasGfxcapture { get; set; }
        [DataMember(Order = 4)] public List<SelfTestAttempt> Attempts { get; set; } = new List<SelfTestAttempt>();
        [DataMember(Order = 5)] public string? Backend { get; set; }
        [DataMember(Order = 6)] public string? Encoder { get; set; }
        [DataMember(Order = 7)] public double SecondsToReachTarget { get; set; }
        [DataMember(Order = 8)] public int SegmentExitCode { get; set; } = -1;
        [DataMember(Order = 9)] public long Frames { get; set; }
        [DataMember(Order = 10)] public long DroppedFrames { get; set; }
        [DataMember(Order = 11)] public int FinalizeExitCode { get; set; } = -1;
        [DataMember(Order = 12)] public string? Output { get; set; }
        [DataMember(Order = 13)] public long OutputBytes { get; set; }
        [DataMember(Order = 14)] public string? AudioError { get; set; }
        [DataMember(Order = 15)] public string? SegmentErrorTail { get; set; }
        [DataMember(Order = 16)] public string? FinalizeErrorTail { get; set; }
        [DataMember(Order = 18)] public double FlashLateMilliseconds { get; set; }
        [DataMember(Order = 19)] public double AudioSeconds { get; set; }
        [DataMember(Order = 20)] public bool AudioDrained { get; set; }
        [DataMember(Order = 21)] public List<string> Monitors { get; set; } = new List<string>();
        [DataMember(Order = 22)] public string? AudioDevices { get; set; }
        [DataMember(Order = 23)] public string? PausedOutput { get; set; }
        [DataMember(Order = 24)] public double PausedSeconds { get; set; }
        [DataMember(Order = 25)] public string? PausedEncoder { get; set; }
        [DataMember(Order = 26)] public int LeftoverSessions { get; set; }
        [DataMember(Order = 27)] public string? HealedOutput { get; set; }
        [DataMember(Order = 28)] public double HealedSeconds { get; set; }
        [DataMember(Order = 17)] public string? Error { get; set; }
    }

    /// <summary>Full-screen black board that turns white at the sync marker; drawn above everything, never excluded from capture.</summary>
    internal sealed class SyncBoard : Form
    {
        public SyncBoard()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = Screen.PrimaryScreen.Bounds;
            BackColor = System.Drawing.Color.Black;
            TopMost = true;
            ShowInTaskbar = false;
        }

        public void Flash()
        {
            BackColor = System.Drawing.Color.White;
            Refresh();
        }
    }

    [DataContract]
    internal sealed class SelfTestAttempt
    {
        [DataMember(Order = 0)] public string Backend { get; set; } = string.Empty;
        [DataMember(Order = 1)] public string Encoder { get; set; } = string.Empty;
        [DataMember(Order = 2)] public bool Ok { get; set; }
        [DataMember(Order = 3)] public long Milliseconds { get; set; }
        [DataMember(Order = 4)] public string? Error { get; set; }
    }
}
