using System;
using System.Collections.Generic;
using System.Linq;

namespace Recora.Core.Audio
{
    /// <summary>An active microphone; <see cref="HandsFree"/> marks a Bluetooth headset's call microphone.</summary>
    public sealed record MicrophoneCandidate(string Id, bool HandsFree);

    /// <summary>Which microphone to record, and whether to warn that it will put a Bluetooth headset into call mode.</summary>
    public sealed record MicrophoneChoice(string Id, bool HandsFreeWarning);

    /// <summary>
    /// Opening a Bluetooth headset's microphone switches the headset to its hands-free (call) profile: game sound in
    /// the headset turns to low-quality mono or goes silent. The user's explicit choice is kept (with a warning);
    /// "Windows default" avoids that microphone when another one exists.
    /// </summary>
    public static class MicrophonePick
    {
        public static MicrophoneChoice? Pick(IReadOnlyList<MicrophoneCandidate> active, string chosenId, string? defaultId)
        {
            if (active.Count == 0)
            {
                return null;
            }

            var chosen = active.FirstOrDefault(m => string.Equals(m.Id, chosenId, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(chosenId) && chosen != null)
            {
                return new MicrophoneChoice(chosen.Id, chosen.HandsFree);
            }

            var fallback = active.FirstOrDefault(m => string.Equals(m.Id, defaultId, StringComparison.OrdinalIgnoreCase)) ?? active[0];
            if (!fallback.HandsFree)
            {
                return new MicrophoneChoice(fallback.Id, false);
            }

            var other = active.FirstOrDefault(m => !m.HandsFree);
            return other != null ? new MicrophoneChoice(other.Id, false) : new MicrophoneChoice(fallback.Id, true);
        }
    }
}
