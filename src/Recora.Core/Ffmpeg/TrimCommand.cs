using System;
using System.Globalization;
using System.IO;
using Recora.Core.Recording;

namespace Recora.Core.Ffmpeg
{
    /// <summary>
    /// "Cắt video": keeps the part between two moments by copying streams, so it takes a second and loses nothing. A copy
    /// can only start on a keyframe, so the start moves back to the keyframe before it (Recora's are 2 s apart).
    /// </summary>
    public static class TrimCommand
    {
        /// <summary>The longest a stream-copy cut can start before the chosen moment in Recora's own files.</summary>
        public const double KeyframeSlackSeconds = 2.0;

        /// <summary>
        /// A keyframe this close after the chosen start is taken instead of the one before it: Recora's keyframes sit a
        /// frame or so after each 2-second mark, and seeking to exactly 2.0 would otherwise go back to 0.
        /// </summary>
        public const double LateKeyframeSeconds = 0.25;

        /// <summary>The muxer for a file Recora can cut, by extension; null when the type is not supported.</summary>
        public static string? Muxer(string path) => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".mp4" => "mp4",
            ".mkv" => "matroska",
            ".m4a" => "ipod",
            ".mp3" => "mp3",
            ".wav" => "wav",
            _ => null,
        };

        public static bool CanTrim(string path) => Muxer(path) != null;

        private static bool IsSoundOnly(string muxer) => muxer == "ipod" || muxer == "mp3" || muxer == "wav";

        /// <summary>"Recora 2026-11-01 20-15-03 (cắt).mp4" next to the source, numbered if taken.</summary>
        public static string OutputPath(string source, Func<string, bool> exists)
        {
            var folder = Path.GetDirectoryName(source) ?? string.Empty;
            var baseName = Path.GetFileNameWithoutExtension(source) + " (cắt)";
            return OutputNaming.Unique(folder, baseName, Path.GetExtension(source), exists);
        }

        public static FfmpegArgs Build(string input, string output, TimeSpan start, TimeSpan end)
        {
            if (end <= start)
            {
                throw new ArgumentException($"end {end} must come after start {start}", nameof(end));
            }

            var muxer = Muxer(output) ?? throw new ArgumentException("unsupported output type: " + output, nameof(output));

            // Both bounds go before -i: input seeking is instant, and -to on the input counts from the same clock as -ss.
            // Sound-only files have no keyframes to catch: every packet is a starting point, so seek exactly.
            var late = IsSoundOnly(muxer) ? 0 : Math.Min(LateKeyframeSeconds, Math.Max(0, (end - start).TotalSeconds / 2));
            var seek = start + TimeSpan.FromSeconds(late);
            var args = new FfmpegArgs()
                .Add("-hide_banner", "-y", "-nostats", "-loglevel", "error")
                .AddIf(start > TimeSpan.Zero, "-ss", Seconds(seek))
                .Add("-to", Seconds(end), "-i", input)
                .Add("-map", "0", "-c", "copy", "-avoid_negative_ts", "make_zero");

            return args.AddIf(muxer == "mp4", "-movflags", "+faststart").Add("-f", muxer, output);
        }

        internal static string Seconds(TimeSpan value) => value.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The length of a file Recora did not record itself (the library only remembers its own): <c>ffmpeg -i</c> prints
    /// "Duration: 00:01:02.50" on stderr and exits with an error for lack of an output, which is expected.
    /// </summary>
    public static class MediaInfoCommand
    {
        private static readonly System.Text.RegularExpressions.Regex DurationLine =
            new System.Text.RegularExpressions.Regex(@"Duration:\s*(\d+):(\d{2}):(\d{2}(?:\.\d+)?)");

        public static FfmpegArgs Build(string input) => new FfmpegArgs().Add("-hide_banner", "-nostdin", "-i", input);

        public static TimeSpan? ParseDuration(string stderr)
        {
            var match = DurationLine.Match(stderr ?? string.Empty);
            if (!match.Success)
            {
                return null;
            }

            var seconds = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * 3600
                + int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) * 60
                + double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            return seconds > 0 ? TimeSpan.FromSeconds(seconds) : (TimeSpan?)null;
        }
    }

    /// <summary>One still of a video, scaled into a fixed box and written as raw BGRA to stdout, for the cut dialog.</summary>
    public static class FramePreviewCommand
    {
        public const int Width = 480;
        public const int Height = 270;
        public const int FrameBytes = Width * Height * 4;

        public static FfmpegArgs Build(string input, TimeSpan at) =>
            new FfmpegArgs()
                .Add("-hide_banner", "-nostats", "-loglevel", "error")
                .Add("-ss", TrimCommand.Seconds(at < TimeSpan.Zero ? TimeSpan.Zero : at), "-i", input)
                .Add("-frames:v", "1", "-an")
                .Add("-vf", $"scale={Width}:{Height}:force_original_aspect_ratio=decrease,pad={Width}:{Height}:(ow-iw)/2:(oh-ih)/2,format=bgra")
                .Add("-f", "rawvideo", "-pix_fmt", "bgra", "pipe:1");
    }
}
