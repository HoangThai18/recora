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
        public void Newer_NothingWhenUpToDate() => Assert.Null(UpdateCheck.Newer(Releases, new Version(0, 2, 1)));

        [Theory]
        [InlineData("")]
        [InlineData("{\"message\":\"API rate limit exceeded\"}")]
        [InlineData("<html>")]
        [InlineData("[null]")]
        public void Newer_GarbageOrErrors_MeanNoUpdate(string json) => Assert.Null(UpdateCheck.Newer(json, new Version(0, 1, 0)));
    }
}
