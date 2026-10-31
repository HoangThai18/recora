using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Recora.App.Logging;
using Recora.Core.Updates;

namespace Recora.App.Platform
{
    /// <summary>Asks GitHub for the release list: no identifiers sent, only the version in the User-Agent.</summary>
    internal static class UpdateService
    {
        public static async Task<AvailableUpdate?> CheckAsync()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (var client = new WebClient { Encoding = Encoding.UTF8 })
                {
                    client.Headers[HttpRequestHeader.UserAgent] = "Recora/" + Program.Version;
                    client.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
                    var json = await client.DownloadStringTaskAsync(UpdateCheck.ReleasesApi).ConfigureAwait(false);
                    var update = UpdateCheck.Newer(json, new Version(Program.Version));
                    Log.Info(update == null ? "update check: up to date" : "update check: " + update.Tag + " available");
                    return update;
                }
            }
            catch (Exception error) when (error is WebException || error is NotSupportedException || error is InvalidOperationException)
            {
                Log.Warn("update check failed: " + error.Message);
                return null;
            }
        }
    }
}
