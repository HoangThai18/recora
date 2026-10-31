using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Recora.Core.Recording
{
    /// <summary>
    /// session.json beside a session's segments: enough to finish the file after a crash (BR-01) without the app state
    /// that was lost with it.
    /// </summary>
    [DataContract]
    public sealed class SessionManifest
    {
        public const string FileName = "session.json";

        private static readonly DataContractJsonSerializer Serializer = new DataContractJsonSerializer(typeof(SessionManifest));

        [DataMember(Name = "id", Order = 0)] public string Id { get; set; } = string.Empty;

        [DataMember(Name = "started", Order = 1)] public string StartedAt { get; set; } = string.Empty;

        [DataMember(Name = "output", Order = 2)] public string OutputPath { get; set; } = string.Empty;

        [DataMember(Name = "container", Order = 3)] public string Container { get; set; } = ContainerFormat.Mp4.ToString();

        [DataMember(Name = "codec", Order = 4)] public string Codec { get; set; } = "H264";

        [DataMember(Name = "segments", Order = 5)] public List<string> Segments { get; set; } = new List<string>();

        public static string SegmentName(int index) => (index + 1).ToString("000", System.Globalization.CultureInfo.InvariantCulture) + ".mkv";

        public void Save(string sessionFolder)
        {
            var temp = Path.Combine(sessionFolder, FileName + ".tmp");
            var target = Path.Combine(sessionFolder, FileName);
            using (var stream = File.Create(temp))
            using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, System.Text.Encoding.UTF8, ownsStream: false, indent: true))
            {
                Serializer.WriteObject(writer, this);
            }

            if (File.Exists(target))
            {
                File.Replace(temp, target, null);
            }
            else
            {
                File.Move(temp, target);
            }
        }

        public static SessionManifest? TryLoad(string sessionFolder)
        {
            var path = Path.Combine(sessionFolder, FileName);
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                using (var stream = File.OpenRead(path))
                {
                    return Serializer.ReadObject(stream) as SessionManifest;
                }
            }
            catch (Exception error) when (error is SerializationException || error is IOException || error is InvalidCastException)
            {
                return null;
            }
        }
    }
}
