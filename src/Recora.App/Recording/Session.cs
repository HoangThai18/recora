using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Recora.App.Audio;
using Recora.App.Logging;
using Recora.Core;
using Recora.Core.Ffmpeg;
using Recora.Core.Recording;

namespace Recora.App.Recording
{
    internal sealed record FinishedSegment(string Name, double AudioSeconds, long Bytes, TimeSpan Duration);

    /// <summary>
    /// One recording: its folder <c>&lt;output&gt;\.recora\&lt;id&gt;</c>, the manifest, finished segments and the live one.
    /// Every segment gets a fresh FFmpeg and fresh pipes (a named pipe instance serves one client), while the audio
    /// devices keep running for the whole session.
    /// </summary>
    internal sealed class Session
    {
        private readonly List<FinishedSegment> _segments = new List<FinishedSegment>();
        private FfmpegProcess? _process;
        private AudioPump? _pump;
        private string? _segmentName;

        private Session(RecordingRequest request, string folder, SessionManifest manifest)
        {
            Request = request;
            Folder = folder;
            Manifest = manifest;
        }

        public RecordingRequest Request { get; }

        public string Folder { get; }

        public SessionManifest Manifest { get; }

        public IReadOnlyList<FinishedSegment> Segments => _segments;

        public string? EncoderName { get; private set; }

        public TimeSpan Elapsed =>
            TimeSpan.FromTicks(_segments.Sum(s => s.Duration.Ticks)) + (_process?.LastProgress?.OutTime ?? TimeSpan.Zero);

        public long Bytes => _segments.Sum(s => s.Bytes) + (_process?.LastProgress?.TotalSizeBytes ?? 0);

        public double CurrentFps => _process?.LastProgress?.Fps ?? 0;

        public static Session Create(RecordingRequest request)
        {
            Directory.CreateDirectory(request.OutputFolder);
            var free = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(request.OutputFolder))!).AvailableFreeSpace;
            if (DiskGuard.ShouldStop(free))
            {
                throw new RecoraException("disk_full", $"only {free} bytes free in {request.OutputFolder}");
            }

            var now = DateTime.Now;
            var id = now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            var folder = Path.Combine(request.OutputFolder, ".recora", id);
            Directory.CreateDirectory(folder);
            File.SetAttributes(Path.Combine(request.OutputFolder, ".recora"), FileAttributes.Directory | FileAttributes.Hidden);

            var output = OutputNaming.Unique(request.OutputFolder, OutputNaming.BaseName(now), FinalizeCommand.Extension(request.Container), File.Exists);
            var manifest = new SessionManifest
            {
                Id = id,
                StartedAt = now.ToString("o", CultureInfo.InvariantCulture),
                OutputPath = output,
                Container = request.Container.ToString(),
                Codec = request.Codec.ToString(),
            };
            manifest.Save(folder);
            Log.Info($"session {id}: {request.Source} -> {output}");
            return new Session(request, folder, manifest);
        }

        /// <summary>Starts a segment with this plan. Returns null when frames flow, otherwise why it failed.</summary>
        public string? TryOpenSegment(string ffmpeg, RecordingPlan plan, TimeSpan firstFrameTimeout)
        {
            var name = SessionManifest.SegmentName(Manifest.Segments.Count);
            var path = Path.Combine(Folder, name);
            Manifest.Segments.Add(name);
            Manifest.Save(Folder);

            var pump = new AudioPump(Request.AudioTracks);
            pump.Start();
            var process = FfmpegProcess.Start(ffmpeg, RecordingCommand.Build(plan, path, pump.PipePaths), "seg " + name);
            var clock = Stopwatch.StartNew();

            while (!process.HasExited && clock.Elapsed < firstFrameTimeout && (process.LastProgress?.Frame ?? 0) == 0)
            {
                Thread.Sleep(50);
            }

            if (!process.HasExited && (process.LastProgress?.Frame ?? 0) > 0)
            {
                _process = process;
                _pump = pump;
                _segmentName = name;
                EncoderName = plan.Encoder.FfmpegName;
                Log.Info($"segment {name} recording with {plan.Backend}/{plan.Encoder.FfmpegName}");
                return null;
            }

            var reason = process.HasExited ? LastLine(process.ErrorTail) : "no frame within " + firstFrameTimeout.TotalSeconds + " s";
            process.Kill();
            process.WaitForExit(TimeSpan.FromSeconds(5));
            process.Dispose();
            pump.Stop();
            Manifest.Segments.Remove(name);
            Manifest.Save(Folder);
            TryDelete(path);
            return reason;
        }

        /// <summary>Ends the live segment cleanly: drain the audio, then 'q', then remember its exact audio length.</summary>
        public void CloseSegment()
        {
            var process = _process;
            var pump = _pump;
            var name = _segmentName;
            if (process == null || pump == null || name == null)
            {
                return;
            }

            _process = null;
            _pump = null;
            _segmentName = null;

            pump.Drain(TimeSpan.FromSeconds(3));
            var audioSeconds = pump.FramesSent.DefaultIfEmpty(0).Max() / (double)RecordingCommand.AudioSampleRate;
            process.RequestStop();
            if (!process.WaitForExit(TimeSpan.FromSeconds(15)))
            {
                Log.Warn($"segment {name}: ffmpeg ignored q, killing it");
                process.Kill();
                process.WaitForExit(TimeSpan.FromSeconds(5));
            }

            var duration = process.LastProgress?.OutTime ?? TimeSpan.Zero;
            var path = Path.Combine(Folder, name);
            var bytes = File.Exists(path) ? new FileInfo(path).Length : 0;
            process.Dispose();
            pump.Stop();

            _segments.Add(new FinishedSegment(name, audioSeconds, bytes, duration));
            Log.Info($"segment {name} closed: {duration.TotalSeconds:0.00} s, audio {audioSeconds:0.00} s, {bytes} bytes");
        }

        public void StopAudio()
        {
            // The WASAPI sources belong to the caller (MainController), which disposes them after StopAsync.
        }

        public void DeleteFolder()
        {
            try
            {
                Directory.Delete(Folder, recursive: true);
            }
            catch (IOException error)
            {
                Log.Warn("could not delete session folder: " + error.Message);
            }
        }

        private static string LastLine(string text) =>
            text.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.Length > 0) ?? "ffmpeg exited";

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
        }
    }
}
