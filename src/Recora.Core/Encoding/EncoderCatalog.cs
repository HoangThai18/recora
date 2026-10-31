using System.Collections.Generic;
using System.Linq;
using Recora.Core.Capture;

namespace Recora.Core.Encoding
{
    public enum EncoderKind
    {
        Nvenc,
        Amf,
        Qsv,
        X264,

        /// <summary>Microsoft's software MFT: weaker than x264 for H.264, but the only CPU route to H.265.</summary>
        MediaFoundation,
    }

    public enum VideoCodec
    {
        H264,
        Hevc,
        Av1,
    }

    public readonly record struct VideoEncoder(EncoderKind Kind, VideoCodec Codec)
    {
        public string FfmpegName => EncoderCatalog.FfmpegName(Kind, Codec)
            ?? throw new RecoraException("encoder_unsupported", $"{Kind} cannot encode {Codec}");

        public bool IsHardware => EncoderCatalog.IsHardware(Kind);

        public override string ToString() => FfmpegName;
    }

    public static class EncoderCatalog
    {
        public static string? FfmpegName(EncoderKind kind, VideoCodec codec) => (kind, codec) switch
        {
            (EncoderKind.Nvenc, VideoCodec.H264) => "h264_nvenc",
            (EncoderKind.Nvenc, VideoCodec.Hevc) => "hevc_nvenc",
            (EncoderKind.Nvenc, VideoCodec.Av1) => "av1_nvenc",
            (EncoderKind.Amf, VideoCodec.H264) => "h264_amf",
            (EncoderKind.Amf, VideoCodec.Hevc) => "hevc_amf",
            (EncoderKind.Amf, VideoCodec.Av1) => "av1_amf",
            (EncoderKind.Qsv, VideoCodec.H264) => "h264_qsv",
            (EncoderKind.Qsv, VideoCodec.Hevc) => "hevc_qsv",
            (EncoderKind.Qsv, VideoCodec.Av1) => "av1_qsv",
            (EncoderKind.X264, VideoCodec.H264) => "libx264",
            (EncoderKind.MediaFoundation, VideoCodec.H264) => "h264_mf",
            (EncoderKind.MediaFoundation, VideoCodec.Hevc) => "hevc_mf",
            _ => null,
        };

        public static bool IsHardware(EncoderKind kind) =>
            kind == EncoderKind.Nvenc || kind == EncoderKind.Amf || kind == EncoderKind.Qsv;

        public static string DisplayName(EncoderKind kind) => kind switch
        {
            EncoderKind.Nvenc => "NVIDIA NVENC",
            EncoderKind.Amf => "AMD AMF",
            EncoderKind.Qsv => "Intel Quick Sync",
            EncoderKind.X264 => "CPU (x264)",
            _ => "Windows Media Foundation",
        };

        public static GpuVendor VendorFromPciId(int vendorId) => vendorId switch
        {
            0x10DE => GpuVendor.Nvidia,
            0x1002 or 0x1022 => GpuVendor.Amd,
            0x8086 => GpuVendor.Intel,
            0x1414 => GpuVendor.Microsoft,
            _ => GpuVendor.Unknown,
        };

        /// <summary>
        /// Encoder kinds to try, best first. The GPU that draws the captured monitor goes first because hardware encoders
        /// only take textures from their own GPU (BR-02); the CPU encoders close the list.
        /// </summary>
        public static IReadOnlyList<EncoderKind> KindOrder(GpuVendor displayVendor)
        {
            var hardware = displayVendor switch
            {
                GpuVendor.Nvidia => new[] { EncoderKind.Nvenc, EncoderKind.Qsv, EncoderKind.Amf },
                GpuVendor.Amd => new[] { EncoderKind.Amf, EncoderKind.Qsv, EncoderKind.Nvenc },
                GpuVendor.Intel => new[] { EncoderKind.Qsv, EncoderKind.Nvenc, EncoderKind.Amf },
                // A VM or the basic display driver has no encoder hardware; probing it only wastes seconds.
                GpuVendor.Microsoft => new EncoderKind[0],
                _ => new[] { EncoderKind.Nvenc, EncoderKind.Amf, EncoderKind.Qsv },
            };

            return hardware.Concat(new[] { EncoderKind.X264, EncoderKind.MediaFoundation }).ToList();
        }

        /// <summary>
        /// Encoders to try, in three tiers: GPU encoders for <paramref name="codec"/>, then GPU encoders for H.264 (a GPU
        /// recording in H.264 beats a CPU one in H.265 that makes the game stutter), then the CPU encoders, x264 first
        /// (BR-02). <paramref name="available"/> filters by what ffmpeg.exe was built with.
        /// </summary>
        public static IReadOnlyList<VideoEncoder> Candidates(
            GpuVendor displayVendor,
            VideoCodec codec,
            IReadOnlyCollection<string>? available = null)
        {
            var codecs = codec == VideoCodec.H264 ? new[] { codec } : new[] { codec, VideoCodec.H264 };
            var kinds = KindOrder(displayVendor);
            var gpu = codecs.SelectMany(c => kinds.Where(IsHardware).Select(kind => (kind, codec: c)));
            var cpu = kinds.Where(kind => !IsHardware(kind)).SelectMany(kind => codecs.Select(c => (kind, codec: c)));

            return gpu.Concat(cpu)
                .Where(p => FfmpegName(p.kind, p.codec) is string name && (available == null || available.Contains(name)))
                .Select(p => new VideoEncoder(p.kind, p.codec))
                .ToList();
        }
    }
}
