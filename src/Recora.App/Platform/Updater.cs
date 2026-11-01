using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Recora.App.Logging;
using Recora.Core;
using Recora.Core.Editor;
using Recora.Core.Updates;

namespace Recora.App.Platform
{
    /// <summary>
    /// "Cập nhật ngay": downloads the new installer from the release, checks it against the release's SHA256SUMS,
    /// and runs it silently; the installer closes Recora, installs over it and starts it again.
    /// </summary>
    internal static class Updater
    {
        private static string Folder => Path.Combine(Path.GetTempPath(), "Recora-update");

        /// <summary>The installed copy updates itself; a portable folder is the user's to replace.</summary>
        public static bool CanUpdateInPlace => !AppPaths.IsPortable;

        /// <summary>Downloads and verifies; returns the installer's path. Throws <see cref="RecoraException"/> with a short code.</summary>
        public static string Download(AvailableUpdate update, Action<long, long> progress, CancellationToken cancel)
        {
            if (!update.CanInstall)
            {
                throw new RecoraException("update_unavailable", update.Tag + " has no installer or no SHA256SUMS.txt");
            }

            var expected = Sha256Sums.Find(Downloads.GetText(update.SumsUrl!), update.SetupName!)
                ?? throw new RecoraException("update_unavailable", "SHA256SUMS has no line for " + update.SetupName);
            Directory.CreateDirectory(Folder);
            var path = Path.Combine(Folder, update.SetupName!);
            var part = path + ".part";
            try
            {
                var actual = Downloads.File(update.SetupUrl!, part, update.SetupBytes, progress, cancel);
                if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                {
                    throw new RecoraException("update_corrupt", $"{update.SetupName}: sha256 {actual}, expected {expected}");
                }

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(part, path);
                Log.Info($"update {update.Tag} downloaded to {path} (sha256 {actual})");
                return path;
            }
            finally
            {
                try
                {
                    if (File.Exists(part))
                    {
                        File.Delete(part);
                    }
                }
                catch (IOException)
                {
                }
            }
        }

        /// <summary>Starts the installer; the caller exits right after so it can replace Recora.exe.</summary>
        public static void Install(string installer)
        {
            Process.Start(new ProcessStartInfo(installer, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /RELAUNCH=1") { UseShellExecute = true })?.Dispose();
            Log.Info("update installer started: " + installer);
        }

        /// <summary>Leftovers of an earlier update, once the new version is running.</summary>
        public static void CleanUp()
        {
            try
            {
                if (Directory.Exists(Folder))
                {
                    Directory.Delete(Folder, recursive: true);
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
            }
        }
    }
}
