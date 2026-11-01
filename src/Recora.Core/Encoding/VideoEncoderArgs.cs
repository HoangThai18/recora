using System;
using System.Collections.Generic;
using System.Globalization;

namespace Recora.Core.Encoding
{
    public enum QualityPreset
    {
        /// <summary>"Rất nét": visually lossless for gameplay, largest files.</summary>
        High,

        /// <summary>"Cân bằng": the default; sharp text and motion at roughly half the size of High.</summary>
        Balanced,

        /// <summary>"Nhẹ": small files, softer in fast scenes.</summary>
        Small,

        /// <summary>"Tiết kiệm": the smallest files for long sessions; fine detail goes soft when the picture moves fast.</summary>
        Saver,
    }

    /// <param name="MaxVideoKbps">The user's size limit turned into a video bitrate ceiling; null when there is none.</param>
    public sealed record VideoEncodeOptions(VideoEncoder Encoder, QualityPreset Quality, int Fps, int Width, int Height, int? MaxVideoKbps = null);

    /// <summary>
    /// Turns one quality preset into each encoder's own rate control. Scales differ per encoder
    /// (see docs/research/capture-encode.md, "Thang chất lượng"), so the mapping lives here and nowhere else.
    /// </summary>
    public static class VideoEncoderArgs
    {
        public static int BaseQuality(QualityPreset preset) => preset switch
        {
            QualityPreset.High => 19,
            QualityPreset.Balanced => 24,
            QualityPreset.Small => 29,
            _ => 32,
        };

        public static int AudioBitrateKbps(QualityPreset preset) => preset switch
        {
            QualityPreset.High => 192,
            QualityPreset.Balanced => 160,
            QualityPreset.Small => 128,
            _ => 96,
        };

        /// <summary>Sizes are counted in decimal GB, as Windows Explorer's "GB" is close enough and users think in it.</summary>
        public static int TotalKbpsForGigabytesPerHour(int gigabytesPerHour) =>
            (int)Math.Round(gigabytesPerHour * 1_000_000_000.0 * 8 / 3600 / 1000);

        /// <summary>
        /// What is left for the picture once the sound tracks have their share of an hourly size limit. Never below
        /// 300 kbps: under that even a still desktop turns to mush, and the limit is a ceiling, not a promise.
        /// </summary>
        public static int? VideoKbpsForSizeLimit(int gigabytesPerHour, int audioKbps, int audioTracks)
        {
            if (gigabytesPerHour <= 0)
            {
                return null;
            }

            return Math.Max(300, TotalKbpsForGigabytesPerHour(gigabytesPerHour) - audioKbps * Math.Max(0, audioTracks));
        }

        /// <summary>
        /// Peak bitrate cap. Quality-based modes spend whatever a scene needs; the cap stops a burst of confetti or
        /// foliage from writing a 100 Mbit/s spike into an otherwise small file.
        /// </summary>
        public static int MaxBitrateKbps(VideoEncodeOptions options)
        {
            var bitsPerPixel = options.Quality switch
            {
                QualityPreset.High => 0.30,
                QualityPreset.Balanced => 0.17,
                QualityPreset.Small => 0.10,
                _ => 0.06,
            };

            if (options.Encoder.Codec != VideoCodec.H264)
            {
                bitsPerPixel *= 0.7;
            }

            var kbps = (double)options.Width * options.Height * options.Fps * bitsPerPixel / 1000.0;
            var presetCap = (int)Math.Max(1500, Math.Min(200_000, Math.Round(kbps)));
            return options.MaxVideoKbps is int limit ? Math.Min(presetCap, limit) : presetCap;
        }

        public static IReadOnlyList<string> Build(VideoEncodeOptions options)
        {
            var encoder = options.Encoder;
            var q = BaseQuality(options.Quality);
            var gop = Num(Math.Max(1, options.Fps * 2));
            var maxKbps = MaxBitrateKbps(options);
            var limited = options.MaxVideoKbps.HasValue;
            var args = new List<string> { "-c:v", encoder.FfmpegName };

            switch (encoder.Kind)
            {
                case EncoderKind.Nvenc:
                    // NVENC's AV1 target-quality scale runs 0-63 instead of 0-51.
                    var cq = encoder.Codec == VideoCodec.Av1 ? (int)Math.Round(q * 63.0 / 51.0) : q;
                    args.AddRange(new[]
                    {
                        "-preset", "p4", "-tune", "hq", "-rc", "vbr", "-cq", Num(cq), "-b:v", "0",
                        "-maxrate", Kbps(maxKbps), "-bufsize", Kbps(maxKbps * 2), "-spatial-aq", "1",
                    });
                    break;

                case EncoderKind.Amf when encoder.Codec == VideoCodec.Av1 || limited:
                    // AMF's AV1 QP runs 0-255, so constant QP would need another scale; QVBR keeps the 0-51 one. Constant
                    // QP has no ceiling either, so a size limit also moves H.264 and H.265 to QVBR (amfenc_h264.c/_hevc.c).
                    args.AddRange(new[]
                    {
                        "-usage", "transcoding", "-quality", "balanced", "-rc", "qvbr",
                        "-qvbr_quality_level", Num(q), "-b:v", Kbps(maxKbps / 2), "-maxrate", Kbps(maxKbps),
                    });
                    break;

                case EncoderKind.Amf:
                    args.AddRange(new[]
                    {
                        "-usage", "transcoding", "-quality", "balanced", "-rc", "cqp",
                        "-qp_i", Num(q), "-qp_p", Num(q + 2),
                    });
                    break;

                case EncoderKind.Qsv when limited:
                    // global_quality with a bit rate and a different maxrate selects QVBR in qsvenc.c: quality-driven,
                    // but never above maxrate.
                    args.AddRange(new[]
                    {
                        "-preset", "medium", "-global_quality", Num(q), "-b:v", Kbps(maxKbps / 2), "-maxrate", Kbps(maxKbps),
                    });
                    break;

                case EncoderKind.Qsv:
                    // global_quality without maxrate selects ICQ (intelligent constant quality) in qsvenc.c.
                    args.AddRange(new[] { "-preset", "medium", "-global_quality", Num(q) });
                    break;

                case EncoderKind.X264:
                    args.AddRange(new[] { "-preset", "veryfast", "-crf", Num(q) });
                    if (limited)
                    {
                        // Capped CRF: constant quality until the VBV ceiling, then x264 raises the quantizer.
                        args.AddRange(new[] { "-maxrate", Kbps(maxKbps), "-bufsize", Kbps(maxKbps * 2) });
                    }

                    break;

                case EncoderKind.MediaFoundation when limited:
                    // Quality mode has no ceiling; peak-constrained VBR reads bit_rate and rc_max_rate (mfenc.c).
                    args.AddRange(new[] { "-rate_control", "pc_vbr", "-b:v", Kbps(maxKbps * 2 / 3), "-maxrate", Kbps(maxKbps) });
                    break;

                case EncoderKind.MediaFoundation:
                    args.AddRange(new[] { "-rate_control", "quality", "-quality", Num(MediaFoundationQuality(q)) });
                    break;
            }

            args.AddRange(new[] { "-g", gop });
            return args;
        }

        /// <summary>Media Foundation's quality runs 0-100, higher is better: q 19/24/29/32 become 78/65/53/45.</summary>
        internal static int MediaFoundationQuality(int q) =>
            (int)Math.Max(0, Math.Min(100, Math.Round(100 - (q - 10) * 2.5, MidpointRounding.AwayFromZero)));

        private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Kbps(int value) => Num(value) + "k";
    }
}
