using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Recora.Core.Library
{
    public sealed record LibraryItem(string Path, string Name, long Bytes, DateTime Modified, TimeSpan? Duration, bool IsImage);

    /// <summary>
    /// The Thư viện page's data: recordings and screenshots in the output folder, newest first. Durations come from
    /// library.json written when Recora finished each file; files from elsewhere just show no duration.
    /// </summary>
    public static class LibraryIndex
    {
        public static readonly string[] VideoExtensions = { ".mp4", ".mkv", ".m4a", ".mp3", ".wav" };
        public static readonly string[] ImageExtensions = { ".png", ".jpg" };

        public static IReadOnlyList<LibraryItem> Scan(IEnumerable<(string Path, long Bytes, DateTime Modified)> files, IReadOnlyDictionary<string, double> durations) =>
            files
                .Where(f => IsMedia(f.Path))
                .Select(f => new LibraryItem(
                    f.Path,
                    System.IO.Path.GetFileName(f.Path),
                    f.Bytes,
                    f.Modified,
                    durations.TryGetValue(System.IO.Path.GetFileName(f.Path), out var seconds) ? TimeSpan.FromSeconds(seconds) : (TimeSpan?)null,
                    ImageExtensions.Contains(System.IO.Path.GetExtension(f.Path).ToLowerInvariant())))
                .OrderByDescending(item => item.Modified)
                .ToList();

        public static bool IsMedia(string path)
        {
            var extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
            return VideoExtensions.Contains(extension) || ImageExtensions.Contains(extension);
        }

        public static string FormatBytes(long bytes) => bytes switch
        {
            >= 1L << 30 => $"{bytes / (double)(1L << 30):0.0} GB",
            >= 1L << 20 => $"{bytes / (double)(1L << 20):0.0} MB",
            >= 1L << 10 => $"{bytes / 1024.0:0} KB",
            _ => $"{bytes} B",
        };

        public static string FormatDuration(TimeSpan duration) =>
            duration.TotalHours >= 1 ? duration.ToString(@"h\:mm\:ss") : duration.ToString(@"m\:ss");
    }

    /// <summary>library.json: file name → seconds. Only names, never full paths, so moving the folder keeps it valid.</summary>
    [DataContract]
    public sealed class DurationStore
    {
        private static readonly DataContractJsonSerializer Serializer = new DataContractJsonSerializer(
            typeof(DurationStore), new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });

        [DataMember(Name = "durations")] public Dictionary<string, double> Durations { get; set; } = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        public static DurationStore Load(string path)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var store = (DurationStore?)Serializer.ReadObject(stream) ?? new DurationStore();
                    store.Durations = new Dictionary<string, double>(store.Durations ?? new Dictionary<string, double>(), StringComparer.OrdinalIgnoreCase);
                    return store;
                }
            }
            catch (Exception error) when (error is IOException || error is SerializationException || error is UnauthorizedAccessException)
            {
                return new DurationStore();
            }
        }

        public void Record(string fileName, double seconds) => Durations[fileName] = Math.Round(seconds, 2);

        public void Save(string path)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
            using (var stream = File.Create(path))
            using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, System.Text.Encoding.UTF8, ownsStream: false, indent: true))
            {
                Serializer.WriteObject(writer, this);
            }
        }
    }
}
