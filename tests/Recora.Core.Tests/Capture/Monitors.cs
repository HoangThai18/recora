using Recora.Core.Capture;

namespace Recora.Core.Tests.Capture
{
    /// <summary>Typical desktops for tests: a 1080p primary and a 1440p monitor to its right.</summary>
    internal static class Monitors
    {
        public static MonitorTarget Primary(GpuVendor vendor = GpuVendor.Nvidia, int? ddaOutput = 0) =>
            new MonitorTarget(@"\\.\DISPLAY1", new PixelRect(0, 0, 1920, 1080), ddaOutput, 0x10001, vendor, IsPrimary: true);

        public static MonitorTarget Second(GpuVendor vendor = GpuVendor.Nvidia, int? ddaOutput = 1) =>
            new MonitorTarget(@"\\.\DISPLAY2", new PixelRect(1920, 0, 2560, 1440), ddaOutput, 0x10002, vendor, IsPrimary: false);
    }
}
