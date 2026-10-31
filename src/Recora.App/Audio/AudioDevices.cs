using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using Recora.App.Logging;

namespace Recora.App.Audio
{
    internal sealed record AudioDeviceOption(string Id, string Name);

    /// <summary>Speakers and microphones Windows reports as active; an empty id means "Windows default".</summary>
    internal static class AudioDevices
    {
        public static IReadOnlyList<AudioDeviceOption> Speakers() => List(DataFlow.Render);

        public static IReadOnlyList<AudioDeviceOption> Microphones() => List(DataFlow.Capture);

        /// <summary>The chosen device, or the default when it is unplugged or never chosen; null if there is none at all.</summary>
        public static MMDevice? Resolve(DataFlow flow, string id)
        {
            try
            {
                using (var enumerator = new MMDeviceEnumerator())
                {
                    if (!string.IsNullOrEmpty(id))
                    {
                        try
                        {
                            var chosen = enumerator.GetDevice(id);
                            if (chosen.State == DeviceState.Active)
                            {
                                return chosen;
                            }
                        }
                        catch (COMException)
                        {
                            Log.Warn($"audio device {id} is gone, using the default");
                        }
                    }

                    return enumerator.HasDefaultAudioEndpoint(flow, Role.Multimedia)
                        ? enumerator.GetDefaultAudioEndpoint(flow, flow == DataFlow.Capture ? Role.Communications : Role.Multimedia)
                        : null;
                }
            }
            catch (COMException error)
            {
                Log.Warn($"audio endpoints unavailable: {error.Message}");
                return null;
            }
        }

        private static IReadOnlyList<AudioDeviceOption> List(DataFlow flow)
        {
            try
            {
                using (var enumerator = new MMDeviceEnumerator())
                {
                    return enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active)
                        .Select(d => new AudioDeviceOption(d.ID, d.FriendlyName))
                        .OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
                        .ToList();
                }
            }
            catch (COMException error)
            {
                Log.Warn($"audio endpoints unavailable: {error.Message}");
                return new AudioDeviceOption[0];
            }
        }
    }
}
