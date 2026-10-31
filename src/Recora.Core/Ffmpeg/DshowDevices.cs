using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Recora.Core.Ffmpeg
{
    /// <summary>Webcams and capture cards as FFmpeg's DirectShow input lists them.</summary>
    public static class DshowDevices
    {
        // [dshow @ 0000020f...] "Integrated Camera" (video)   or   "Game Capture HD60 S" (video, audio)
        private static readonly Regex DeviceLine = new Regex("\"(?<name>[^\"]+)\"\\s+\\((?<kinds>[a-z, ]+)\\)", RegexOptions.Compiled);

        public static FfmpegArgs ListCommand() =>
            new FfmpegArgs().Add("-hide_banner", "-list_devices", "true", "-f", "dshow", "-i", "dummy");

        public static IReadOnlyList<string> ParseVideo(string output) =>
            DeviceLine.Matches(output)
                .Cast<Match>()
                .Where(m => m.Groups["kinds"].Value.Split(',').Any(k => k.Trim() == "video"))
                .Select(m => m.Groups["name"].Value)
                .Distinct()
                .ToList();
    }
}
