using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Recora.Core.Settings
{
    /// <summary>
    /// Reads and writes settings.json. A damaged file never stops the app: it is kept as <c>settings.json.bak</c>
    /// for support and Recora starts from defaults. Writes go to a temp file first so a crash cannot leave half a file.
    /// </summary>
    public static class SettingsStore
    {
        private static readonly DataContractJsonSerializer Serializer = new DataContractJsonSerializer(
            typeof(AppSettings),
            new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });

        public static AppSettings Load(string path)
        {
            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            try
            {
                using (var stream = File.OpenRead(path))
                {
                    return ((AppSettings?)Serializer.ReadObject(stream) ?? new AppSettings()).Normalize();
                }
            }
            catch (Exception error) when (error is SerializationException || error is InvalidCastException || error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                // Keep the broken file for a look later, but never let settings stop Recora from opening.
                try
                {
                    File.Copy(path, path + ".bak", overwrite: true);
                }
                catch (Exception copyError) when (copyError is IOException || copyError is UnauthorizedAccessException)
                {
                }

                return new AppSettings();
            }
        }

        public static void Save(string path, AppSettings settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var temp = path + ".tmp";

            using (var stream = File.Create(temp))
            using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, System.Text.Encoding.UTF8, ownsStream: false, indent: true))
            {
                Serializer.WriteObject(writer, settings);
            }

            if (File.Exists(path))
            {
                File.Replace(temp, path, null);
            }
            else
            {
                File.Move(temp, path);
            }
        }
    }
}
