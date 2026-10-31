using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Recora.App.Logging;
using Recora.App.Native;
using Recora.Core.Capture;
using Recora.Core.Encoding;

namespace Recora.App.Platform
{
    internal sealed record MonitorInfo(string Name, MonitorTarget Target, string AdapterName);

    internal static class MonitorService
    {
        /// <summary>
        /// Monitors left to right ("Màn hình 1, 2..."), each tagged with the GPU that draws it. If DXGI is unavailable
        /// (some VMs, remote desktop) the screens are still listed, just without GPU details.
        /// </summary>
        public static IReadOnlyList<MonitorInfo> List()
        {
            IReadOnlyList<DxgiOutput> outputs;
            try
            {
                outputs = Dxgi.EnumerateOutputs();
            }
            catch (Exception error) when (error is System.Runtime.InteropServices.COMException || error is DllNotFoundException || error is EntryPointNotFoundException || error is InvalidCastException)
            {
                Log.Warn("DXGI enumeration failed: " + error.Message);
                outputs = new DxgiOutput[0];
            }

            return Screen.AllScreens
                .OrderBy(s => s.Bounds.X).ThenBy(s => s.Bounds.Y)
                .Select((screen, i) =>
                {
                    var output = outputs.FirstOrDefault(o => string.Equals(o.DeviceName, screen.DeviceName, StringComparison.OrdinalIgnoreCase));
                    var bounds = new PixelRect(screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height);
                    var target = new MonitorTarget(
                        screen.DeviceName,
                        bounds,
                        output != null && output.AdapterIndex == 0 ? output.OutputIndex : (int?)null,
                        output?.Monitor.ToInt64() ?? 0,
                        output == null ? GpuVendor.Unknown : EncoderCatalog.VendorFromPciId(output.VendorId),
                        screen.Primary);
                    return new MonitorInfo($"Màn hình {i + 1}", target, output?.AdapterName ?? "?");
                })
                .ToList();
        }
    }
}
