using Recora.Core.Ffmpeg;

namespace Recora.Core.Tests.Ffmpeg
{
    public class ProbeCommandTests
    {
        // Layout of fftools/opt_common.c show_filters() in FFmpeg 8.1.3: " %c%c %-17s %-10s %s".
        private const string FiltersOutput =
            "Filters:\r\n" +
            "  T.. = Timeline support\r\n" +
            "  .S. = Slice threading\r\n" +
            "  A = Audio input/output\r\n" +
            "  V = Video input/output\r\n" +
            "  N = Dynamic number and/or type of input/output\r\n" +
            "  | = Source or sink filter\r\n" +
            " .. ddagrab           |->V       Grab Windows Desktop images using Desktop Duplication API\r\n" +
            " .. scale_d3d11       V->V       Scale video using Direct3D11\r\n" +
            " TS format            V->V       Convert the input video to one of the specified pixel formats.\r\n" +
            " .. anullsrc          |->A       Null audio source, return empty audio frames.\r\n";

        private const string EncodersOutput =
            "Encoders:\r\n" +
            " V..... = Video\r\n" +
            " A..... = Audio\r\n" +
            " ------\r\n" +
            " V....D h264_nvenc           NVIDIA NVENC H.264 encoder (codec h264)\r\n" +
            " V....D libx264              libx264 H.264 / AVC / MPEG-4 AVC / MPEG-4 part 10 (codec h264)\r\n" +
            " A....D aac                  AAC (Advanced Audio Coding)\r\n";

        [Fact]
        public void ParseFilters_ReadsNamesAndSkipsLegend()
        {
            var filters = ProbeCommand.ParseFilters(FiltersOutput);

            Assert.Equal(new[] { "anullsrc", "ddagrab", "format", "scale_d3d11" }, filters.OrderBy(f => f));
        }

        [Fact]
        public void ParseEncoders_ReadsNamesAndSkipsLegend()
        {
            var encoders = ProbeCommand.ParseEncoders(EncodersOutput);

            Assert.Equal(new[] { "aac", "h264_nvenc", "libx264" }, encoders.OrderBy(e => e));
        }

        [Fact]
        public void Capabilities_ReflectWhichCaptureFiltersWereBuilt()
        {
            var caps = ProbeCommand.Capabilities(ProbeCommand.ParseFilters(FiltersOutput));

            Assert.True(caps.HasDdagrab);
            Assert.False(caps.HasGfxcapture);
        }
    }
}
