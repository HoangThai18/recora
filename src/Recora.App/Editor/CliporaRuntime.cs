using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Recora.App.Logging;
using Recora.Core;
using Recora.Core.Editor;

namespace Recora.App.Editor
{
    /// <summary>A clipora.exe Recora can start, and where it came from ("đã cài" or the release tag Recora downloaded).</summary>
    internal sealed record CliporaInstall(string Exe, string Origin);

    /// <summary>
    /// Finds Clipora (installed from thaipro.store, or a copy Recora downloaded before) or downloads the portable build
    /// into %LocalAppData%\Recora\Clipora\&lt;tag&gt;, checking it against the release's SHA256SUMS before use.
    /// </summary>
    internal static class CliporaRuntime
    {
        private const int TimeoutMilliseconds = 30000;

        public static string ManagedRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Recora", "Clipora");

        public static CliporaInstall? Find()
        {
            var overridePath = Environment.GetEnvironmentVariable("RECORA_CLIPORA");
            if (!string.IsNullOrEmpty(overridePath) && File.Exists(overridePath))
            {
                return new CliporaInstall(overridePath, "RECORA_CLIPORA");
            }

            // The MSI installs per machine under the 64-bit Program Files whatever bitness Recora runs with.
            var programFiles = Environment.GetEnvironmentVariable("ProgramW6432") ?? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var installed = Path.Combine(programFiles, "Clipora", "clipora.exe");
            if (File.Exists(installed))
            {
                return new CliporaInstall(installed, "đã cài");
            }

            if (!Directory.Exists(ManagedRoot))
            {
                return null;
            }

            return Directory.GetDirectories(ManagedRoot)
                .Select(folder => (Folder: folder, Version: CliporaRelease.ParseVersion(Path.GetFileName(folder))))
                .Where(f => f.Version != null)
                .OrderByDescending(f => f.Version)
                .Select(f => (f.Folder, Exe: Directory.GetFiles(f.Folder, "clipora.exe", SearchOption.AllDirectories).FirstOrDefault()))
                .Where(f => f.Exe != null)
                .Select(f => new CliporaInstall(f.Exe!, Path.GetFileName(f.Folder)))
                .FirstOrDefault();
        }

        /// <summary>Downloads, verifies and unpacks the newest Clipora; reports bytes done and total as it goes.</summary>
        public static CliporaInstall Download(Action<long, long> progress, CancellationToken cancel)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var package = CliporaRelease.Pick(GetText(CliporaRelease.ReleasesApi))
                ?? throw new RecoraException("clipora_unavailable", "no published Clipora release with a Windows portable zip");
            var expected = Sha256Sums.Find(GetText(package.SumsUrl), package.ZipName)
                ?? throw new RecoraException("clipora_unavailable", "SHA256SUMS has no line for " + package.ZipName);

            Directory.CreateDirectory(ManagedRoot);
            var zip = Path.Combine(ManagedRoot, package.ZipName + ".part");
            var unpack = Path.Combine(ManagedRoot, package.Tag + ".unpack");
            var target = Path.Combine(ManagedRoot, package.Tag);
            try
            {
                var actual = DownloadFile(package.ZipUrl, zip, package.ZipBytes, progress, cancel);
                if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                {
                    throw new RecoraException("clipora_corrupt", $"{package.ZipName}: sha256 {actual}, expected {expected}");
                }

                DeleteFolder(unpack);
                ZipFile.ExtractToDirectory(zip, unpack);
                DeleteFolder(target);
                Directory.Move(unpack, target);
                Log.Info($"clipora {package.Tag} ready in {target} (sha256 {actual})");
            }
            finally
            {
                TryDelete(zip);
                DeleteFolder(unpack);
            }

            // Older downloads are of no use once a newer one is there.
            foreach (var old in Directory.GetDirectories(ManagedRoot).Where(f => !string.Equals(f, target, StringComparison.OrdinalIgnoreCase)))
            {
                DeleteFolder(old);
            }

            var exe = Directory.GetFiles(target, "clipora.exe", SearchOption.AllDirectories).FirstOrDefault()
                ?? throw new RecoraException("clipora_corrupt", "the zip has no clipora.exe");
            return new CliporaInstall(exe, package.Tag);
        }

        private static string GetText(string url)
        {
            var request = Request(url);
            using (var response = request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream()!, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>Streams to disk and hashes on the way, so a 70 MB file is never held in memory.</summary>
        private static string DownloadFile(string url, string path, long expectedBytes, Action<long, long> progress, CancellationToken cancel)
        {
            var request = Request(url);
            using (var response = request.GetResponse())
            using (var input = response.GetResponseStream()!)
            using (var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var sha = SHA256.Create())
            {
                var total = response.ContentLength > 0 ? response.ContentLength : expectedBytes;
                var buffer = new byte[1 << 16];
                long done = 0;
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancel.ThrowIfCancellationRequested();
                    output.Write(buffer, 0, read);
                    sha.TransformBlock(buffer, 0, read, null, 0);
                    done += read;
                    progress(done, total);
                }

                sha.TransformFinalBlock(new byte[0], 0, 0);
                return string.Concat(sha.Hash.Select(b => b.ToString("x2")));
            }
        }

        private static HttpWebRequest Request(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = "Recora/" + Program.Version;
            request.Timeout = TimeoutMilliseconds;
            request.ReadWriteTimeout = TimeoutMilliseconds;
            request.AllowAutoRedirect = true;
            return request;
        }

        private static void DeleteFolder(string folder)
        {
            try
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, recursive: true);
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Log.Warn($"could not delete {folder}: {error.Message}");
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
            }
        }
    }
}
