using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

            // Generous timeout: copying streams is disk-bound, about a second per gigabyte on an SSD.
            var bytes = usable.Sum(s => s.Bytes);
            var timeout = TimeSpan.FromSeconds(60 + bytes / (20L * 1024 * 1024));
            var command = audioOnly ? FinalizeCommand.BuildAudio(list, output, audioFormat) : FinalizeCommand.Build(list, output, format, codec);
            var run = FfmpegProcess.Run(ffmpeg, command, timeout);

            if (run.ExitCode != 0 || !File.Exists(output) || new FileInfo(output).Length == 0)
            {
                Log.Error("finalize failed: " + run.Error);
                throw new RecoraException("finalize_failed", "ffmpeg concat exited with " + run.ExitCode);
            }

            Log.Info($"saved {output} ({new FileInfo(output).Length} bytes, {usable.Count} segment(s){(recovered ? ", recovered" : string.Empty)})");
            Directory.Delete(folder, recursive: true);
            return output;
        }
    }
}
