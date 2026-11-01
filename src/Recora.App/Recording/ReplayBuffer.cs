using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Recora.App.Audio;
using Recora.App.Logging;
using Recora.Core;
using Recora.Core.Encoding;
using Recora.Core.Ffmpeg;
using Recora.Core.Recording;

namespace Recora.App.Recording
{
    /// <summary>
    /// "Lưu khoảnh khắc": one FFmpeg keeps the last N seconds of the screen as a ring of 2-second chunks in
    /// <c>&lt;output&gt;\.recora\replay</c>; a save copies the newest chunks aside and joins them by stream copy, while
    /// the ring keeps running. The folder has no session manifest, so crash recovery leaves it alone; it is emptied at
    /// every start and stop instead. Methods block: call them from the thread pool, one at a time.
    /// </summary>
    internal sealed class ReplayBuffer
    {
        public const string FolderName = "replay";
        private const string SaveFolderPrefix = "replay-save-";
        private static readonly TimeSpan FirstFrameTimeout = TimeSpan.FromSeconds(6);

        private readonly CaptureEnvironment _environment;
        private readonly EncoderMemory _memory;
        private readonly object _gate = new object();
        private FfmpegProcess? _process;
        private AudioPump? _pump;
        private string? _folder;
        private int _seconds;
        private VideoCodec _codec;
        private Stopwatch? _clock;

        public ReplayBuffer(CaptureEnvironment environment, EncoderMemory memory)
        {
            _environment = environment;
            _memory = memory;
        }

        public bool IsRunning
        {
            get
            {
                lock (_gate)
                {
                    return _process != null && !_process.HasExited;
                }
            }
        }

        /// <summary>FFmpeg stopped by itself (driver reset, disk error): the controller restarts or reports it.</summary>
        public bool HasDied
        {
            get
            {
                lock (_gate)
                {
                    return _process != null && _process.HasExited;
                }
            }
        }

        public string? EncoderName { get; private set; }

        public int Seconds => _seconds;

        /// <summary>How much a save would hold right now: the running time, up to the chosen length.</summary>
        public TimeSpan Held
        {
            get
            {
                var clock = _clock;
                if (clock == null || !IsRunning)
                {
                    return TimeSpan.Zero;
                }

                var max = TimeSpan.FromSeconds(_seconds);
                return clock.Elapsed < max ? clock.Elapsed : max;
            }
        }

        public static string FolderFor(string outputFolder) => Path.Combine(outputFolder, ".recora", FolderName);

        /// <summary>Removes what a crash left: the ring and any half-finished save.</summary>
        public static void CleanStale(string outputFolder)
        {
            var root = Path.Combine(outputFolder, ".recora");
            if (!Directory.Exists(root))
            {
                return;
            }

            TryDeleteFolder(FolderFor(outputFolder));
            foreach (var folder in Directory.GetDirectories(root, SaveFolderPrefix + "*"))
            {
                TryDeleteFolder(folder);
            }
        }

        public void Start(RecordingRequest request, int seconds)
        {
            Stop();

            Directory.CreateDirectory(request.OutputFolder);
            var free = Finalizer.FreeBytes(request.OutputFolder);
            if (free.HasValue && DiskGuard.ShouldStop(free.Value))
            {
                throw new RecoraException("disk_full", $"only {free} bytes free in {request.OutputFolder}");
            }

            var folder = FolderFor(request.OutputFolder);
            TryDeleteFolder(folder);
            Directory.CreateDirectory(folder);
            File.SetAttributes(Path.Combine(request.OutputFolder, ".recora"), FileAttributes.Directory | FileAttributes.Hidden);

            CandidateRunner.Run(_environment, _memory, request, plan => TryOpen(plan, request, folder, seconds));
        }

        /// <summary>
        /// Saves the last <see cref="Seconds"/> as a new video next to the recordings. Waits for the chunk being written
        /// (at most one chunk length) so the moment the user just saw is in it. Returns null when nothing is held yet.
        /// </summary>
        public SavedRecording? Save(string outputFolder, ContainerFormat container)
        {
            string folder;
            int seconds;
            VideoCodec codec;
            lock (_gate)
            {
                if (_process == null || _process.HasExited || _folder == null)
                {
                    return null;
                }

                folder = _folder;
                seconds = _seconds;
                codec = _codec;
            }

            var list = Path.Combine(folder, ReplayCommand.ListFileName);
            var before = ReadList(list).LastOrDefault();
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(ReplayCommand.ChunkSeconds + 1.5);
            IReadOnlyList<ReplayChunk> chunks;
            do
            {
                chunks = ReadList(list);
                if (chunks.Count > 0 && !Equals(chunks[chunks.Count - 1], before))
                {
                    break;
                }

                Thread.Sleep(100);
            }
            while (DateTime.UtcNow < deadline && IsRunning);

            var picked = ReplayChunks.Pick(chunks, seconds, ReplayChunks.SafePickCount(ReplayCommand.RingSize(seconds)));
            if (picked.Count == 0)
            {
                Log.Warn("replay save: no finished chunk yet");
                return null;
            }

            // Copied aside first: the ring keeps turning, and joining straight from it could read a chunk mid-rewrite.
            var save = Path.Combine(outputFolder, ".recora", SaveFolderPrefix + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(save);
            try
            {
                var copies = new List<string>();
                for (var i = 0; i < picked.Count; i++)
                {
                    var copy = Path.Combine(save, $"{i:000}.mkv");
                    File.Copy(Path.Combine(folder, Path.GetFileName(picked[i].FileName)), copy);
                    copies.Add(copy);
                }

                var concat = Path.Combine(save, "concat.txt");
                File.WriteAllText(concat, FinalizeCommand.ConcatList(copies), new UTF8Encoding(false));

                var output = OutputNaming.Unique(outputFolder, OutputNaming.BaseName(DateTime.Now) + " khoảnh khắc", FinalizeCommand.Extension(container), File.Exists);
                var partial = Path.Combine(save, "final.part");
                var run = FfmpegProcess.Run(_environment.FfmpegPath, FinalizeCommand.Build(concat, partial, container, codec), TimeSpan.FromSeconds(90));
                if (run.ExitCode != 0 || !File.Exists(partial) || new FileInfo(partial).Length == 0)
                {
                    Log.Error("replay save failed: " + run.Error);
                    throw new RecoraException("replay_save_failed", "ffmpeg concat exited with " + run.ExitCode);
                }

                File.Move(partial, output);
                var duration = TimeSpan.FromSeconds(picked.Sum(c => c.Seconds));
                LibraryFiles.Remember(output, duration);
                Log.Info($"replay saved {output}: {picked.Count} chunk(s), {duration.TotalSeconds:0.0} s");
                return new SavedRecording(output, duration, new FileInfo(output).Length);
            }
            finally
            {
                TryDeleteFolder(save);
            }
        }

        public void Stop()
        {
            FfmpegProcess? process;
            AudioPump? pump;
            string? folder;
            lock (_gate)
            {
                process = _process;
                pump = _pump;
                folder = _folder;
                _process = null;
                _pump = null;
                _folder = null;
                _clock = null;
            }

            if (process != null)
            {
                process.RequestStop();
                if (!process.WaitForExit(TimeSpan.FromSeconds(5)))
                {
                    process.Kill();
                    process.WaitForExit(TimeSpan.FromSeconds(5));
                }

                process.Dispose();
                Log.Info("replay buffer stopped");
            }

            pump?.Stop();
            if (folder != null)
            {
                TryDeleteFolder(folder);
            }
        }

        private string? TryOpen(RecordingPlan plan, RecordingRequest request, string folder, int seconds)
        {
            var pump = new AudioPump(request.AudioTracks);
            pump.Start();
            FfmpegProcess process;
            try
            {
                process = FfmpegProcess.Start(_environment.FfmpegPath, ReplayCommand.Build(plan, folder, pump.PipePaths, seconds), "replay", plan.UsesCpu);
            }
            catch
            {
                // ffmpeg.exe missing or blocked: the pipes and their threads must not outlive the attempt.
                pump.Stop();
                throw;
            }

            var clock = Stopwatch.StartNew();

            while (!process.HasExited && clock.Elapsed < FirstFrameTimeout && (process.LastProgress?.Frame ?? 0) == 0)
            {
                Thread.Sleep(50);
            }

            if (!process.HasExited && (process.LastProgress?.Frame ?? 0) > 0)
            {
                lock (_gate)
                {
                    _process = process;
                    _pump = pump;
                    _folder = folder;
                    _seconds = seconds;
                    _codec = plan.Encoder.Codec;
                    _clock = clock;
                }

                EncoderName = plan.Encoder.FfmpegName;
                Log.Info($"replay buffer: last {seconds} s with {plan.Backend}/{plan.Encoder.FfmpegName}{FrameRoutes.Label(plan.Route)}");
                return null;
            }

            var reason = process.HasExited ? FfmpegFailure.Reason(process.ErrorTail) : "no frame within " + FirstFrameTimeout.TotalSeconds + " s";
            process.Kill();
            process.WaitForExit(TimeSpan.FromSeconds(5));
            process.Dispose();
            pump.Stop();
            foreach (var file in Directory.GetFiles(folder))
            {
                TryDeleteFile(file);
            }

            return reason;
        }

        /// <summary>FFmpeg replaces the list by renaming a temp file over it; reading must allow that (share delete).</summary>
        private static IReadOnlyList<ReplayChunk> ReadList(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return ReplayChunks.Parse(reader.ReadToEnd());
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                return new ReplayChunk[0];
            }
        }

        private static void TryDeleteFolder(string folder)
        {
            try
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, recursive: true);
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Log.Warn($"could not delete {folder}: {error.Message}");
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
            }
        }
    }
}
