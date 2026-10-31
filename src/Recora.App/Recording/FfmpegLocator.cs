using System;
using System.IO;
using Recora.Core;

namespace Recora.App.Recording
{
    internal static class FfmpegLocator
    {
        /// <summary>ffmpeg.exe shipped next to Recora.exe; RECORA_FFMPEG overrides it for development and CI.</summary>
        public static string Find()
        {
            var overridePath = Environment.GetEnvironmentVariable("RECORA_FFMPEG");
            if (!string.IsNullOrEmpty(overridePath) && File.Exists(overridePath))
            {
                return overridePath!;
            }

            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            foreach (var candidate in new[] { Path.Combine(baseDirectory, "ffmpeg.exe"), Path.Combine(baseDirectory, "ffmpeg", "ffmpeg.exe") })
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new RecoraException("ffmpeg_missing", $"ffmpeg.exe not found next to {baseDirectory}");
        }
    }
}
