using System.IO;
using Recora.Core.Recording;

namespace Recora.Core.Tests.Recording
{
    public class SessionStateMachineTests
    {
        [Fact]
        public void PauseResumeStop_FollowsTheLifecycle()
        {
            var machine = new SessionStateMachine();

            machine.MoveTo(SessionState.Starting);
            machine.MoveTo(SessionState.Recording);
            Assert.True(machine.CanPause);
            machine.MoveTo(SessionState.Paused);
            Assert.True(machine.CanResume);
            machine.MoveTo(SessionState.Starting);
            machine.MoveTo(SessionState.Recording);
            machine.MoveTo(SessionState.Finalizing);
            machine.MoveTo(SessionState.Idle);

            Assert.True(machine.CanStart);
        }

        [Theory]
        [InlineData(SessionState.Idle, SessionState.Paused)]
        [InlineData(SessionState.Idle, SessionState.Recording)]
        [InlineData(SessionState.Paused, SessionState.Recording)]
        [InlineData(SessionState.Finalizing, SessionState.Recording)]
        public void MoveTo_RejectsImpossibleSteps(SessionState from, SessionState to)
        {
            Assert.False(SessionStateMachine.Allowed(from, to));
        }

        [Fact]
        public void MoveTo_InvalidStep_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => new SessionStateMachine().MoveTo(SessionState.Paused));
        }
    }

    public class OutputNamingTests
    {
        [Fact]
        public void BaseName_UsesLocalTimeWithoutColons()
        {
            Assert.Equal("Recora 2026-10-31 20-15-03", OutputNaming.BaseName(new DateTime(2026, 10, 31, 20, 15, 3)));
        }

        [Fact]
        public void Unique_AddsCounterWhenTaken()
        {
            var taken = new HashSet<string> { Path.Combine("D", "a.mp4"), Path.Combine("D", "a (2).mp4") };

            Assert.Equal(Path.Combine("D", "a (3).mp4"), OutputNaming.Unique("D", "a", ".mp4", taken.Contains));
            Assert.Equal(Path.Combine("D", "b.mp4"), OutputNaming.Unique("D", "b", ".mp4", taken.Contains));
        }

        [Theory]
        [InlineData("Trận 1: thắng?", "Trận 1- thắng-")]
        [InlineData("a/b\\c", "a-b-c")]
        [InlineData("...", "Recora")]
        public void Sanitize_ReplacesCharactersWindowsForbids(string name, string expected)
        {
            Assert.Equal(expected, OutputNaming.Sanitize(name));
        }

        [Fact]
        public void DiskGuard_StopsBelow500Megabytes()
        {
            Assert.True(DiskGuard.ShouldStop(499L * 1024 * 1024));
            Assert.False(DiskGuard.ShouldStop(2L * 1024 * 1024 * 1024));
        }
    }

    public sealed class SessionManifestTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "recora-session-" + Guid.NewGuid().ToString("N"));

        public SessionManifestTests() => Directory.CreateDirectory(_folder);

        public void Dispose() => Directory.Delete(_folder, recursive: true);

        [Fact]
        public void SaveThenLoad_RoundTrips()
        {
            var manifest = new SessionManifest
            {
                Id = "20261031-201503-ab12",
                StartedAt = "2026-10-31T20:15:03+07:00",
                OutputPath = @"D:\Video\Recora 2026-10-31 20-15-03.mp4",
                Container = "Mp4",
                Codec = "Hevc",
                Segments = { SessionManifest.SegmentName(0), SessionManifest.SegmentName(1) },
            };

            manifest.Save(_folder);
            manifest.Save(_folder);
            var loaded = SessionManifest.TryLoad(_folder);

            Assert.NotNull(loaded);
            Assert.Equal(manifest.OutputPath, loaded!.OutputPath);
            Assert.Equal(new[] { "001.mkv", "002.mkv" }, loaded.Segments);
            Assert.Equal("Hevc", loaded.Codec);
        }

        [Fact]
        public void TryLoad_BrokenOrMissing_ReturnsNull()
        {
            Assert.Null(SessionManifest.TryLoad(_folder));
            File.WriteAllText(Path.Combine(_folder, SessionManifest.FileName), "{oops");
            Assert.Null(SessionManifest.TryLoad(_folder));
        }
    }

    public class RecoveryPlannerTests
    {
        private static SessionManifest Manifest(params string[] segments)
        {
            var manifest = new SessionManifest { Id = "s", OutputPath = "out.mp4" };
            manifest.Segments.AddRange(segments);
            return manifest;
        }

        [Fact]
        public void Plan_FinishesSessionsWithDataAndSkipsEmptySegments()
        {
            var crashed = new LeftoverSession("s1", Manifest("001.mkv", "002.mkv"), new Dictionary<string, long> { ["001.mkv"] = 5_000_000, ["002.mkv"] = 0 });

            var plan = RecoveryPlanner.Plan(new[] { crashed });

            var finish = Assert.Single(plan.Finish);
            Assert.Equal(new[] { "001.mkv" }, finish.Segments);
            Assert.Empty(plan.Discard);
        }

        [Fact]
        public void Plan_DiscardsSessionsWithOnlyEmptySegments_AndIgnoresForeignFolders()
        {
            var empty = new LeftoverSession("s2", Manifest("001.mkv"), new Dictionary<string, long> { ["001.mkv"] = 200 });
            var foreign = new LeftoverSession("other", null, new Dictionary<string, long> { ["x.mkv"] = 9_000_000 });

            var plan = RecoveryPlanner.Plan(new[] { empty, foreign });

            Assert.Empty(plan.Finish);
            Assert.Equal(new[] { "s2" }, plan.Discard);
        }

        [Fact]
        public void Plan_KeepsSegmentOrderFromTheManifest()
        {
            var session = new LeftoverSession("s3", Manifest("001.mkv", "002.mkv", "003.mkv"),
                new Dictionary<string, long> { ["003.mkv"] = 4096, ["001.mkv"] = 4096, ["002.mkv"] = 4096 });

            Assert.Equal(new[] { "001.mkv", "002.mkv", "003.mkv" }, RecoveryPlanner.Plan(new[] { session }).Finish[0].Segments);
        }
    }
}
