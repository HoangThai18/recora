using System.Collections.Generic;
using System.Linq;

namespace Recora.Core.Recording
{
    /// <param name="Saved">The final file was already written (the folder just could not be removed then): only clean up.</param>
    public sealed record LeftoverSession(string Folder, SessionManifest? Manifest, IReadOnlyDictionary<string, long> SegmentSizes, bool Saved = false);

    public sealed record RecoveryPlan(IReadOnlyList<(LeftoverSession Session, IReadOnlyList<string> Segments)> Finish, IReadOnlyList<string> Discard);

    /// <summary>
    /// Decides what to do with session folders left behind by a crash (BR-01): any non-empty segment is worth a file;
    /// folders with only empty segments are cleaned up; folders without a manifest are not ours and stay untouched.
    /// </summary>
    public static class RecoveryPlanner
    {
        public static RecoveryPlan Plan(IEnumerable<LeftoverSession> leftovers)
        {
            var finish = new List<(LeftoverSession, IReadOnlyList<string>)>();
            var discard = new List<string>();

            foreach (var session in leftovers)
            {
                if (session.Manifest == null)
                {
                    continue;
                }

                if (session.Saved)
                {
                    discard.Add(session.Folder);
                    continue;
                }

                // Segments the manifest names, in recording order, that actually hold data.
                var usable = session.Manifest.Segments
                    .Where(name => session.SegmentSizes.TryGetValue(name, out var size) && size > 1024)
                    .ToList();

                if (usable.Count > 0)
                {
                    finish.Add((session, usable));
                }
                else
                {
                    discard.Add(session.Folder);
                }
            }

            return new RecoveryPlan(finish, discard);
        }
    }
}
