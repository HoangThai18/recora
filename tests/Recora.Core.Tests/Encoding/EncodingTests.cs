using Recora.Core.Capture;
using Recora.Core.Encoding;

namespace Recora.Core.Tests.Encoding
{
    public class EncoderCatalogTests
    {
        [Theory]
        [InlineData(GpuVendor.Nvidia, EncoderKind.Nvenc)]
        [InlineData(GpuVendor.Amd, EncoderKind.Amf)]
        [InlineData(GpuVendor.Intel, EncoderKind.Qsv)]
        [InlineData(GpuVendor.Unknown, EncoderKind.Nvenc)]
        public void KindOrder_StartsWithDisplayGpuEncoder(GpuVendor vendor, EncoderKind first)
        {
            Assert.Equal(first, EncoderCatalog.KindOrder(vendor)[0]);
        }

        [Theory]
        [InlineData(GpuVendor.Nvidia)]
        [InlineData(GpuVendor.Amd)]
        [InlineData(GpuVendor.Intel)]
        [InlineData(GpuVendor.Unknown)]
        [InlineData(GpuVendor.Microsoft)]
        public void KindOrder_EndsWithCpuEncoders_X264BeforeMediaFoundation(GpuVendor vendor)
        {
            var order = EncoderCatalog.KindOrder(vendor);

            Assert.Equal(new[] { EncoderKind.X264, EncoderKind.MediaFoundation }, order.Skip(order.Count - 2));
            Assert.Equal(order.Count, order.Distinct().Count());
        }

        [Fact]
        public void KindOrder_ForVirtualAdapter_SkipsHardwareEncoders()
        {
            Assert.DoesNotContain(EncoderCatalog.KindOrder(GpuVendor.Microsoft), EncoderCatalog.IsHardware);
        }

        [Fact]
        public void Candidates_ForHevc_GpuHevcThenGpuH264ThenCpuWithX264First()
        {
            var candidates = EncoderCatalog.Candidates(GpuVendor.Nvidia, VideoCodec.Hevc);

            Assert.Equal(
                new[] { "hevc_nvenc", "hevc_qsv", "hevc_amf", "h264_nvenc", "h264_qsv", "h264_amf", "libx264", "hevc_mf", "h264_mf" },
                candidates.Select(c => c.FfmpegName));
        }

        [Fact]
        public void Candidates_NeverTryACpuEncoderBeforeAGpuOne()
        {
            foreach (var vendor in new[] { GpuVendor.Nvidia, GpuVendor.Amd, GpuVendor.Intel, GpuVendor.Unknown })
            {
                foreach (var codec in new[] { VideoCodec.H264, VideoCodec.Hevc, VideoCodec.Av1 })
                {
                    var candidates = EncoderCatalog.Candidates(vendor, codec);
                    var firstCpu = candidates.ToList().FindIndex(c => !c.IsHardware);
                    Assert.All(candidates.Skip(firstCpu), c => Assert.False(c.IsHardware));
                    Assert.Equal(EncoderKind.X264, candidates[firstCpu].Kind);
                }
            }
        }

        [Fact]
        public void Candidates_SkipEncodersMissingFromTheBuild()
        {
            var available = new[] { "h264_qsv", "libx264" };

            var candidates = EncoderCatalog.Candidates(GpuVendor.Nvidia, VideoCodec.H264, available);

            Assert.Equal(new[] { "h264_qsv", "libx264" }, candidates.Select(c => c.FfmpegName));
        }

        [Theory]
        [InlineData(0x10DE, GpuVendor.Nvidia)]
        [InlineData(0x1002, GpuVendor.Amd)]
        [InlineData(0x8086, GpuVendor.Intel)]
        [InlineData(0x1414, GpuVendor.Microsoft)]
        [InlineData(0x5143, GpuVendor.Unknown)]
        public void VendorFromPciId_MapsKnownVendors(int pciId, GpuVendor expected)
        {
            Assert.Equal(expected, EncoderCatalog.VendorFromPciId(pciId));
        }

        [Fact]
        public void FfmpegName_WhenUnsupported_ThrowsOnUse()
        {
            Assert.Null(EncoderCatalog.FfmpegName(EncoderKind.X264, VideoCodec.Av1));
            var error = Assert.Throws<RecoraException>(() => new VideoEncoder(EncoderKind.X264, VideoCodec.Av1).FfmpegName);
            Assert.Equal("encoder_unsupported", error.Code);
        }
    }

    public class VideoEncoderArgsTests
    {
        private static VideoEncodeOptions Options(EncoderKind kind, VideoCodec codec, QualityPreset quality = QualityPreset.Balanced) =>
            new VideoEncodeOptions(new VideoEncoder(kind, codec), quality, Fps: 60, Width: 1920, Height: 1080);

        public static IEnumerable<object[]> AllSupportedCombinations() =>
            from kind in Enum.GetValues(typeof(EncoderKind)).Cast<EncoderKind>()
            from codec in Enum.GetValues(typeof(VideoCodec)).Cast<VideoCodec>()
            from quality in Enum.GetValues(typeof(QualityPreset)).Cast<QualityPreset>()
            where EncoderCatalog.FfmpegName(kind, codec) != null
            select new object[] { kind, codec, quality };

        [Theory]
        [MemberData(nameof(AllSupportedCombinations))]
        public void Build_EveryCombination_StaysInsideTheEncoderScale(EncoderKind kind, VideoCodec codec, QualityPreset quality)
        {
            var args = Args(Options(kind, codec, quality));

            Assert.Equal(EncoderCatalog.FfmpegName(kind, codec), Value(args, "-c:v"));
            Assert.Equal("120", Value(args, "-g"));

            var (option, max) = kind switch
            {
                EncoderKind.Nvenc => ("-cq", codec == VideoCodec.Av1 ? 63 : 51),
                EncoderKind.Amf => codec == VideoCodec.Av1 ? ("-qvbr_quality_level", 51) : ("-qp_i", 51),
                EncoderKind.Qsv => ("-global_quality", 51),
                EncoderKind.X264 => ("-crf", 51),
                _ => ("-quality", 100),
            };

            var value = int.Parse(Value(args, option));
            Assert.InRange(value, 1, max);
        }

        [Fact]
        public void Build_NvencAv1_UsesThe0To63Scale()
        {
            var args = Args(Options(EncoderKind.Nvenc, VideoCodec.Av1));

            Assert.Equal("30", Value(args, "-cq"));
            Assert.Equal("24", Value(Args(Options(EncoderKind.Nvenc, VideoCodec.H264)), "-cq"));
        }

        [Fact]
        public void Build_AmfAv1_UsesQvbrNotConstantQp()
        {
            var args = Args(Options(EncoderKind.Amf, VideoCodec.Av1));

            Assert.Equal("qvbr", Value(args, "-rc"));
            Assert.DoesNotContain("-qp_i", args);
            Assert.Equal("cqp", Value(Args(Options(EncoderKind.Amf, VideoCodec.Hevc)), "-rc"));
        }

        [Fact]
        public void Build_Qsv_UsesIcqWithoutMaxrate()
        {
            var args = Args(Options(EncoderKind.Qsv, VideoCodec.H264));

            Assert.Equal("24", Value(args, "-global_quality"));
            Assert.DoesNotContain("-maxrate", args);
        }

        [Fact]
        public void Build_Nvenc_CapsPeakBitrate()
        {
            var args = Args(Options(EncoderKind.Nvenc, VideoCodec.H264));

            Assert.Equal("21151k", Value(args, "-maxrate"));
            Assert.Equal("42302k", Value(args, "-bufsize"));
        }

        [Fact]
        public void MaxBitrateKbps_ShrinksForNewerCodecsAndClampsSmallFrames()
        {
            var h264 = VideoEncoderArgs.MaxBitrateKbps(Options(EncoderKind.Nvenc, VideoCodec.H264));
            var hevc = VideoEncoderArgs.MaxBitrateKbps(Options(EncoderKind.Nvenc, VideoCodec.Hevc));
            var tiny = VideoEncoderArgs.MaxBitrateKbps(
                new VideoEncodeOptions(new VideoEncoder(EncoderKind.X264, VideoCodec.H264), QualityPreset.Small, 24, 160, 90));

            Assert.True(hevc < h264);
            Assert.Equal(1500, tiny);
        }

        [Theory]
        [InlineData(QualityPreset.High, 78)]
        [InlineData(QualityPreset.Balanced, 65)]
        [InlineData(QualityPreset.Small, 53)]
        public void Build_MediaFoundation_MapsToItsHigherIsBetterScale(QualityPreset quality, int expected)
        {
            var args = Args(Options(EncoderKind.MediaFoundation, VideoCodec.H264, quality));

            Assert.Equal(expected.ToString(), Value(args, "-quality"));
        }

        [Fact]
        public void Presets_AreOrderedFromSharpestToSmallest()
        {
            Assert.True(VideoEncoderArgs.BaseQuality(QualityPreset.High) < VideoEncoderArgs.BaseQuality(QualityPreset.Balanced));
            Assert.True(VideoEncoderArgs.BaseQuality(QualityPreset.Balanced) < VideoEncoderArgs.BaseQuality(QualityPreset.Small));
            Assert.True(VideoEncoderArgs.AudioBitrateKbps(QualityPreset.High) > VideoEncoderArgs.AudioBitrateKbps(QualityPreset.Small));
        }

        private static List<string> Args(VideoEncodeOptions options) => VideoEncoderArgs.Build(options).ToList();

        private static string Value(List<string> args, string option)
        {
            var index = args.IndexOf(option);
            Assert.True(index >= 0 && index + 1 < args.Count, $"{option} missing in: {string.Join(" ", args)}");
            return args[index + 1];
        }
    }
}
