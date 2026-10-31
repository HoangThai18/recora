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
using Recora.App.Recording;
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
    internal static class SelfTest
    {
        private static readonly TimeSpan Target = TimeSpan.FromSeconds(3);

        public static int Run(string directory)
        {
            Directory.CreateDirectory(directory);
            Log.Initialize(directory, echoToConsole: true);
            var report = new SelfTestReport();

            try
            {
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
            report.FfmpegVersion = FfmpegProcess.Run(ffmpeg, new FfmpegArgs().Add("-hide_banner", "-version"), TimeSpan.FromSeconds(20))
                .Output.Split('\n').FirstOrDefault()?.Trim() ?? string.Empty;

            var filters = ProbeCommand.ParseFilters(FfmpegProcess.Run(ffmpeg, ProbeCommand.Filters(), TimeSpan.FromSeconds(20)).Output);
            var encoders = ProbeCommand.ParseEncoders(FfmpegProcess.Run(ffmpeg, ProbeCommand.Encoders(), TimeSpan.FromSeconds(20)).Output);
            var caps = ProbeCommand.Capabilities(filters);
            report.HasDdagrab = caps.HasDdagrab;
            report.HasGfxcapture = caps.HasGfxcapture;

            var source = new MonitorSource(PrimaryMonitor());
            var plan = FirstWorkingPlan(ffmpeg, source, caps, encoders, report)
                ?? throw new InvalidOperationException("no capture backend and encoder pair worked");

            report.Backend = plan.Backend.ToString();
            report.Encoder = plan.Encoder.FfmpegName;

            var segment = Path.Combine(directory, "segment-001.mkv");
            RecordSegment(ffmpeg, plan, segment, report);

            var list = Path.Combine(directory, "segments.txt");
            File.WriteAllText(list, FinalizeCommand.ConcatList(new[] { segment }), new UTF8Encoding(false));
            var output = Path.Combine(directory, "selftest.mp4");
            var finalize = FfmpegProcess.Run(ffmpeg, FinalizeCommand.Build(list, output, ContainerFormat.Mp4, plan.Encoder.Codec), TimeSpan.FromMinutes(1));
            report.FinalizeExitCode = finalize.ExitCode;
            report.Output = output;
            report.OutputBytes = File.Exists(output) ? new FileInfo(output).Length : 0;
            report.Ok = finalize.ExitCode == 0 && report.OutputBytes > 0 && report.SegmentExitCode == 0;
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

        private static void RecordSegment(string ffmpeg, RecordingPlan plan, string segment, SelfTestReport report)
        {
            var track = new AudioTrackSpec("mix", new (IAudioInput, float)[] { (new ToneInput(440, 0.2f), 1f) });

            using (var pump = new AudioPump(new[] { track }))
            {
                pump.Failed += (name, error) => report.AudioError = $"{name}: {error.Message}";
                pump.Start();

                using (var process = FfmpegProcess.Start(ffmpeg, RecordingCommand.Build(plan, segment, pump.PipePaths), "selftest"))
                {
                    var clock = Stopwatch.StartNew();

                    // Stop once FFmpeg reports ~3 s muxed; progress comes every 0.5 s, so aim slightly early.
                    while (!process.HasExited && clock.Elapsed < TimeSpan.FromSeconds(30)
                        && (process.LastProgress?.OutTime ?? TimeSpan.Zero) < Target - TimeSpan.FromMilliseconds(250))
                    {
                        Thread.Sleep(50);
                    }

                    report.SecondsToReachTarget = clock.Elapsed.TotalSeconds;
                    pump.BeginStop();
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

        private static MonitorTarget PrimaryMonitor()
        {
            var screen = Screen.PrimaryScreen;
            var bounds = new PixelRect(screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height);
            var hmonitor = NativeMethods.MonitorFromPoint(new NativeMethods.Point { X = 0, Y = 0 }, NativeMethods.MonitorDefaultToPrimary);

            // DXGI enumeration arrives in P1; until then the primary screen is assumed to be output 0 of the default adapter.
            return new MonitorTarget(screen.DeviceName, bounds, 0, hmonitor.ToInt64(), GpuVendor.Unknown, IsPrimary: true);
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
        [DataMember(Order = 16)] public string? Error { get; set; }
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
