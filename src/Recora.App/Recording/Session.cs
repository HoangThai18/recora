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
using Recora.Core.Encoding;
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

        /// <summary>A copy: the UI timer reads it while a pause or stop adds to it on the thread pool.</summary>
        public IReadOnlyList<FinishedSegment> Segments
        {
            get
            {
                lock (_segments)
                {
                    return _segments.ToList();
                }
            }
        }

        public string? EncoderName { get; private set; }

        public TimeSpan Elapsed =>
            TimeSpan.FromTicks(Segments.Sum(s => s.Duration.Ticks)) + (_process?.LastProgress?.OutTime ?? TimeSpan.Zero);

        public long Bytes => Segments.Sum(s => s.Bytes) + (_process?.LastProgress?.TotalSizeBytes ?? 0);

        public double CurrentFps => _process?.LastProgress?.Fps ?? 0;

        public static Session Create(RecordingRequest request)
        {
            Directory.CreateDirectory(request.OutputFolder);
            var free = Finalizer.FreeBytes(request.OutputFolder);
            if (free.HasValue && DiskGuard.ShouldStop(free.Value))
            {
                throw new RecoraException("disk_full", $"only {free} bytes free in {request.OutputFolder}");
            }

            var now = DateTime.Now;
            var id = now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            var folder = Path.Combine(request.OutputFolder, ".recora", id);
            Directory.CreateDirectory(folder);
            File.SetAttributes(Path.Combine(request.OutputFolder, ".recora"), FileAttributes.Directory | FileAttributes.Hidden);

            var extension = request.AudioOnly is AudioFormat audio ? AudioRecordingCommand.Extension(audio) : FinalizeCommand.Extension(request.Container);
            var output = OutputNaming.Unique(request.OutputFolder, OutputNaming.BaseName(now), extension, File.Exists);
            var manifest = new SessionManifest
            {
                Id = id,
                StartedAt = now.ToString("o", CultureInfo.InvariantCulture),
                OutputPath = output,
                Container = request.AudioOnly?.ToString() ?? request.Container.ToString(),
                Codec = request.AudioOnly == null ? request.Codec.ToString() : "Audio",
            };
            manifest.Save(folder);
            Log.Info($"session {id}: {request.Source} -> {output}");
            return new Session(request, folder, manifest);
        }

        /// <summary>Starts a segment with this plan. Returns null when frames flow, otherwise why it failed.</summary>
        public string? TryOpenSegment(string ffmpeg, RecordingPlan plan, TimeSpan firstFrameTimeout)
        {
            var failure = TryOpen(ffmpeg, (path, pipes) => RecordingCommand.Build(plan, path, pipes), $"{plan.Backend}/{plan.Encoder.FfmpegName}{FrameRoutes.Label(plan.Route)}", plan.Encoder.FfmpegName, audioOnly: false, firstFrameTimeout, plan.UsesCpu);

            // The codec actually written, not the one asked for: H.265 falls back to H.264 on GPUs (and the CPU) that
            // lack it, and finalizing must not tag that stream as hvc1.
            if (failure == null)
            {
                LivePlan = plan;
            }

            var codec = plan.Encoder.Codec.ToString();
            if (failure == null && Manifest.Codec != codec)
            {
                Manifest.Codec = codec;
                Manifest.Save(Folder);
            }

            return failure;
        }

        /// <summary>Starts a sound-only segment (one mixed track). Returns null once audio flows.</summary>
        public string? TryOpenAudioSegment(string ffmpeg, AudioFormat format, QualityPreset quality, TimeSpan timeout)
        {
            if (Request.AudioTracks.Count != 1)
            {
                return "sound-only recording needs exactly one mixed track, got " + Request.AudioTracks.Count;
            }

            var codec = AudioRecordingCommand.Codec(format, quality)[1];
            return TryOpen(ffmpeg, (path, pipes) => AudioRecordingCommand.Build(pipes[0], format, quality, path), "audio/" + codec, codec, audioOnly: true, timeout);
        }

        private string? TryOpen(string ffmpeg, Func<string, IReadOnlyList<string>, FfmpegArgs> command, string label, string encoder, bool audioOnly, TimeSpan firstFrameTimeout, bool yieldCpu = false)
        {
            var name = SessionManifest.SegmentName(Manifest.Segments.Count);
            var path = Path.Combine(Folder, name);
            Manifest.Segments.Add(name);
            Manifest.Save(Folder);

            var pump = new AudioPump(Request.AudioTracks);
            pump.Start();
            var process = FfmpegProcess.Start(ffmpeg, command(path, pump.PipePaths), "seg " + name, yieldCpu);
            var clock = Stopwatch.StartNew();

            // Video is live once a frame is encoded; sound-only output has no frames, so time must move instead.
            bool Flowing() => (process.LastProgress?.Frame ?? 0) > 0 || (audioOnly && (process.LastProgress?.OutTime ?? TimeSpan.Zero) > TimeSpan.Zero);

            while (!process.HasExited && clock.Elapsed < firstFrameTimeout && !Flowing())
            {
                Thread.Sleep(50);
            }

            if (!process.HasExited && Flowing())
            {
                _process = process;
                _pump = pump;
                _segmentName = name;
                EncoderName = encoder;
                Log.Info($"segment {name} recording with {label}");
                return null;
            }

            // The last few stderr lines: FFmpeg ends with "Conversion failed!", the cause is a line or two above.
            var reason = process.HasExited ? FfmpegFailure.Reason(process.ErrorTail) : (audioOnly ? "no audio" : "no frame") + " within " + firstFrameTimeout.TotalSeconds + " s";
            process.Kill();
            process.WaitForExit(TimeSpan.FromSeconds(5));
            process.Dispose();
            pump.Stop();
            Manifest.Segments.Remove(name);
            Manifest.Save(Folder);
            TryDelete(path);
            return reason;
        }

        /// <summary>The plan the live segment runs with (backend and encoder), to restart it the same way.</summary>
        public RecordingPlan? LivePlan { get; private set; }

        /// <summary>
        /// Why the live FFmpeg exited by itself, or null while it runs. Windows takes the screen away from every
        /// capture when a game switches to exclusive fullscreen; ddagrab then stops with "AcquireNextFrame failed".
        /// </summary>
        public string? LiveSegmentEnded()
        {
            var process = _process;
            return process != null && process.HasExited ? LastLines(process.ErrorTail, 3) : null;
        }

        /// <summary>For the selftest: FFmpeg dying under a live recording, as Windows makes it do.</summary>
        internal void KillLiveForTest() => _process?.Kill();

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

            lock (_segments)
            {
                _segments.Add(new FinishedSegment(name, audioSeconds, bytes, duration));
            }
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

        private static string LastLines(string text, int count)
        {
            var lines = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            return lines.Count == 0 ? "ffmpeg exited" : string.Join(" | ", lines.Skip(Math.Max(0, lines.Count - count)));
        }

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
