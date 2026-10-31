using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Recora.App.Logging;

namespace Recora.App.Native
{
    /// <summary>
    /// A Job Object that kills every FFmpeg child when Recora's process ends, crash included. Without it an orphaned
    /// FFmpeg would keep capturing the screen in the background. The segment it was writing stays readable (BR-01).
    /// </summary>
    internal static class ChildProcessJob
    {
        private static readonly Lazy<IntPtr> Job = new Lazy<IntPtr>(Create);

        public static void Attach(Process process)
        {
            var job = Job.Value;
            if (job != IntPtr.Zero && !NativeMethods.AssignProcessToJobObject(job, process.Handle))
            {
                Log.Warn($"AssignProcessToJobObject failed: {Marshal.GetLastWin32Error()}");
            }
        }

        private static IntPtr Create()
        {
            var job = NativeMethods.CreateJobObject(IntPtr.Zero, null);
            if (job == IntPtr.Zero)
            {
                Log.Warn($"CreateJobObject failed: {Marshal.GetLastWin32Error()}");
                return IntPtr.Zero;
            }

            var info = new NativeMethods.JobObjectExtendedLimit();
            info.BasicLimitInformation.LimitFlags = NativeMethods.JobObjectLimitKillOnJobClose;
            var size = (uint)Marshal.SizeOf(typeof(NativeMethods.JobObjectExtendedLimit));

            if (!NativeMethods.SetInformationJobObject(job, NativeMethods.JobObjectExtendedLimitInformation, ref info, size))
            {
                Log.Warn($"SetInformationJobObject failed: {Marshal.GetLastWin32Error()}");
            }

            // The handle is never closed on purpose: closing it is what kills the children, so it must live as long as Recora.
            return job;
        }
    }
}
