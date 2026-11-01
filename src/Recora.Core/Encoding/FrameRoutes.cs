using System.Collections.Generic;
using Recora.Core.Capture;

namespace Recora.Core.Encoding
{
    /// <summary>How captured frames can reach an encoder: as D3D11 textures, or through system memory.</summary>
    public static class FrameRoutes
    {
        private static readonly bool[] TexturesOnly = { false };
        private static readonly bool[] TexturesThenMemory = { false, true };
        private static readonly bool[] MemoryThenTextures = { true, false };

        /// <summary>
        /// The DownloadFrames values to try, in order. AMF on some AMD APUs opens fine and then rejects BGRA textures on
        /// the first frame (SubmitInput error 18), yet the chip still encodes NV12 from memory: far lighter than falling
        /// back to x264. Other encoders keep one route, since nothing shows a second one would help them.
        /// </summary>
        public static IReadOnlyList<bool> For(CaptureBackend backend, EncoderKind kind, bool texturesKnownToFail)
        {
            var gpuFrames = backend == CaptureBackend.Ddagrab || backend == CaptureBackend.Gfxcapture;
            if (!gpuFrames || kind != EncoderKind.Amf)
            {
                return TexturesOnly;
            }

            return texturesKnownToFail ? MemoryThenTextures : TexturesThenMemory;
        }
    }
}
