using Recora.Core.Capture;

namespace Recora.Core.Tests.Capture
{
    public class PixelRectTests
    {
        [Fact]
        public void ToEncodable_WhenOddSize_RoundsDownToEven()
        {
            var even = new PixelRect(10, 20, 801, 601).ToEncodable();

            Assert.Equal(new PixelRect(10, 20, 800, 600), even);
        }

        [Theory]
        [InlineData(159, 400)]
        [InlineData(400, 89)]
        [InlineData(160, 89)]
        public void ToEncodable_WhenBelowMinimum_ThrowsRegionTooSmall(int width, int height)
        {
            var error = Assert.Throws<RecoraException>(() => new PixelRect(0, 0, width, height).ToEncodable());

            Assert.Equal("region_too_small", error.Code);
        }

        [Fact]
        public void ToEncodable_AtMinimum_IsAccepted()
        {
            Assert.Equal(new PixelRect(0, 0, 160, 90), new PixelRect(0, 0, 160, 90).ToEncodable());
            Assert.Equal(new PixelRect(0, 0, 160, 90), new PixelRect(0, 0, 161, 91).ToEncodable());
        }

        [Fact]
        public void Intersect_WhenDisjoint_IsEmpty()
        {
            var a = new PixelRect(0, 0, 100, 100);
            var b = new PixelRect(200, 200, 50, 50);

            Assert.True(a.Intersect(b).IsEmpty);
            Assert.Equal(new PixelRect(50, 50, 50, 50), a.Intersect(new PixelRect(50, 50, 500, 500)));
        }

        [Fact]
        public void RegionSource_Area_IsClippedToItsMonitor()
        {
            var region = new RegionSource(Monitors.Primary(), new PixelRect(1800, 1000, 400, 300));

            Assert.Equal(new PixelRect(1800, 1000, 120, 80), region.Area);
        }
    }

    public class CaptureBackendSelectorTests
    {
        private static readonly CaptureCapabilities Both = new CaptureCapabilities(HasDdagrab: true, HasGfxcapture: true);

        [Fact]
        public void Select_MonitorOnDefaultAdapter_UsesDdagrab()
        {
            Assert.Equal(CaptureBackend.Ddagrab, CaptureBackendSelector.Select(new MonitorSource(Monitors.Primary()), Both));
        }

        [Fact]
        public void Select_MonitorOnOtherAdapter_UsesGfxcapture()
        {
            var source = new MonitorSource(Monitors.Second(ddaOutput: null));

            Assert.Equal(CaptureBackend.Gfxcapture, CaptureBackendSelector.Select(source, Both));
        }

        [Fact]
        public void Select_Window_PrefersGfxcaptureThenDdagrab()
        {
            var window = new WindowSource(0x1234, "Notepad", new PixelRect(100, 100, 800, 600), Monitors.Primary());

            Assert.Equal(CaptureBackend.Gfxcapture, CaptureBackendSelector.Select(window, Both));
            Assert.Equal(
                CaptureBackend.Ddagrab,
                CaptureBackendSelector.Select(window, new CaptureCapabilities(HasDdagrab: true, HasGfxcapture: false)));
        }

        [Fact]
        public void Select_WithoutGpuFilters_FallsBackToGdigrab()
        {
            var none = new CaptureCapabilities(HasDdagrab: false, HasGfxcapture: false);

            Assert.Equal(CaptureBackend.Gdigrab, CaptureBackendSelector.Select(new MonitorSource(Monitors.Primary()), none));
            Assert.Equal(CaptureBackend.Gdigrab, CaptureBackendSelector.Select(new MonitorSource(Monitors.Primary()), CaptureCapabilities.CpuOnly));
        }
    }
}
