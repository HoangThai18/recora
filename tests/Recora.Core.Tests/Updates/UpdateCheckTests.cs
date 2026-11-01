using Recora.Core.Updates;

namespace Recora.Core.Tests.Updates
{
    public class UpdateCheckTests
    {
        private const string Releases = """
            [
              {"tag_name":"recora-0.3.0","draft":true,"prerelease":false},
              {"tag_name":"recora-0.2.1","draft":false,"prerelease":false,"assets":[]},
              {"tag_name":"recora-0.2.0-beta","draft":false,"prerelease":false},
              {"tag_name":"recora-0.2.5","draft":false,"prerelease":true},
              {"tag_name":"hubly-9.0.0","draft":false,"prerelease":false},
              {"tag_name":"recora-0.1.0","draft":false,"prerelease":false}
            ]
            """;

        [Fact]
        public void Newer_PicksTheHighestPublishedRecoraRelease()
        {
            var update = UpdateCheck.Newer(Releases, new Version(0, 1, 0));

            Assert.NotNull(update);
            Assert.Equal(new Version(0, 2, 1), update!.Version);
            Assert.Equal("recora-0.2.1", update.Tag);
        }

        [Fact]
        public void Newer_FindsTheInstallerAndTheChecksumsOfThatRelease()
        {
            const string json = """
                [{"tag_name":"recora-0.3.1","draft":false,"prerelease":false,"assets":[
                  {"name":"recora-0.3.1-windows-x64-portable.zip","size":5800000,"browser_download_url":"https://x/portable.zip"},
                  {"name":"recora-0.3.1-windows-x64-setup.exe","size":6200000,"browser_download_url":"https://x/setup.exe"},
                  {"name":"SHA256SUMS.txt","size":300,"browser_download_url":"https://x/SHA256SUMS.txt"}]}]
                """;

            var update = UpdateCheck.Newer(json, new Version(0, 3, 0));

            Assert.NotNull(update);
            Assert.True(update!.CanInstall);
            Assert.Equal("recora-0.3.1-windows-x64-setup.exe", update.SetupName);
            Assert.Equal("https://x/setup.exe", update.SetupUrl);
            Assert.Equal(6200000, update.SetupBytes);
            Assert.Equal("https://x/SHA256SUMS.txt", update.SumsUrl);
        }

        [Fact]
        public void Newer_ReleaseWithoutInstallerOrChecksums_CannotInstall()
        {
            var update = UpdateCheck.Newer(Releases, new Version(0, 1, 0));

            Assert.False(update!.CanInstall);
        }

        [Fact]
        public void Newer_NothingWhenUpToDate() => Assert.Null(UpdateCheck.Newer(Releases, new Version(0, 2, 1)));

        [Theory]
        [InlineData("")]
        [InlineData("{\"message\":\"API rate limit exceeded\"}")]
        [InlineData("<html>")]
        [InlineData("[null]")]
        public void Newer_GarbageOrErrors_MeanNoUpdate(string json) => Assert.Null(UpdateCheck.Newer(json, new Version(0, 1, 0)));
    }
}
