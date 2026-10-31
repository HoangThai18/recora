using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Recora.App.Logging;
using Recora.Core.Library;

namespace Recora.App.Recording
{
    /// <summary>The output folder as the library sees it, plus library.json with the length of every file Recora saved.</summary>
    internal static class LibraryFiles
    {
        private static readonly object Gate = new object();

        public static string IndexPath(string outputFolder) => Path.Combine(outputFolder, ".recora", "library.json");

        public static void Remember(string file, TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero)
            {
                return;
            }

            try
            {
                lock (Gate)
                {
                    var index = IndexPath(Path.GetDirectoryName(file)!);
                    var store = DurationStore.Load(index);
                    store.Record(Path.GetFileName(file), duration.TotalSeconds);
                    store.Save(index);
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Log.Warn("library index not saved: " + error.Message);
            }
        }

        public static IReadOnlyList<LibraryItem> Scan(string? outputFolder)
        {
            if (string.IsNullOrEmpty(outputFolder) || !Directory.Exists(outputFolder))
            {
                return new LibraryItem[0];
            }

            var files = new DirectoryInfo(outputFolder).EnumerateFiles()
                .Where(f => LibraryIndex.IsMedia(f.Name))
                .Select(f => (f.FullName, f.Length, f.LastWriteTime));
            return LibraryIndex.Scan(files, DurationStore.Load(IndexPath(outputFolder!)).Durations);
        }
    }
}
