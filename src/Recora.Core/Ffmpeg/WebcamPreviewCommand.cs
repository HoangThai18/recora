namespace Recora.Core.Ffmpeg
{
    /// <summary>
    /// The floating webcam window: FFmpeg decodes the device and writes fixed-size BGRA frames to stdout, which the app
    /// paints. The window itself is what ends up in the screen recording.
    /// </summary>
    public static class WebcamPreviewCommand
    {
        public const int Width = 320;
        public const int Height = 240;
        public const int FrameBytes = Width * Height * 4;

        public static FfmpegArgs Build(string deviceName, bool mirror) =>
            new FfmpegArgs()
                .Add("-hide_banner", "-nostats", "-loglevel", "error")
                .Add("-rtbufsize", "64M", "-f", "dshow", "-i", "video=" + deviceName)
                .Add("-vf", (mirror ? "hflip," : string.Empty)
                    + $"scale={Width}:{Height}:force_original_aspect_ratio=increase,crop={Width}:{Height},format=bgra")
                .Add("-an", "-f", "rawvideo", "-pix_fmt", "bgra", "pipe:1");
    }
}
