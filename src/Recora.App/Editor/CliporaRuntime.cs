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
            var package = CliporaRelease.Pick(Platform.Downloads.GetText(CliporaRelease.ReleasesApi))
                ?? throw new RecoraException("clipora_unavailable", "no published Clipora release with a Windows portable zip");
            var expected = Sha256Sums.Find(Platform.Downloads.GetText(package.SumsUrl), package.ZipName)
                ?? throw new RecoraException("clipora_unavailable", "SHA256SUMS has no line for " + package.ZipName);

            Directory.CreateDirectory(ManagedRoot);
            var zip = Path.Combine(ManagedRoot, package.ZipName + ".part");
            var unpack = Path.Combine(ManagedRoot, package.Tag + ".unpack");
            var target = Path.Combine(ManagedRoot, package.Tag);
            try
            {
                var actual = Platform.Downloads.File(package.ZipUrl, zip, package.ZipBytes, progress, cancel);
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
