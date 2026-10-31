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

    /// <summary>Everything that decides one segment's FFmpeg command, already resolved to a backend and an encoder.</summary>
    public sealed record RecordingPlan(
        CaptureSource Source,
        CaptureBackend Backend,
        VideoEncoder Encoder,
        QualityPreset Quality,
        int Fps,
        OutputScale Scale,
        bool DrawCursor,
        AudioLayout Audio)
    {
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
                return new VideoEncodeOptions(Encoder, Quality, Fps, width, height);
            }
        }
    }
}
