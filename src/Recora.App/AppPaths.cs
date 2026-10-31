using System;
using System.IO;

namespace Recora.App
{
    /// <summary>Where Recora keeps its files. A <c>portable.txt</c> next to Recora.exe keeps settings beside the exe instead.</summary>
    internal static class AppPaths
    {
        public static string ExeFolder => AppDomain.CurrentDomain.BaseDirectory;

        public static bool IsPortable => File.Exists(Path.Combine(ExeFolder, "portable.txt"));

        public static string DataFolder => IsPortable
            ? Path.Combine(ExeFolder, "data")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Recora");

        public static string SettingsFile => Path.Combine(DataFolder, "settings.json");

        public static string DefaultOutputFolder
        {
            get
            {
                var videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
                if (string.IsNullOrEmpty(videos))
                {
                    videos = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Videos");
                }

                return Path.Combine(videos, "Recora");
            }
        }
    }
}
