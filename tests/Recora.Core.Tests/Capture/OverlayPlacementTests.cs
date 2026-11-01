using Recora.Core.Capture;
using Recora.Core.Settings;
using Xunit;

namespace Recora.Core.Tests.Capture
{
    public class OverlayPlacementTests
    {
        private static readonly PixelRect Second = new PixelRect(1920, -200, 2560, 1440);

        [Theory]
        [InlineData(OverlayCorner.TopLeft, 1936, -184)]
        [InlineData(OverlayCorner.TopRight, 4184, -184)]
        [InlineData(OverlayCorner.BottomLeft, 1936, 1184)]
        [InlineData(OverlayCorner.BottomRight, 4184, 1184)]
        public void Place_PutsTheBarInTheCornerOfThatScreen(OverlayCorner corner, int x, int y)
        {
            var bar = OverlayPlacement.Place(Second, 280, 40, corner, 16);

            Assert.Equal(new PixelRect(x, y, 280, 40), bar);
        }

        [Fact]
        public void Place_OnAScreenSmallerThanTheBar_StaysInside()
        {
            var screen = new PixelRect(0, 0, 200, 30);

            var bar = OverlayPlacement.Place(screen, 280, 40, OverlayCorner.BottomRight, 16);

            Assert.Equal(new PixelRect(0, 0, 200, 30), bar);
        }
    }
}
