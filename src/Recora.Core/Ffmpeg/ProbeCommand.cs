using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Recora.Core.Capture;
using Recora.Core.Recording;

namespace Recora.Core.Ffmpeg
{
    /// <summary>Asks the bundled ffmpeg.exe what it can do, and whether an encoder really works on this PC.</summary>
    public static class ProbeCommand
    {
        // " .. ddagrab           |->V       Grab Windows Desktop images..." -> "ddagrab" (two flag columns in FFmpeg 8.1, opt_common.c)
        private static readonly Regex FilterLine = new Regex(@"^\s*[T.][S.]\s+(\S+)\s+\S*->\S*", RegexOptions.Compiled);

        // " V....D h264_nvenc           NVIDIA NVENC H.264 encoder (codec h264)" -> "h264_nvenc"
        private static readonly Regex EncoderLine = new Regex(@"^\s*[VAS][A-Z.]{5}\s+(\S+)\s", RegexOptions.Compiled);

        public static FfmpegArgs Filters() => new FfmpegArgs().Add("-hide_banner", "-filters");

        public static FfmpegArgs Encoders() => new FfmpegArgs().Add("-hide_banner", "-encoders");

        public static IReadOnlyCollection<string> ParseFilters(string output) => ParseNames(output, FilterLine);

        public static IReadOnlyCollection<string> ParseEncoders(string output) => ParseNames(output, EncoderLine);

        public static CaptureCapabilities Capabilities(IReadOnlyCollection<string> filters) =>
            new CaptureCapabilities(filters.Contains("ddagrab"), filters.Contains("gfxcapture"));

        /// <summary>
        /// Encodes a handful of real captured frames to nowhere, through exactly the filter chain and encoder settings
        /// a recording would use. A hardware encoder that exists but cannot take this GPU's textures fails here.
        /// </summary>
        public static FfmpegArgs EncoderTest(RecordingPlan plan) =>
            new FfmpegArgs()
                .Add("-hide_banner", "-nostats", "-loglevel", "error")
                .Add(RecordingCommand.VideoInput(plan))
                .Add(RecordingCommand.VideoOutput(plan))
                .Add("-frames:v", "10", "-an", "-f", "null", "-");

        private static IReadOnlyCollection<string> ParseNames(string output, Regex line) =>
            new HashSet<string>(
                output.Split('\n')
                    .Select(text => line.Match(text.TrimEnd('\r')))
                    .Where(match => match.Success && match.Groups[1].Value != "=")
                    .Select(match => match.Groups[1].Value));
    }
}
