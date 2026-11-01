using Recora.Core.Capture;
using Recora.Core.Encoding;

namespace Recora.Core.Tests.Encoding
{
    public class FrameRoutesTests
    {
        [Theory]
        [InlineData(CaptureBackend.Ddagrab)]
        [InlineData(CaptureBackend.Gfxcapture)]
        public void For_AmfOnGpuCapture_TriesTexturesThenMemory(CaptureBackend backend)
        {
            Assert.Equal(new[] { false, true }, FrameRoutes.For(backend, EncoderKind.Amf, texturesKnownToFail: false));
        }

        [Fact]
        public void For_AmfWhenTexturesKnownToFail_TriesMemoryFirst()
        {
            Assert.Equal(new[] { true, false }, FrameRoutes.For(CaptureBackend.Ddagrab, EncoderKind.Amf, texturesKnownToFail: true));
        }

        [Theory]
        [InlineData(CaptureBackend.Gdigrab)]
        [InlineData(CaptureBackend.Dshow)]
        public void For_AmfOnCpuFrames_HasOneRoute(CaptureBackend backend)
        {
            Assert.Equal(new[] { false }, FrameRoutes.For(backend, EncoderKind.Amf, texturesKnownToFail: true));
        }

        [Theory]
        [InlineData(EncoderKind.Nvenc)]
        [InlineData(EncoderKind.Qsv)]
        [InlineData(EncoderKind.X264)]
        public void For_OtherEncoders_HaveOneRoute(EncoderKind kind)
        {
            Assert.Equal(new[] { false }, FrameRoutes.For(CaptureBackend.Ddagrab, kind, texturesKnownToFail: false));
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
