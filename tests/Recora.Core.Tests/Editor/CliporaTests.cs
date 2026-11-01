using System;
using System.Linq;
using Recora.Core.Editor;

namespace Recora.Core.Tests.Editor
{
    public class CliporaTests
    {
        private const string Releases = """
            [
              {"tag_name":"wavely-0.2.0-06db0bd","draft":false,"prerelease":false,"assets":[]},
              {"tag_name":"clipora-0.5.0-aaaaaaa","draft":true,"prerelease":false,"assets":[]},
              {"tag_name":"clipora-0.4.0-dd525c9","draft":false,"prerelease":false,"assets":[
                {"name":"clipora-0.4.0-windows-x64-setup.exe","size":58000000,"browser_download_url":"https://x/setup.exe"},
                {"name":"clipora-0.4.0-windows-x64-portable.zip","size":70000000,"browser_download_url":"https://x/portable.zip"},
                {"name":"SHA256SUMS.txt","size":400,"browser_download_url":"https://x/SHA256SUMS.txt"}]},
              {"tag_name":"clipora-0.3.0-5231852","draft":false,"prerelease":false,"assets":[
                {"name":"clipora-0.3.0-windows-x64-portable.zip","size":60000000,"browser_download_url":"https://x/old.zip"},
                {"name":"SHA256SUMS.txt","size":400,"browser_download_url":"https://x/old-sums"}]},
              {"tag_name":"clipora-0.4.1-bbbbbbb","draft":false,"prerelease":false,"assets":[
                {"name":"clipora-0.4.1-macos-universal.dmg","size":1,"browser_download_url":"https://x/mac.dmg"}]}
            ]
            """;

        [Fact]
        public void Pick_TakesTheNewestPublishedReleaseThatHasAWindowsZip()
        {
            var package = CliporaRelease.Pick(Releases);

            Assert.NotNull(package);
            Assert.Equal("clipora-0.4.0-dd525c9", package!.Tag);
            Assert.Equal(new Version(0, 4, 0), package.Version);
            Assert.Equal("clipora-0.4.0-windows-x64-portable.zip", package.ZipName);
            Assert.Equal("https://x/portable.zip", package.ZipUrl);
            Assert.Equal(70000000, package.ZipBytes);
            Assert.Equal("https://x/SHA256SUMS.txt", package.SumsUrl);
        }

        [Theory]
        [InlineData("")]
        [InlineData("{\"message\":\"API rate limit exceeded\"}")]
        [InlineData("[null]")]
        [InlineData("[{\"tag_name\":\"wavely-1.0.0\",\"assets\":[]}]")]
        public void Pick_NothingUsable_IsNull(string json) => Assert.Null(CliporaRelease.Pick(json));

        [Theory]
        [InlineData("clipora-0.4.0-dd525c9", "0.4.0")]
        [InlineData("clipora-1.2.3", "1.2.3")]
        [InlineData("clipora-x-1", null)]
        [InlineData("wavely-0.2.0", null)]
        public void ParseVersion_ReadsTheNumberAfterThePrefix(string tag, string? expected) =>
            Assert.Equal(expected, CliporaRelease.ParseVersion(tag)?.ToString());

        [Fact]
        public void Sha256Sums_FindsTheExactFileName()
        {
            const string sums = "3f93781  short\n"
                + "a02722ad78fff8586b35395054524c3f34e31d423fad8a58f0ff4605b89e3550  clipora-0.4.0-windows-x64-portable.zip\r\n"
                + "CDC464B6AB6EAABD6B6AC6A855F43327E619497D8A9451C7DA1A54C7A051844A *clipora-0.4.0-windows-x64-setup.exe\n";

            Assert.Equal("a02722ad78fff8586b35395054524c3f34e31d423fad8a58f0ff4605b89e3550", Sha256Sums.Find(sums, "clipora-0.4.0-windows-x64-portable.zip"));
            Assert.Equal("cdc464b6ab6eaabd6b6ac6a855f43327e619497d8a9451c7da1a54c7a051844a", Sha256Sums.Find(sums, "clipora-0.4.0-windows-x64-setup.exe"));
            Assert.Null(Sha256Sums.Find(sums, "clipora-0.4.0-windows-x64.msi"));
            Assert.Null(Sha256Sums.Find(sums, "short"));
        }

        [Fact]
        public void Control_BuildsOneJsonObjectPerRequest_WithEscapedPaths()
        {
            var import = CliporaControl.Execute(3, "file.import", CliporaControl.ImportParams(new[] { @"C:\Users\Bạn\Videos\Recora 1 ""hay"".mp4" }));

            Assert.Equal(
                "{\"id\":3,\"method\":\"engine.execute\",\"params\":{\"command\":\"file.import\",\"params\":{\"paths\":[\"C:\\\\Users\\\\Bạn\\\\Videos\\\\Recora 1 \\\"hay\\\".mp4\"]}}}",
                import);
            Assert.Equal("{\"id\":4,\"method\":\"app.quit\"}", CliporaControl.Method(4, "app.quit"));
            Assert.Equal("{\"items\":[7,9]}", CliporaControl.ItemsParams(new long[] { 7, 9 }));
            Assert.DoesNotContain('\n', import);
        }

        [Fact]
        public void Parse_ReadsOkErrorAndImportedItems()
        {
            var ok = CliporaControl.Parse("{\"id\":3,\"ok\":true,\"result\":{\"items\":[12, 13],\"errors\":[]}}");
            var failed = CliporaControl.Parse("{\"id\":4,\"ok\":false,\"error\":\"file.newSequenceFromClip: select a \\\"clip\\\"\"}");

            Assert.True(ok.Ok);
            Assert.Equal(new long[] { 12, 13 }, ok.Items.ToArray());
            Assert.False(failed.Ok);
            Assert.Equal("file.newSequenceFromClip: select a \"clip\"", failed.Error);
            Assert.False(CliporaControl.Parse(null).Ok);
            Assert.Empty(CliporaControl.Parse("{\"ok\":true,\"result\":null}").Items);
        }
    }
}
