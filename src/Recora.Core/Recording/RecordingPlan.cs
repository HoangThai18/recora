using System;
using Recora.Core.Capture;
using Recora.Core.Encoding;

namespace Recora.Core.Recording
{
    /// <summary>Output height cap. The source is only ever scaled down, never up.</summary>
    public enum OutputScale
    {
        Original = 0,
        P1080 = 1080,
        P720 = 720,
        P480 = 480,
    }

    public enum AudioLayout
    {
        None,

        /// <summary>System sound and microphone mixed by the app into one track.</summary>
        Mixed,

        /// <summary>Track 1 system sound, track 2 microphone, for editing them apart.</summary>
        SeparateTracks,
    }

    public enum ContainerFormat
    {
        Mp4,
        Mkv,
    }

    /// <summary>What a recording may cost when the CPU has to encode it next to a game.</summary>
    public static class LightCpuProfile
    {
        public const int MaxFps = 30;

        /// <summary>
        /// At most 720p at 30 frames a second, under a quarter of the pixels of 1080p60: x264's load follows the pixels,
        /// and no encoder setting saves as much. Choices already below that stay as they are.
        /// </summary>
        public static (int Fps, OutputScale Scale) Apply(int fps, OutputScale scale) =>
            (Math.Min(fps, MaxFps), scale == OutputScale.Original || scale == OutputScale.P1080 ? OutputScale.P720 : scale);
    }

    /// <summary>Everything that decides one segment's FFmpeg command, already resolved to a backend and an encoder.</summary>
    /// <param name="MaxGigabytesPerHour">The user's size limit (0 for none), shared between the picture and the sound.</param>
    /// <param name="Route">How frames reach a GPU encoder.</param>
    public sealed record RecordingPlan(
        CaptureSource Source,
        CaptureBackend Backend,
        VideoEncoder Encoder,
        QualityPreset Quality,
        int Fps,
        OutputScale Scale,
        bool DrawCursor,
        AudioLayout Audio,
        int MaxGigabytesPerHour = 0,
        FrameRoute Route = FrameRoute.Textures)
    {
        /// <summary>
        /// Encoding or pixel conversion runs on the CPU, so this recording competes with a game for its cores. Copying a
        /// converted NV12 frame does not count: it costs little, and low priority would only drop frames from the video.
        /// </summary>
        public bool UsesCpu => !Encoder.IsHardware || Route == FrameRoute.BgraMemory || Backend == CaptureBackend.Gdigrab;

        public int AudioTrackCount => Audio switch
        {
            AudioLayout.None => 0,
            AudioLayout.Mixed => 1,
            _ => 2,
        };

        /// <summary>The frame size FFmpeg captures: the source area rounded to even numbers (BR-07).</summary>
        public PixelRect CaptureSize => Source.Area.ToEncodable();

        /// <summary>The frame size that reaches the encoder after the optional downscale.</summary>
        public (int Width, int Height) OutputSize
        {
            get
            {
                var capture = CaptureSize;
                var maxHeight = (int)Scale;

                if (Scale == OutputScale.Original || capture.Height <= maxHeight)
                {
                    return (capture.Width, capture.Height);
                }

                var width = (int)Math.Round(capture.Width * (double)maxHeight / capture.Height) & ~1;
                return (width, maxHeight);
            }
        }

        public VideoEncodeOptions EncodeOptions
        {
            get
            {
                var (width, height) = OutputSize;
                var limit = VideoEncoderArgs.VideoKbpsForSizeLimit(MaxGigabytesPerHour, VideoEncoderArgs.AudioBitrateKbps(Quality), AudioTrackCount);
                return new VideoEncodeOptions(Encoder, Quality, Fps, width, height, limit);
            }
        }
    }
}
