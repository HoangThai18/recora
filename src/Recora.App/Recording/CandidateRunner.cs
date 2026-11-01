using System;
using System.Collections.Generic;
using System.Linq;
using Recora.App.Logging;
using Recora.Core;
using Recora.Core.Capture;
using Recora.Core.Encoding;
using Recora.Core.Recording;

namespace Recora.App.Recording
{
    /// <summary>
    /// Tries capture backends and encoders in order until one produces frames (BR-02). Shared by recordings and the
    /// replay buffer, so both fall back the same way and both teach <see cref="EncoderMemory"/>.
    /// </summary>
    internal static class CandidateRunner
    {
        /// <param name="tryOpen">Starts FFmpeg with a plan; returns null when frames flow, otherwise why it failed.</param>
        /// <param name="only">Restrict to one capture backend (restarting a segment the way it ran before).</param>
        public static void Run(CaptureEnvironment environment, EncoderMemory memory, RecordingRequest request, Func<RecordingPlan, string?> tryOpen, CaptureBackend? only = null)
        {
            var backends = only.HasValue ? new[] { only.Value } : Backends(environment, request.Source);
            var encoders = memory.Order(
                EncoderCatalog.Candidates(request.Source.Monitor.Vendor, request.Codec, environment.Encoders),
                request.PreferredEncoder);
            var failures = new List<string>();

            foreach (var backend in backends)
            {
                foreach (var encoder in encoders)
                {
                    var plan = new RecordingPlan(
                        request.Source, backend, encoder, request.Quality, request.Fps, request.Scale, request.DrawCursor, Layout(request), request.MaxGigabytesPerHour);
                    var outcome = tryOpen(plan);

                    if (outcome == null)
                    {
                        memory.Worked(encoder);
                        return;
                    }

                    failures.Add($"{backend}/{encoder.FfmpegName}: {outcome}");
                    Log.Warn($"candidate {backend}/{encoder.FfmpegName} failed: {outcome}");

                    // A capture that cannot even open fails the same way for every encoder: skip to the next backend,
                    // and don't hold it against the encoder.
                    if (outcome.IndexOf("Error opening input", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        break;
                    }

                    // Only failures that name the encoder count against it (a bad option or a broken input would
                    // otherwise mark every encoder as failed and push recordings onto the CPU).
                    if (outcome.IndexOf(encoder.FfmpegName, StringComparison.OrdinalIgnoreCase) >= 0
                        || outcome.IndexOf("encoder", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        memory.Failed(encoder);
                    }
                }
            }

            if (request.Source is DeviceSource && failures.All(f => f.IndexOf("Error opening input", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                throw new RecoraException("device_unavailable", "the device did not open: " + string.Join(" || ", failures));
            }

            throw new RecoraException("no_working_encoder", "no capture/encoder pair worked: " + string.Join(" || ", failures));
        }

        private static IEnumerable<CaptureBackend> Backends(CaptureEnvironment environment, CaptureSource source)
        {
            var first = CaptureBackendSelector.Select(source, environment.Capabilities);
            yield return first;
            if (first == CaptureBackend.Dshow)
            {
                yield break;
            }

            if (first != CaptureBackend.Gfxcapture && environment.Capabilities.HasGfxcapture && source is not RegionSource)
            {
                yield return CaptureBackend.Gfxcapture;
            }

            if (first != CaptureBackend.Gdigrab)
            {
                yield return CaptureBackend.Gdigrab;
            }
        }

        private static AudioLayout Layout(RecordingRequest request) => request.AudioTracks.Count switch
        {
            0 => AudioLayout.None,
            1 => AudioLayout.Mixed,
            _ => AudioLayout.SeparateTracks,
        };
    }
}
