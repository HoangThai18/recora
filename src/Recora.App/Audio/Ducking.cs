using System;
using System.Runtime.InteropServices;
using Recora.App.Logging;

namespace Recora.App.Audio
{
    /// <summary>
    /// Windows treats a stream on the default communications device (a headset microphone, usually) as a call and,
    /// by default, turns every other sound down by 80%: the game goes quiet in the headset while Recora records.
    /// Opting this process's session out before the stream opens keeps the game at full volume.
    /// </summary>
    internal static class Ducking
    {
        private const int ClsCtxAll = 0x17;

        public static bool OptOut(string deviceId)
        {
            object? enumeratorObject = null;
            try
            {
                enumeratorObject = new MMDeviceEnumeratorCom();
                var enumerator = (IMMDeviceEnumerator)enumeratorObject;
                if (enumerator.GetDevice(deviceId, out var device) != 0 || device == null)
                {
                    return false;
                }

                var iid = typeof(IAudioSessionManager2).GUID;
                if (device.Activate(ref iid, ClsCtxAll, IntPtr.Zero, out var managerObject) != 0 || managerObject == null)
                {
                    return false;
                }

                var manager = (IAudioSessionManager2)managerObject;
                if (manager.GetAudioSessionControl(Guid.Empty, 0, out var controlObject) != 0 || controlObject == null)
                {
                    return false;
                }

                var result = ((IAudioSessionControl2)controlObject).SetDuckingPreference(true);
                Log.Info($"ducking opt-out on {deviceId}: 0x{result:x}");
                return result == 0;
            }
            catch (Exception error) when (error is COMException || error is InvalidCastException)
            {
                Log.Warn("ducking opt-out failed: " + error.Message);
                return false;
            }
            finally
            {
                if (enumeratorObject != null)
                {
                    Marshal.ReleaseComObject(enumeratorObject);
                }
            }
        }

        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumeratorCom
        {
        }

        [ComImport]
        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig]
            int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);

            [PreserveSig]
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IntPtr endpoint);

            [PreserveSig]
            int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        }

        [ComImport]
        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig]
            int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        }

        [ComImport]
        [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionManager2
        {
            [PreserveSig]
            int GetAudioSessionControl([MarshalAs(UnmanagedType.LPStruct)] Guid sessionGuid, int streamFlags, [MarshalAs(UnmanagedType.IUnknown)] out object sessionControl);
        }

        /// <summary>IAudioSessionControl's nine methods come first; only the vtable order matters for the ones never called.</summary>
        [ComImport]
        [Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionControl2
        {
            [PreserveSig] int GetState(out int state);

            [PreserveSig] int GetDisplayName(out IntPtr name);

            [PreserveSig] int SetDisplayName(IntPtr name, IntPtr eventContext);

            [PreserveSig] int GetIconPath(out IntPtr path);

            [PreserveSig] int SetIconPath(IntPtr path, IntPtr eventContext);

            [PreserveSig] int GetGroupingParam(out Guid grouping);

            [PreserveSig] int SetGroupingParam(IntPtr grouping, IntPtr eventContext);

            [PreserveSig] int RegisterAudioSessionNotification(IntPtr client);

            [PreserveSig] int UnregisterAudioSessionNotification(IntPtr client);

            [PreserveSig] int GetSessionIdentifier(out IntPtr id);

            [PreserveSig] int GetSessionInstanceIdentifier(out IntPtr id);

            [PreserveSig] int GetProcessId(out uint processId);

            [PreserveSig] int IsSystemSoundsSession();

            [PreserveSig] int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
        }
    }
}
