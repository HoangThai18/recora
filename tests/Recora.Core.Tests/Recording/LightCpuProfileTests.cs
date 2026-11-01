using Recora.Core.Capture;
using Recora.Core.Encoding;
using Recora.Core.Ffmpeg;
using Recora.Core.Recording;
using Recora.Core.Tests.Capture;

namespace Recora.Core.Tests.Recording
{
    public class LightCpuProfileTests
    {
        private static RecordingPlan Plan(EncoderKind kind, CaptureBackend backend = CaptureBackend.Ddagrab, FrameRoute route = FrameRoute.Textures, OutputScale scale = OutputScale.Original) =>
            new RecordingPlan(
                new MonitorSource(Monitors.Primary()),
                backend,
                new VideoEncoder(kind, VideoCodec.H264),
                QualityPreset.Balanced,
                Fps: 60,
                scale,
                DrawCursor: true,
                AudioLayout.None,
                Route: route);

        [Theory]
        [InlineData(60, OutputScale.Original, 30, OutputScale.P720)]
        [InlineData(144, OutputScale.P1080, 30, OutputScale.P720)]
        [InlineData(30, OutputScale.P720, 30, OutputScale.P720)]
        [InlineData(24, OutputScale.P480, 24, OutputScale.P480)]
        public void Apply_WhenGivenAChoice_NeverGoesAbove720pAt30(int fps, OutputScale scale, int expectedFps, OutputScale expectedScale)
        {
            Assert.Equal((expectedFps, expectedScale), LightCpuProfile.Apply(fps, scale));
        }

        [Fact]
        public void UsesCpu_HardwareEncoderWithGpuFrames_IsFalse()
        {
            Assert.False(Plan(EncoderKind.Amf).UsesCpu);
        }

        [Fact]
        public void UsesCpu_HardwareEncoderWithDownloadedFrames_IsTrue()
        {
            Assert.True(Plan(EncoderKind.Amf, route: FrameRoute.BgraMemory).UsesCpu);
        }

        [Theory]
        [InlineData(FrameRoute.Nv12Textures)]
        [InlineData(FrameRoute.Nv12Memory)]
        public void UsesCpu_GpuConvertedFrames_IsFalse(FrameRoute route)
        {
            Assert.False(Plan(EncoderKind.Amf, route: route).UsesCpu);
        }

        [Fact]
        public void UsesCpu_X264_IsTrue()
        {
            Assert.True(Plan(EncoderKind.X264).UsesCpu);
        }

        [Fact]
        public void UsesCpu_GdiCapture_IsTrue()
        {
            Assert.True(Plan(EncoderKind.Nvenc, CaptureBackend.Gdigrab).UsesCpu);
        }

        [Fact]
        public void VideoFilterChain_X264Scaled_UsesTheFastScaler()
        {
            var chain = RecordingCommand.VideoFilterChain(Plan(EncoderKind.X264, scale: OutputScale.P720));

            Assert.StartsWith("hwdownload,format=bgra,scale=1280:720:flags=fast_bilinear:", chain);
        }
    }
}
