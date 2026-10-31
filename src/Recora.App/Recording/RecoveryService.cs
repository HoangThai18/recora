using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Recora.App.Logging;
using Recora.Core;
using Recora.Core.Recording;

namespace Recora.App.Recording
{
    /// <summary>
    /// At startup, finishes recordings a crash or power cut left as segments (BR-01). Segments written by a killed
    /// FFmpeg lack their index, which the stream-copy remux rebuilds.
    /// </summary>
    /// <param name="NeedSpace">Recordings kept as segments because the drive is too full to join them.</param>
    internal sealed record RecoveryResult(IReadOnlyList<string> Saved, int NeedSpace);

    internal static class RecoveryService
    {
        /// <summary>Finds what a crash left behind. Fast; call it before recording can start, so a new session's folder is never mistaken for a leftover.</summary>
        public static RecoveryPlan Scan(string outputFolder)
        {
            var nothing = new RecoveryPlan(new (LeftoverSession, IReadOnlyList<string>)[0], new string[0]);
            var root = Path.Combine(outputFolder, ".recora");
            try
            {
                if (!Directory.Exists(root))
                {
                    return nothing;
                }

                var leftovers = Directory.GetDirectories(root)
                    .Select(folder => new LeftoverSession(
                        folder,
                        SessionManifest.TryLoad(folder),
                        Directory.GetFiles(folder, "*.mkv").ToDictionary(f => Path.GetFileName(f), f => new FileInfo(f).Length, StringComparer.OrdinalIgnoreCase),
                        File.Exists(Path.Combine(folder, SessionManifest.SavedMarker))))
                    .ToList();

                return RecoveryPlanner.Plan(leftovers);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Log.Warn("could not look for unfinished recordings: " + error.Message);
                return nothing;
            }
        }

        /// <summary>Remuxes every recoverable session into its final file (slow, disk-bound) and removes empty ones.</summary>
        public static RecoveryResult Finish(string ffmpeg, RecoveryPlan plan)
        {
            var recovered = new List<string>();
            var needSpace = 0;

            foreach (var folder in plan.Discard)
            {
                if (File.Exists(Path.Combine(folder, SessionManifest.SavedMarker)))
                {
                    TryClean(folder);
                }
                else
                {
                    TryDelete(folder);
                }
            }

            foreach (var (session, segments) in plan.Finish)
            {
                try
                {
                    var finished = segments.Select(name => new FinishedSegment(name, 0, session.SegmentSizes[name], TimeSpan.Zero)).ToList();
                    var output = Finalizer.Finish(ffmpeg, session.Folder, session.Manifest!, finished, recovered: true);
                    if (output != null)
                    {
                        recovered.Add(output);
                    }
                }
                catch (RecoraException error) when (error.Code == "finalize_no_space")
                {
                    Log.Warn("recovery postponed, not enough space: " + session.Folder);
                    needSpace++;
                }
                catch (Exception error)
                {
                    Log.Error("recovery failed for " + session.Folder, error);
                }
            }

            return new RecoveryResult(recovered, needSpace);
        }

        public static RecoveryResult RecoverAll(string ffmpeg, string outputFolder) => Finish(ffmpeg, Scan(outputFolder));

        private static void TryClean(string folder)
        {
            try
            {
                Finalizer.CleanSavedFolder(folder);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Log.Warn("could not clean " + folder + ": " + error.Message);
            }
        }

        private static void TryDelete(string folder)
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
