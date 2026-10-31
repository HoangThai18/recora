using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Recora.App.Native
{
    internal sealed record DxgiOutput(
        int AdapterIndex,
        int OutputIndex,
        string AdapterName,
        int VendorId,
        string DeviceName,
        int Left,
        int Top,
        int Right,
        int Bottom,
        IntPtr Monitor);

    /// <summary>
    /// Minimal DXGI enumeration: which adapter and output index drives each monitor. ddagrab numbers outputs on the
    /// default adapter only, so this decides whether a monitor can use it. Only the vtable slots we call are typed;
    /// the others are placeholders that keep the order right.
    /// </summary>
    internal static class Dxgi
    {
        public static IReadOnlyList<DxgiOutput> EnumerateOutputs()
        {
            var outputs = new List<DxgiOutput>();
            var iid = typeof(IDXGIFactory1).GUID;
            if (CreateDXGIFactory1(ref iid, out var factory) < 0)
            {
                return outputs;
            }

            try
            {
                for (var a = 0; factory.EnumAdapters1((uint)a, out var adapter) >= 0; a++)
                {
                    try
                    {
                        adapter.GetDesc1(out var adapterDesc);
                        for (var o = 0; adapter.EnumOutputs((uint)o, out var output) >= 0; o++)
                        {
                            try
                            {
                                output.GetDesc(out var desc);
                                if (desc.AttachedToDesktop)
                                {
                                    outputs.Add(new DxgiOutput(
                                        a, o, adapterDesc.Description, (int)adapterDesc.VendorId, desc.DeviceName,
                                        desc.DesktopCoordinates.Left, desc.DesktopCoordinates.Top,
                                        desc.DesktopCoordinates.Right, desc.DesktopCoordinates.Bottom, desc.Monitor));
                                }
                            }
                            finally
                            {
                                Marshal.ReleaseComObject(output);
                            }
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(adapter);
                    }
                }
            }
            finally
            {
                Marshal.ReleaseComObject(factory);
            }

            return outputs;
        }

        [DllImport("dxgi.dll")]
        private static extern int CreateDXGIFactory1(ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IDXGIFactory1 factory);

        [ComImport, Guid("770aae78-f26f-4dba-a829-253c83d1b387"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDXGIFactory1
        {
            void SetPrivateData();
            void SetPrivateDataInterface();
            void GetPrivateData();
            void GetParent();
            void EnumAdapters();
            void MakeWindowAssociation();
            void GetWindowAssociation();
            void CreateSwapChain();
            void CreateSoftwareAdapter();

            [PreserveSig]
            int EnumAdapters1(uint index, [MarshalAs(UnmanagedType.Interface)] out IDXGIAdapter1 adapter);
        }

        [ComImport, Guid("29038f61-3839-4626-91fd-086879011a05"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDXGIAdapter1
        {
            void SetPrivateData();
            void SetPrivateDataInterface();
            void GetPrivateData();
            void GetParent();

            [PreserveSig]
            int EnumOutputs(uint index, [MarshalAs(UnmanagedType.Interface)] out IDXGIOutput output);

            void GetDesc();
            void CheckInterfaceSupport();

            [PreserveSig]
            int GetDesc1(out AdapterDesc1 desc);
        }

        [ComImport, Guid("ae02eedb-c735-4690-8d52-5a8dc20213aa"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDXGIOutput
        {
            void SetPrivateData();
            void SetPrivateDataInterface();
            void GetPrivateData();
            void GetParent();

            [PreserveSig]
            int GetDesc(out OutputDesc desc);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct AdapterDesc1
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Description;
            public uint VendorId;
            public uint DeviceId;
            public uint SubSysId;
            public uint Revision;
            public UIntPtr DedicatedVideoMemory;
            public UIntPtr DedicatedSystemMemory;
            public UIntPtr SharedSystemMemory;
            public uint LuidLow;
            public int LuidHigh;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OutputDesc
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            public Rect DesktopCoordinates;
            [MarshalAs(UnmanagedType.Bool)]
            public bool AttachedToDesktop;
            public int Rotation;
            public IntPtr Monitor;
        }
    }
}
