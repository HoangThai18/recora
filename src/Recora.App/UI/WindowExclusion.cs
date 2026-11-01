using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Recora.App.Logging;
using Recora.App.Native;

namespace Recora.App.UI
{
    /// <summary>Keeps a helper window (picker, frame, countdown) out of every capture, from the moment it has a handle.</summary>
    internal static class WindowExclusion
    {
        /// <summary>
        /// Windows 10 2004 (build 19041) and later. Older builds accept the flag but treat it as WDA_MONITOR: the call
        /// succeeds and the window turns into a black box in the video, so it must not be asked for there.
        /// </summary>
        public static bool Supported => Environment.OSVersion.Version.Major > 10
            || (Environment.OSVersion.Version.Major == 10 && Environment.OSVersion.Version.Build >= 19041);

        /// <summary>False when Windows can't keep it out (before Windows 10 2004): the window would be in the video.</summary>
        public static bool Apply(Form form)
        {
            if (!Supported)
            {
                Log.Warn($"{form.GetType().Name}: Windows build {Environment.OSVersion.Version.Build} can't exclude windows from capture");
                return false;
            }

            if (!NativeMethods.SetWindowDisplayAffinity(form.Handle, NativeMethods.WdaExcludeFromCapture))
            {
                Log.Warn($"{form.GetType().Name}: SetWindowDisplayAffinity failed ({Marshal.GetLastWin32Error()})");
                return false;
            }

            return true;
        }
    }
}
