using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Recora.App.Logging;
using Recora.Core.Updates;

namespace Recora.App.Platform
{
    internal sealed record UpdateResult(AvailableUpdate? Update, bool Failed);

    /// <summary>Asks GitHub for the release list: no identifiers sent, only the version in the User-Agent.</summary>
    internal static class UpdateService
    {
        private const int TimeoutMilliseconds = 10000;

        /// <summary>
        /// Runs entirely on a pool thread: on .NET Framework even the "async" web calls resolve DNS, detect the proxy and
        /// connect on the calling thread, which could freeze the window (and the record hotkey) for a long time.
        /// </summary>
        public static Task<UpdateResult> CheckAsync() => Task.Run(Check);

        private static UpdateResult Check()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                var request = (HttpWebRequest)WebRequest.Create(UpdateCheck.ReleasesApi);
                request.UserAgent = "Recora/" + Program.Version;
                request.Accept = "application/vnd.github+json";
                request.Timeout = TimeoutMilliseconds;
                request.ReadWriteTimeout = TimeoutMilliseconds;

                string json;
                using (var response = request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()!, Encoding.UTF8))
                {
                    json = reader.ReadToEnd();
                }

                var update = UpdateCheck.Newer(json, new Version(Program.Version));
                Log.Info(update == null ? "update check: up to date" : "update check: " + update.Tag + " available");
                return new UpdateResult(update, false);
            }
            catch (Exception error) when (error is WebException || error is IOException || error is NotSupportedException || error is InvalidOperationException)
            {
                Log.Warn("update check failed: " + error.Message);
                return new UpdateResult(null, true);
            }
        }
    }
}
