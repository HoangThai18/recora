using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Recora.Core.Recording
{
    /// <summary>File names for recordings and screenshots (BR-08): <c>Recora 2026-10-31 20-15-03.mp4</c>, never overwriting.</summary>
    public static class OutputNaming
    {
        private static readonly char[] Invalid = Path.GetInvalidFileNameChars().Concat(new[] { ':', '*', '?', '"', '<', '>', '|', '/', '\\' }).Distinct().ToArray();

        public static string BaseName(DateTime localTime, string prefix = "Recora") =>
            Sanitize($"{prefix} {localTime.ToString("yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture)}");

        /// <summary>Replaces characters Windows forbids in file names, which a typed name or window title can contain.</summary>
        public static string Sanitize(string name)
        {
            var cleaned = new string(name.Select(c => Array.IndexOf(Invalid, c) >= 0 || char.IsControl(c) ? '-' : c).ToArray()).Trim().TrimEnd('.');
            return cleaned.Length == 0 ? "Recora" : cleaned;
        }

        /// <summary>First free path: <c>name.ext</c>, then <c>name (2).ext</c>, <c>name (3).ext</c>...</summary>
        public static string Unique(string folder, string baseName, string extension, Func<string, bool> exists)
        {
            var candidate = Path.Combine(folder, baseName + extension);
            for (var n = 2; exists(candidate); n++)
            {
                candidate = Path.Combine(folder, $"{baseName} ({n.ToString(CultureInfo.InvariantCulture)}){extension}");
            }

            return candidate;
        }
    }

    public static class DiskGuard
    {
        /// <summary>Below this much free space recording stops cleanly instead of producing a broken file (BR-06).</summary>
        public const long MinimumFreeBytes = 500L * 1024 * 1024;

        public static bool ShouldStop(long freeBytes) => freeBytes < MinimumFreeBytes;
    }
}
