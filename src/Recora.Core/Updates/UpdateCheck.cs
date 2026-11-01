using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Recora.Core.Updates
{
    /// <summary>A newer release; <see cref="SetupUrl"/> and <see cref="SumsUrl"/> are null when it has no installer to update with.</summary>
    public sealed record AvailableUpdate(Version Version, string Tag, string? SetupName = null, string? SetupUrl = null, long SetupBytes = 0, string? SumsUrl = null)
    {
        public bool CanInstall => SetupUrl != null && SumsUrl != null;
    }

    /// <summary>
    /// Reads GitHub's release list for the Recora repo and tells whether a newer <c>recora-x.y.z</c> exists. The only
    /// thing Recora ever asks the internet (BR-09); the app downloads the installer only when the user presses "Cập nhật ngay".
    /// </summary>
    public static class UpdateCheck
    {
        public const string ReleasesApi = "https://api.github.com/repos/HoangThai18/recora/releases?per_page=20";
        public const string TagPrefix = "recora-";

        public static AvailableUpdate? Newer(string releasesJson, Version current)
        {
            List<Release> releases;
            try
            {
                using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(releasesJson)))
                {
                    releases = (List<Release>?)new DataContractJsonSerializer(typeof(List<Release>)).ReadObject(stream) ?? new List<Release>();
                }
            }
            catch (Exception error) when (error is SerializationException || error is InvalidCastException || error is ArgumentException)
            {
                return null;
            }

            return releases
                .Where(r => r != null && !r.Draft && !r.Prerelease && r.Tag != null && r.Tag.StartsWith(TagPrefix, StringComparison.Ordinal))
                .Select(r => (Release: r, Ok: Version.TryParse(r.Tag!.Substring(TagPrefix.Length), out var v), Version: v))
                .Where(r => r.Ok && r.Version > current)
                .OrderByDescending(r => r.Version)
                .Select(r => Describe(r.Release, r.Version!))
                .FirstOrDefault();
        }

        /// <summary>The installer the CI names <c>&lt;prefix&gt;&lt;version&gt;-windows-x64-setup.exe</c> and the checksums beside it.</summary>
        private static AvailableUpdate Describe(Release release, Version version)
        {
            var setupName = TagPrefix + release.Tag!.Substring(TagPrefix.Length) + "-windows-x64-setup.exe";
            var assets = release.Assets ?? new List<Asset>();
            var setup = assets.FirstOrDefault(a => a != null && a.Name == setupName && !string.IsNullOrEmpty(a.Url));
            var sums = assets.FirstOrDefault(a => a != null && a.Name == "SHA256SUMS.txt" && !string.IsNullOrEmpty(a.Url));
            return new AvailableUpdate(version, release.Tag!, setup?.Name, setup?.Url, setup?.Size ?? 0, sums?.Url);
        }

        [DataContract]
        private sealed class Release
        {
            [DataMember(Name = "tag_name")] public string? Tag { get; set; }

            [DataMember(Name = "draft")] public bool Draft { get; set; }

            [DataMember(Name = "prerelease")] public bool Prerelease { get; set; }

            [DataMember(Name = "assets")] public List<Asset>? Assets { get; set; }
        }

        [DataContract]
        private sealed class Asset
        {
            [DataMember(Name = "name")] public string? Name { get; set; }

            [DataMember(Name = "browser_download_url")] public string? Url { get; set; }

            [DataMember(Name = "size")] public long Size { get; set; }
        }
    }
}
