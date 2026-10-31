using System.Collections.Generic;
using System.Text;
using Recora.Core.Encoding;
using Recora.Core.Recording;

namespace Recora.Core.Ffmpeg
{
    /// <summary>
    /// Joins a session's segments into the final file by copying streams: nothing is re-encoded, so finishing a long
    /// recording takes seconds and costs no quality.
    /// </summary>
    public static class FinalizeCommand
    {
        /// <summary>Contents of the concat demuxer list. Paths go in single quotes; a quote inside becomes <c>'\''</c>.</summary>
        public static string ConcatList(IEnumerable<string> segmentPaths)
        {
            var list = new StringBuilder("ffconcat version 1.0\n");

            foreach (var path in segmentPaths)
            {
                list.Append("file '").Append(path.Replace("'", @"'\''")).Append("'\n");
            }

            return list.ToString();
        }

        public static FfmpegArgs Build(string concatListPath, string outputPath, ContainerFormat format, VideoCodec codec)
        {
            var args = new FfmpegArgs()
                .Add("-hide_banner", "-y", "-nostats")
                .Add("-f", "concat", "-safe", "0", "-i", concatListPath)
                .Add("-map", "0", "-c", "copy");

            if (format == ContainerFormat.Mp4)
            {
                // Apple players and some editors only accept H.265 in MP4 under the hvc1 tag.
                args.AddIf(codec == VideoCodec.Hevc, "-tag:v", "hvc1");
                return args.Add("-f", "mp4", outputPath);
            }

            return args.Add("-f", "matroska", outputPath);
        }

        public static string Extension(ContainerFormat format) => format == ContainerFormat.Mp4 ? ".mp4" : ".mkv";

        /// <summary>A single, cleanly closed Matroska segment can simply be moved into place when the target is MKV.</summary>
        public static bool CanMoveInsteadOfRemux(IReadOnlyCollection<string> segments, ContainerFormat format, bool recovered) =>
            format == ContainerFormat.Mkv && segments.Count == 1 && !recovered;
    }
}
