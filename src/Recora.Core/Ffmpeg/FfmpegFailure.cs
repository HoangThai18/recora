using System;
using System.Linq;

namespace Recora.Core.Ffmpeg
{
    /// <summary>Picks why FFmpeg failed out of its stderr, for the log and for deciding what to blame.</summary>
    public static class FfmpegFailure
    {
        private const int MaxLines = 4;

        private static readonly string[] Markers = { "error", "failed", "could not", "invalid", "not supported", "no such" };

        /// <summary>
        /// The first error lines, cause first. FFmpeg keeps printing after the cause (stream tables, encoder summaries),
        /// so the end of stderr rarely names the encoder that broke, and blaming the right one depends on that name.
        /// </summary>
        public static string Reason(string stderr)
        {
            var lines = stderr.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            if (lines.Count == 0)
            {
                return "ffmpeg exited";
            }

            var errors = lines.Where(IsError).Take(MaxLines).ToList();
            return string.Join(" | ", errors.Count > 0 ? errors : lines.Skip(Math.Max(0, lines.Count - MaxLines)));
        }

        private static bool IsError(string line) =>
            !line.StartsWith("frame=", StringComparison.Ordinal)
            && line.IndexOf("Conversion failed", StringComparison.Ordinal) < 0
            && Markers.Any(m => line.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
