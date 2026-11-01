using System.Collections.Generic;
using System.Linq;
using Recora.Core.Encoding;
using Recora.Core.Settings;

namespace Recora.App.Recording
{
    /// <summary>
    /// Remembers which encoders worked and failed on this PC (saved in settings with the GPU/FFmpeg fingerprint), so the
    /// next start tries the known-good one first instead of spending seconds on encoders this GPU cannot run.
    /// </summary>
    internal sealed class EncoderMemory
    {
        private static readonly FrameRoute[] AllRoutes =
        {
            FrameRoute.Textures, FrameRoute.Nv12Textures, FrameRoute.Nv12Memory, FrameRoute.BgraMemory,
        };

        private readonly EncoderProbeCache _cache;

        public EncoderMemory(AppSettings settings, string fingerprint)
        {
            if (settings.EncoderProbe == null || settings.EncoderProbe.Fingerprint != fingerprint)
            {
                settings.EncoderProbe = new EncoderProbeCache { Fingerprint = fingerprint };
            }

            _cache = settings.EncoderProbe;
        }

        public bool Dirty { get; set; }

        public string? LastWorking => _cache.Working.FirstOrDefault();

        public void Worked(VideoEncoder encoder)
        {
            _cache.Working = new[] { encoder.FfmpegName }.Concat(_cache.Working.Where(n => n != encoder.FfmpegName)).ToArray();
            _cache.Failed = _cache.Failed.Where(n => n != encoder.FfmpegName).ToArray();
            Dirty = true;
        }

        public void Failed(VideoEncoder encoder)
        {
            if (!_cache.Failed.Contains(encoder.FfmpegName))
            {
                _cache.Failed = _cache.Failed.Concat(new[] { encoder.FfmpegName }).ToArray();
                Dirty = true;
            }
        }

        /// <summary>The frame routes this encoder has refused on this PC.</summary>
        public IReadOnlyCollection<FrameRoute> FailedRoutes(VideoEncoder encoder) =>
            AllRoutes.Where(route => _cache.Failed.Contains(RouteKey(encoder, route))).ToList();

        /// <summary>
        /// Only a route working where others just failed proves those routes were the problem: a capture hiccup fails
        /// them all and must not move a healthy GPU off its lightest path, so nothing is recorded unless one worked.
        /// </summary>
        public void RememberRoutes(VideoEncoder encoder, FrameRoute worked, IEnumerable<FrameRoute> failedJustNow)
        {
            var workedKey = RouteKey(encoder, worked);
            var failed = _cache.Failed
                .Where(name => name != workedKey)
                .Concat(failedJustNow.Select(route => RouteKey(encoder, route)))
                .Distinct()
                .ToArray();

            if (!failed.SequenceEqual(_cache.Failed))
            {
                _cache.Failed = failed;
                Dirty = true;
            }
        }

        /// <summary>User's forced choice first, then encoders known to work, then untried ones, known failures last.</summary>
        public IReadOnlyList<VideoEncoder> Order(IReadOnlyList<VideoEncoder> candidates, EncoderKind? preferred)
        {
            int Rank(VideoEncoder e)
            {
                var working = System.Array.IndexOf(_cache.Working, e.FfmpegName);
                if (working >= 0)
                {
                    return 1;
                }

                return _cache.Failed.Contains(e.FfmpegName) ? 3 : 2;
            }

            // The catalog's tiers stay as they are: GPU encoders for the requested codec, GPU H.264, then CPU (BR-02).
            // Memory only reorders inside a tier, so switching to H.265 never keeps recording H.264 just because H.264
            // worked last time, and a CPU encoder that worked never jumps ahead of an untried GPU one.
            var requested = candidates.Count > 0 ? candidates[0].Codec : VideoCodec.H264;
            int Tier(VideoEncoder e) => !e.IsHardware ? 2 : e.Codec == requested ? 0 : 1;

            return candidates.Select((e, i) => (e, i))
                .OrderBy(p => preferred.HasValue && p.e.Kind == preferred.Value && p.e.Codec == requested ? 0 : 1)
                .ThenBy(p => Tier(p.e))
                .ThenBy(p => Rank(p.e))
                .ThenBy(p => p.i)
                .Select(p => p.e)
                .ToList();
        }

        // "+textures" is what settings saved before there were more routes call the same thing.
        private static string RouteKey(VideoEncoder encoder, FrameRoute route) => route switch
        {
            FrameRoute.Textures => encoder.FfmpegName + "+textures",
            FrameRoute.Nv12Textures => encoder.FfmpegName + "+nv12-textures",
            FrameRoute.Nv12Memory => encoder.FfmpegName + "+nv12-memory",
            _ => encoder.FfmpegName + "+memory",
        };
    }
}
