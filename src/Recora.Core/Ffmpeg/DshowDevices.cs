using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Recora.Core.Ffmpeg
{
    /// <summary>Webcams and capture cards as FFmpeg's DirectShow input lists them.</summary>
    public static class DshowDevices
    {
        // [dshow @ 0000020f...] "Integrated Camera" (video)      (FFmpeg 5+; older builds print a header line instead)
        private static readonly Regex DeviceLine = new Regex("\"(?<name>[^\"]+)\"\\s+\\((?<kind>video|audio|none)\\)", RegexOptions.Compiled);

        public static FfmpegArgs ListCommand() =>
            new FfmpegArgs().Add("-hide_banner", "-list_devices", "true", "-f", "dshow", "-i", "dummy");

        public static IReadOnlyList<string> ParseVideo(string output) =>
            DeviceLine.Matches(output)
                .Cast<Match>()
                .Where(m => m.Groups["kind"].Value == "video")
                .Select(m => m.Groups["name"].Value)
                .Distinct()
                .ToList();
    }
}
