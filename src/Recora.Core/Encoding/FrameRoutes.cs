using System.Collections.Generic;
using System.Linq;
using Recora.Core.Capture;

namespace Recora.Core.Encoding
{
    /// <summary>How captured frames reach a GPU encoder, lightest first.</summary>
    public enum FrameRoute
    {
        /// <summary>The capture's BGRA D3D11 textures, untouched.</summary>
        Textures,

        /// <summary>The GPU converts to NV12 and the encoder takes the texture: the frame still never leaves the GPU.</summary>
        Nv12Textures,

        /// <summary>The GPU converts and scales; only the finished NV12 frame is copied to system memory.</summary>
        Nv12Memory,

        /// <summary>The BGRA frame is copied to system memory and the CPU converts and scales it: the heaviest route.</summary>
        BgraMemory,
    }

    public static class FrameRoutes
    {
        private static readonly FrameRoute[] TexturesOnly = { FrameRoute.Textures };

        private static readonly FrameRoute[] Lightest =
        {
            FrameRoute.Textures, FrameRoute.Nv12Textures, FrameRoute.Nv12Memory, FrameRoute.BgraMemory,
        };

        /// <summary>
        /// The routes to try, in order. AMF on some AMD APUs opens fine and then rejects BGRA textures on the first frame
        /// (SubmitInput error 18). Those chips share everything with the game, so each step down costs frames: the routes
        /// a PC is known to refuse wait at the end, in case a driver update changed that. Other encoders keep one route,
        /// since nothing shows a second one would help them.
        /// </summary>
        public static IReadOnlyList<FrameRoute> For(CaptureBackend backend, EncoderKind kind, IReadOnlyCollection<FrameRoute> knownToFail)
        {
            var gpuFrames = backend == CaptureBackend.Ddagrab || backend == CaptureBackend.Gfxcapture;
            if (!gpuFrames || kind != EncoderKind.Amf)
            {
                return TexturesOnly;
            }

            return Lightest.Where(route => !knownToFail.Contains(route)).Concat(Lightest.Where(knownToFail.Contains)).ToList();
        }

        /// <summary>What the log calls a route, after the encoder's name.</summary>
        public static string Label(FrameRoute route) => route switch
        {
            FrameRoute.Nv12Textures => " (NV12 textures)",
            FrameRoute.Nv12Memory => " (NV12 system memory)",
            FrameRoute.BgraMemory => " (system memory)",
            _ => string.Empty,
        };
    }
}
