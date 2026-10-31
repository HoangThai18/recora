namespace Recora.Core.Capture
{
    public enum GpuVendor
    {
        Unknown,
        Nvidia,
        Amd,
        Intel,

        /// <summary>Microsoft Basic Render Driver or a VM adapter: no hardware encoder behind it.</summary>
        Microsoft,
    }

    /// <summary>
    /// One monitor as DXGI sees it. <see cref="DdaOutputIndex"/> is the output index on the default adapter, or null
    /// when the monitor hangs off another GPU: ddagrab behind the lavfi device only reaches the default adapter.
    /// </summary>
    public sealed record MonitorTarget(
        string DeviceName,
        PixelRect Bounds,
        int? DdaOutputIndex,
        long HMonitor,
        GpuVendor Vendor,
        bool IsPrimary);

    public abstract record CaptureSource
    {
        /// <summary>The monitor the captured pixels come from; picks the GPU and so the encoder order.</summary>
        public abstract MonitorTarget Monitor { get; }

        /// <summary>What ends up in the frame, in virtual-desktop pixels, before rounding to even sizes.</summary>
        public abstract PixelRect Area { get; }
    }

    public sealed record MonitorSource(MonitorTarget Target) : CaptureSource
    {
        public override MonitorTarget Monitor => Target;

        public override PixelRect Area => Target.Bounds;
    }

    /// <summary>A rectangle in virtual-desktop pixels, clipped to one monitor (Desktop Duplication works per output).</summary>
    public sealed record RegionSource(MonitorTarget Target, PixelRect Region) : CaptureSource
    {
        public override MonitorTarget Monitor => Target;

        public override PixelRect Area => Region.Intersect(Target.Bounds);
    }

    /// <summary>
    /// A top-level window. <see cref="Area"/> is clipped to its monitor: that is all a rectangle-based fallback can grab,
    /// while gfxcapture records the window itself at its real size.
    /// </summary>
    public sealed record WindowSource(long Hwnd, string Title, PixelRect Bounds, MonitorTarget Target) : CaptureSource
    {
        public override MonitorTarget Monitor => Target;

        public override PixelRect Area => Bounds.Intersect(Target.Bounds);
    }

    /// <summary>
    /// A webcam or capture card through DirectShow. <see cref="Size"/> is what the device usually sends (used to cap
    /// the bitrate); the frames themselves are evened and scaled by the filter whatever size really arrives.
    /// <see cref="Target"/> only decides which GPU encodes: the primary monitor's.
    /// </summary>
    public sealed record DeviceSource(string DeviceName, PixelRect Size, MonitorTarget Target) : CaptureSource
    {
        public override MonitorTarget Monitor => Target;

        public override PixelRect Area => Size;
    }
}
