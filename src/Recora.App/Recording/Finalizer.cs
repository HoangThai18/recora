using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static System.FormattableString;
using Recora.App.Logging;
using Recora.Core;
using Recora.Core.Encoding;
using Recora.Core.Ffmpeg;
using Recora.Core.Recording;

namespace Recora.App.Recording
{
    /// <summary>Turns a session folder into the final file by stream copy, then removes the folder (BR-01).</summary>
    internal static class Finalizer
    {
        private const long MarginBytes = 64L * 1024 * 1024;

        public static long? FreeBytes(string folder)
        {
            try
            {
                var root = Path.GetPathRoot(Path.GetFullPath(folder));
                return string.IsNullOrEmpty(root) || root!.StartsWith(@"\\", StringComparison.Ordinal) ? (long?)null : new DriveInfo(root).AvailableFreeSpace;
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return null;
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

        public static string? Finish(string ffmpeg, string folder, SessionManifest manifest, IReadOnlyList<FinishedSegment> segments, bool recovered = false)
        {
            var usable = segments.Where(s => s.Bytes > 1024).ToList();
            if (usable.Count == 0)
            {
                Directory.Delete(folder, recursive: true);
                return null;
            }

            var audioOnly = Enum.TryParse<AudioFormat>(manifest.Container, out var audioFormat);
            var format = Enum.TryParse<ContainerFormat>(manifest.Container, out var container) ? container : ContainerFormat.Mp4;
            var codec = Enum.TryParse<VideoCodec>(manifest.Codec, out var parsed) ? parsed : VideoCodec.H264;
            var output = manifest.OutputPath;
            if (File.Exists(output))
            {
                output = OutputNaming.Unique(Path.GetDirectoryName(output)!, Path.GetFileNameWithoutExtension(output), Path.GetExtension(output), File.Exists);
            }

            var entries = usable.Select(s => new ConcatEntry(Path.Combine(folder, s.Name), s.AudioSeconds > 0 ? s.AudioSeconds : (double?)null)).ToList();
            var list = Path.Combine(folder, "concat.txt");
            File.WriteAllText(list, FinalizeCommand.ConcatList(entries), new UTF8Encoding(false));

            // The copy needs as much room as the segments: without it, keep the segments and say how much to free
            // (BR-06: never leave a broken file). A UNC share reports no drive; then just try.
            var bytes = usable.Sum(s => s.Bytes);
            var free = FreeBytes(folder);
            if (free.HasValue && free.Value < bytes + MarginBytes)
            {
                throw new RecoraException("finalize_no_space", Invariant($"need {bytes + MarginBytes} bytes, {free.Value} free"));
            }

            // Written inside the session folder first and moved into place only when complete, so a failure or a
            // kill mid-copy never leaves a half file where the user looks.
            var partial = Path.Combine(folder, "final.part");
            var timeout = TimeSpan.FromSeconds(60 + bytes / (20L * 1024 * 1024));
            var command = audioOnly ? FinalizeCommand.BuildAudio(list, partial, audioFormat) : FinalizeCommand.Build(list, partial, format, codec);
            var run = FfmpegProcess.Run(ffmpeg, command, timeout);

            // Sessions saved by 0.1.x named the requested codec: an H.264 fallback was then tagged hvc1 and refused by
            // the MP4 muxer. Join those again untagged instead of failing at every start.
            if (run.ExitCode != 0 && !audioOnly && codec == VideoCodec.Hevc && run.Error.IndexOf("incompatible with output codec", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Log.Warn("segments are not H.265 after all; joining again without the hvc1 tag");
                TryDeleteFile(partial);
                run = FfmpegProcess.Run(ffmpeg, FinalizeCommand.Build(list, partial, format, VideoCodec.H264), timeout);
            }

            if (run.ExitCode != 0 || !File.Exists(partial) || new FileInfo(partial).Length == 0)
            {
                Log.Error("finalize failed: " + run.Error);
                TryDeleteFile(partial);
                throw new RecoraException("finalize_failed", "ffmpeg concat exited with " + run.ExitCode);
            }

            File.Move(partial, output);
            Log.Info($"saved {output} ({new FileInfo(output).Length} bytes, {usable.Count} segment(s){(recovered ? ", recovered" : string.Empty)})");

            // The video is saved: failing to tidy up (antivirus holding a segment) must not turn it into an error.
            // The marker tells the next scan to just delete the folder, never to join it a second time.
            try
            {
                File.WriteAllText(Path.Combine(folder, SessionManifest.SavedMarker), output);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                TryDeleteFile(Path.Combine(folder, SessionManifest.FileName));
            }

            CleanSavedFolder(folder);
            return output;
        }

        /// <summary>
        /// Segments and scratch files go first; the manifest and the marker last, so a folder that can't be emptied
        /// (a segment held open by antivirus) still says "saved" and the next scan only deletes it again.
        /// </summary>
        public static void CleanSavedFolder(string folder)
        {
            foreach (var file in Directory.GetFiles(folder))
            {
                var name = Path.GetFileName(file);
                if (name != SessionManifest.FileName && name != SessionManifest.SavedMarker)
                {
                    TryDeleteFile(file);
                }
            }

            if (Directory.GetFiles(folder).Length <= 2)
            {
                TryDeleteFile(Path.Combine(folder, SessionManifest.FileName));
                TryDeleteFile(Path.Combine(folder, SessionManifest.SavedMarker));
                try
                {
                    Directory.Delete(folder, recursive: true);
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                {
                    Log.Warn("session folder left behind: " + error.Message);
                }
            }
            else
            {
                Log.Warn("session folder kept for the next start to clean: " + folder);
            }
        }
    }
}
