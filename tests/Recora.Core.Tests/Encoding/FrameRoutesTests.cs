using Recora.Core.Capture;
using Recora.Core.Encoding;

namespace Recora.Core.Tests.Encoding
{
    public class FrameRoutesTests
    {
        private static readonly FrameRoute[] None = new FrameRoute[0];

        [Theory]
        [InlineData(CaptureBackend.Ddagrab)]
        [InlineData(CaptureBackend.Gfxcapture)]
        public void For_AmfOnGpuCapture_TriesLightestFirstAndCpuConversionLast(CaptureBackend backend)
        {
            Assert.Equal(
                new[] { FrameRoute.Textures, FrameRoute.Nv12Textures, FrameRoute.Nv12Memory, FrameRoute.BgraMemory },
                FrameRoutes.For(backend, EncoderKind.Amf, None));
        }

        [Fact]
        public void For_AmfWithRoutesKnownToFail_MovesThemLastAndStillOffersThem()
        {
            Assert.Equal(
                new[] { FrameRoute.Nv12Memory, FrameRoute.BgraMemory, FrameRoute.Textures, FrameRoute.Nv12Textures },
                FrameRoutes.For(CaptureBackend.Ddagrab, EncoderKind.Amf, new[] { FrameRoute.Textures, FrameRoute.Nv12Textures }));
        }

        [Theory]
        [InlineData(CaptureBackend.Gdigrab)]
        [InlineData(CaptureBackend.Dshow)]
        public void For_AmfOnCpuFrames_HasOneRoute(CaptureBackend backend)
        {
            Assert.Equal(new[] { FrameRoute.Textures }, FrameRoutes.For(backend, EncoderKind.Amf, new[] { FrameRoute.Textures }));
        }

        [Theory]
        [InlineData(EncoderKind.Nvenc)]
        [InlineData(EncoderKind.Qsv)]
        [InlineData(EncoderKind.X264)]
        public void For_OtherEncoders_HaveOneRoute(EncoderKind kind)
        {
            Assert.Equal(new[] { FrameRoute.Textures }, FrameRoutes.For(CaptureBackend.Ddagrab, kind, None));
        }

        [Theory]
        [InlineData("libx264", true)]
        [InlineData("h264_mf", true)]
        [InlineData("h264_amf", false)]
        [InlineData(null, false)]
        public void IsCpuEncoderName_WhenGivenName_RecognisesCpuEncoders(string? name, bool expected)
        {
            Assert.Equal(expected, EncoderCatalog.IsCpuEncoderName(name));
        }
    }
}
