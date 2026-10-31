namespace Recora.Core.Capture
{
    public enum CaptureBackend
    {
        /// <summary>Desktop Duplication: GPU frames, no yellow border, constant frame rate. Default adapter only.</summary>
        Ddagrab,

        /// <summary>Windows.Graphics.Capture: GPU frames, works across adapters and for single windows.</summary>
        Gfxcapture,

        /// <summary>GDI BitBlt: CPU only and slow, the last resort (remote desktop, broken drivers).</summary>
        Gdigrab,

        /// <summary>DirectShow: webcams and capture cards, frames in system memory.</summary>
        Dshow,
    }

    /// <summary>Which capture filters the bundled ffmpeg.exe has, plus whether GPU capture already failed this session.</summary>
    public sealed record CaptureCapabilities(bool HasDdagrab, bool HasGfxcapture, bool GpuCaptureFailed = false)
    {
        public static CaptureCapabilities CpuOnly { get; } = new CaptureCapabilities(false, false, true);
    }

    public static class CaptureBackendSelector
    {
        public static CaptureBackend Select(CaptureSource source, CaptureCapabilities caps)
        {
            if (source is DeviceSource)
            {
                return CaptureBackend.Dshow;
            }

            if (caps.GpuCaptureFailed)
            {
                return CaptureBackend.Gdigrab;
            }

            var ddagrabReachable = caps.HasDdagrab && source.Monitor.DdaOutputIndex.HasValue;

            if (source is WindowSource)
            {
                // WGC follows the window when it moves and sees it under other windows; a fixed rectangle cannot.
                if (caps.HasGfxcapture)
                {
                    return CaptureBackend.Gfxcapture;
                }

                return ddagrabReachable ? CaptureBackend.Ddagrab : CaptureBackend.Gdigrab;
            }

            if (ddagrabReachable)
            {
                return CaptureBackend.Ddagrab;
            }

            return caps.HasGfxcapture ? CaptureBackend.Gfxcapture : CaptureBackend.Gdigrab;
        }
    }
}
