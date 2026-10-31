using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Recora.Core.Updates
{
    public sealed record AvailableUpdate(Version Version, string Tag);

    /// <summary>
    /// Reads GitHub's release list for the Recora repo and tells whether a newer <c>recora-x.y.z</c> exists. The only
    /// thing Recora ever asks the internet (BR-09); the app links to the download page, it never installs by itself.
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
                .Select(r => (Tag: r.Tag!, Ok: Version.TryParse(r.Tag!.Substring(TagPrefix.Length), out var v), Version: v))
                .Where(r => r.Ok && r.Version > current)
                .OrderByDescending(r => r.Version)
                .Select(r => new AvailableUpdate(r.Version!, r.Tag))
                .FirstOrDefault();
        }

        [DataContract]
        private sealed class Release
        {
            [DataMember(Name = "tag_name")] public string? Tag { get; set; }

            [DataMember(Name = "draft")] public bool Draft { get; set; }

            [DataMember(Name = "prerelease")] public bool Prerelease { get; set; }
        }
    }
}
