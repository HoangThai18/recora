using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Recora.App.Platform
{
    /// <summary>HTTPS downloads from GitHub Releases, hashed with SHA-256 while they stream to disk.</summary>
    internal static class Downloads
    {
        private const int TimeoutMilliseconds = 30000;

        public static string GetText(string url)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using (var response = Request(url).GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream()!, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>Streams to <paramref name="path"/> and returns the lower-case SHA-256, so big files never sit in memory.</summary>
        public static string File(string url, string path, long expectedBytes, Action<long, long> progress, CancellationToken cancel)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using (var response = Request(url).GetResponse())
            using (var input = response.GetResponseStream()!)
            using (var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var sha = SHA256.Create())
            {
                var total = response.ContentLength > 0 ? response.ContentLength : expectedBytes;
                var buffer = new byte[1 << 16];
                long done = 0;
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancel.ThrowIfCancellationRequested();
                    output.Write(buffer, 0, read);
                    sha.TransformBlock(buffer, 0, read, null, 0);
                    done += read;
                    progress(done, total);
                }

                sha.TransformFinalBlock(new byte[0], 0, 0);
                return string.Concat(sha.Hash.Select(b => b.ToString("x2")));
            }
        }

        private static HttpWebRequest Request(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = "Recora/" + Program.Version;
            request.Timeout = TimeoutMilliseconds;
            request.ReadWriteTimeout = TimeoutMilliseconds;
            request.AllowAutoRedirect = true;
            return request;
        }
    }
}
