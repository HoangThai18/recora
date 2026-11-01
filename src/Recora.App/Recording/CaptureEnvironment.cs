using System;
using System.Collections.Generic;
using System.Linq;
using Recora.App.Logging;
using Recora.App.Platform;
using Recora.Core.Capture;
using Recora.Core.Ffmpeg;

namespace Recora.App.Recording
{
    /// <summary>What this PC and the bundled FFmpeg offer, read once at startup (a few hundred milliseconds).</summary>
    internal sealed class CaptureEnvironment
    {
        private CaptureEnvironment(string ffmpeg, string version, CaptureCapabilities capabilities, IReadOnlyCollection<string> encoders, IReadOnlyList<MonitorInfo> monitors)
        {
            FfmpegPath = ffmpeg;
            FfmpegVersion = version;
            Capabilities = capabilities;
            Encoders = encoders;
            Monitors = monitors;
        }

        public string FfmpegPath { get; }

        public string FfmpegVersion { get; }

        public CaptureCapabilities Capabilities { get; }

        public IReadOnlyCollection<string> Encoders { get; }

        public IReadOnlyList<MonitorInfo> Monitors { get; private set; }

        /// <summary>Identifies GPUs and FFmpeg build: a cached encoder probe is only trusted while this stays the same.</summary>
        public string Fingerprint =>
            string.Join("|", Monitors.Select(m => m.AdapterName).Distinct().OrderBy(n => n, StringComparer.Ordinal)) + "|" + FfmpegVersion;

        public static CaptureEnvironment Detect()
        {
            var ffmpeg = FfmpegLocator.Find();
            var version = FfmpegProcess.Run(ffmpeg, ProbeCommand.Version(), TimeSpan.FromSeconds(20))
                .Output.Split('\n').FirstOrDefault()?.Trim() ?? "unknown";
            var filters = ProbeCommand.ParseFilters(FfmpegProcess.Run(ffmpeg, ProbeCommand.Filters(), TimeSpan.FromSeconds(20)).Output);
            var encoders = ProbeCommand.ParseEncoders(FfmpegProcess.Run(ffmpeg, ProbeCommand.Encoders(), TimeSpan.FromSeconds(20)).Output);
            var monitors = MonitorService.List();

            Log.Info($"ffmpeg: {version}; ddagrab={filters.Contains("ddagrab")} gfxcapture={filters.Contains("gfxcapture")}");
            foreach (var monitor in monitors)
            {
                Log.Info($"monitor {monitor.Name}: {monitor.Target.DeviceName} {monitor.Target.Bounds} {monitor.AdapterName} ({monitor.Target.Vendor}) dda={monitor.Target.DdaOutputIndex?.ToString() ?? "-"}");
            }

            return new CaptureEnvironment(ffmpeg, version, ProbeCommand.Capabilities(filters), encoders, monitors);
        }

        /// <summary>A screen was plugged in or removed, or a game changed its resolution: sizes and DXGI indexes moved.</summary>
        public void RefreshMonitors()
        {
            var monitors = MonitorService.List();
            if (monitors.Count == 0)
            {
                return;
            }

            Monitors = monitors;
            foreach (var monitor in monitors)
            {
                Log.Info($"monitor {monitor.Name}: {monitor.Target.DeviceName} {monitor.Target.Bounds} {monitor.AdapterName} dda={monitor.Target.DdaOutputIndex?.ToString() ?? "-"}");
            }
        }

        public MonitorInfo MonitorFor(string deviceName) =>
            Monitors.FirstOrDefault(m => string.Equals(m.Target.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
            ?? Monitors.FirstOrDefault(m => m.Target.IsPrimary)
            ?? Monitors[0];
    }
}
