using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace Recora.Core.Editor
{
    /// <summary>The Clipora build Recora can download: the portable zip of the newest published release.</summary>
    public sealed record CliporaPackage(string Tag, Version Version, string ZipName, string ZipUrl, long ZipBytes, string SumsUrl);

    /// <summary>
    /// Clipora (the video editor) is published from the thaipro-craft repository next to other apps, under tags like
    /// <c>clipora-0.4.0-dd525c9</c>. This picks the newest one that has a Windows portable zip and a SHA256SUMS file.
    /// </summary>
    public static class CliporaRelease
    {
        public const string Repo = "HoangThai18/thaipro-craft";
        public const string TagPrefix = "clipora-";
        public const string DownloadPage = "https://thaipro.store/phan-mem/clipora";

        public static string ReleasesApi => $"https://api.github.com/repos/{Repo}/releases?per_page=100";

        public static CliporaPackage? Pick(string releasesJson)
        {
            List<Release> releases;
            try
            {
                using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(releasesJson ?? string.Empty)))
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
                .Select(r => (Release: r, Version: ParseVersion(r.Tag!)))
                .Where(r => r.Version != null)
                .OrderByDescending(r => r.Version)
                .Select(r =>
                {
                    var assets = r.Release.Assets ?? new List<Asset>();
                    var zip = assets.FirstOrDefault(a => a?.Name != null && a.Name.EndsWith("-windows-x64-portable.zip", StringComparison.OrdinalIgnoreCase));
                    var sums = assets.FirstOrDefault(a => a?.Name == "SHA256SUMS.txt");
                    return zip == null || sums == null || zip.Url == null || sums.Url == null
                        ? null
                        : new CliporaPackage(r.Release.Tag!, r.Version!, zip.Name!, zip.Url, zip.Size, sums.Url);
                })
                .FirstOrDefault(p => p != null);
        }

        /// <summary>"clipora-0.4.0-dd525c9" → 0.4.0.</summary>
        public static Version? ParseVersion(string tag)
        {
            if (!tag.StartsWith(TagPrefix, StringComparison.Ordinal))
            {
                return null;
            }

            var rest = tag.Substring(TagPrefix.Length);
            var dash = rest.IndexOf('-');
            return Version.TryParse(dash >= 0 ? rest.Substring(0, dash) : rest, out var version) ? version : null;
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

            [DataMember(Name = "size")] public long Size { get; set; }

            [DataMember(Name = "browser_download_url")] public string? Url { get; set; }
        }
    }

    public static class Sha256Sums
    {
        /// <summary>The lower-case hex digest listed for <paramref name="fileName"/> ("hash  name" or "hash *name").</summary>
        public static string? Find(string sums, string fileName)
        {
            foreach (var raw in (sums ?? string.Empty).Split('\n'))
            {
                var match = Regex.Match(raw.Trim(), @"^([0-9a-fA-F]{64})\s+\*?(.+)$");
                if (match.Success && string.Equals(match.Groups[2].Value.Trim(), fileName, StringComparison.Ordinal))
                {
                    return match.Groups[1].Value.ToLowerInvariant();
                }
            }

            return null;
        }
    }

    /// <summary>One reply line of Clipora's control channel.</summary>
    public sealed record ControlReply(bool Ok, string? Error, string Raw)
    {
        /// <summary>Ids under <c>"items"</c> in the result (what <c>file.import</c> returns).</summary>
        public IReadOnlyList<long> Items
        {
            get
            {
                var match = Regex.Match(Raw, "\"items\"\\s*:\\s*\\[([0-9,\\s]*)\\]");
                return match.Success
                    ? match.Groups[1].Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(v => long.Parse(v.Trim(), CultureInfo.InvariantCulture)).ToList()
                    : new List<long>();
            }
        }
    }

    /// <summary>
    /// Clipora's <c>--control &lt;port&gt;</c> protocol: one JSON object per line on 127.0.0.1, one reply per line
    /// (docs/control-protocol.md upstream). Only the few requests Recora needs are built here.
    /// </summary>
    public static class CliporaControl
    {
        public static string Execute(int id, string command, string paramsJson) =>
            $"{{\"id\":{id},\"method\":\"engine.execute\",\"params\":{{\"command\":{Json(command)},\"params\":{paramsJson}}}}}";

        public static string Method(int id, string method) => $"{{\"id\":{id},\"method\":{Json(method)}}}";

        public static string ImportParams(IEnumerable<string> paths) => "{\"paths\":[" + string.Join(",", paths.Select(Json)) + "]}";

        public static string ItemsParams(IEnumerable<long> items) =>
            "{\"items\":[" + string.Join(",", items.Select(i => i.ToString(CultureInfo.InvariantCulture))) + "]}";

        public static ControlReply Parse(string? line)
        {
            var raw = line ?? string.Empty;
            var ok = Regex.IsMatch(raw, "\"ok\"\\s*:\\s*true");
            var error = Regex.Match(raw, "\"error\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            return new ControlReply(ok, error.Success ? Regex.Unescape(error.Groups[1].Value) : (ok ? null : "no reply"), raw);
        }

        /// <summary>A JSON string literal; Windows paths are full of backslashes, file names may hold quotes or Vietnamese.</summary>
        public static string Json(string value)
        {
            var text = new StringBuilder("\"");
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': text.Append("\\\""); break;
                    case '\\': text.Append("\\\\"); break;
                    case '\n': text.Append("\\n"); break;
                    case '\r': text.Append("\\r"); break;
                    case '\t': text.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            text.Append(c);
                        }

                        break;
                }
            }

            return text.Append('"').ToString();
        }
    }
}
