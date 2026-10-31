using System.IO;
using Recora.Core.Library;

namespace Recora.Core.Tests.Library
{
    public class LibraryIndexTests
    {
        private static readonly DateTime Monday = new DateTime(2026, 10, 26, 20, 0, 0);

        private static string InFolder(string name) => Path.Combine(Path.GetTempPath(), "Rec", name);

        [Fact]
        public void Scan_KeepsMediaNewestFirst_WithDurationsWhereKnown()
        {
            var files = new[]
            {
                (InFolder("Recora 2026-10-26 20-00-00.mp4"), 1000L, Monday),
                (InFolder("notes.txt"), 10L, Monday.AddHours(3)),
                (InFolder("Recora 2026-10-27 09-15-00.PNG"), 200L, Monday.AddHours(13)),
                (InFolder("clip from phone.mkv"), 5000L, Monday.AddHours(1)),
            };
            var durations = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["recora 2026-10-26 20-00-00.mp4"] = 75.5 };

            var items = LibraryIndex.Scan(files, durations);

            Assert.Equal(new[] { "Recora 2026-10-27 09-15-00.PNG", "clip from phone.mkv", "Recora 2026-10-26 20-00-00.mp4" }, items.Select(i => i.Name));
            Assert.True(items[0].IsImage);
            Assert.Null(items[1].Duration);
            Assert.Equal(TimeSpan.FromSeconds(75.5), items[2].Duration);
        }

        [Theory]
        [InlineData(512L, "512 B")]
        [InlineData(2048L, "2 KB")]
        [InlineData(5L * 1024 * 1024 + 300 * 1024, "5.3 MB")]
        [InlineData(3L * 1024 * 1024 * 1024, "3.0 GB")]
        public void FormatBytes_PicksTheReadableUnit(long bytes, string expected) =>
            Assert.Equal(expected, LibraryIndex.FormatBytes(bytes).Replace(',', '.'));

        [Fact]
        public void FormatDuration_ShowsHoursOnlyWhenNeeded()
        {
            Assert.Equal("1:15", LibraryIndex.FormatDuration(TimeSpan.FromSeconds(75)));
            Assert.Equal("1:02:03", LibraryIndex.FormatDuration(new TimeSpan(1, 2, 3)));
        }
    }

    public sealed class DurationStoreTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "recora-tests", Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, recursive: true);
            }
        }

        [Fact]
        public void SaveThenLoad_KeepsSecondsByFileName_CaseInsensitive()
        {
            var path = Path.Combine(_folder, ".recora", "library.json");
            var store = DurationStore.Load(path);
            store.Record("Recora 2026-10-26 20-00-00.mp4", 75.456);
            store.Save(path);

            var loaded = DurationStore.Load(path);

            Assert.Equal(75.46, loaded.Durations["recora 2026-10-26 20-00-00.MP4"]);
        }

        [Fact]
        public void Load_MissingOrBroken_GivesEmptyStore()
        {
            Directory.CreateDirectory(_folder);
            var broken = Path.Combine(_folder, "library.json");
            File.WriteAllText(broken, "{ not json");

            Assert.Empty(DurationStore.Load(Path.Combine(_folder, "missing.json")).Durations);
            Assert.Empty(DurationStore.Load(broken).Durations);
        }
    }
}
