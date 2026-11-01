using System.Runtime.InteropServices;
using System.Windows.Forms;
using Recora.App.Logging;
using Recora.App.Native;

namespace Recora.App.UI
{
    /// <summary>Keeps a helper window (picker, frame, countdown) out of every capture, from the moment it has a handle.</summary>
    internal static class WindowExclusion
    {
        /// <summary>False when Windows can't keep it out (before Windows 10 2004): the window would be in the video.</summary>
        public static bool Apply(Form form)
        {
            if (!NativeMethods.SetWindowDisplayAffinity(form.Handle, NativeMethods.WdaExcludeFromCapture))
            {
                Log.Warn($"{form.GetType().Name}: SetWindowDisplayAffinity failed ({Marshal.GetLastWin32Error()})");
                return false;
            }

            return true;
        }
    }
}
