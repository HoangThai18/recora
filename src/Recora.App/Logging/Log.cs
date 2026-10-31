using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Recora.App.Logging
{
    /// <summary>
    /// Plain-text log in %LocalAppData%\Recora\logs, one file per day, seven days kept. This is where FFmpeg's stderr and
    /// exception details go; the UI only ever shows friendly Vietnamese sentences.
    /// </summary>
    internal static class Log
    {
        private static readonly object Gate = new object();
        private static string? _path;
        private static bool _echo;

        public static string DefaultDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Recora", "logs");

        public static string? CurrentFile => _path;

        public static void Initialize(string directory, bool echoToConsole)
        {
            Directory.CreateDirectory(directory);
            _path = Path.Combine(directory, $"recora-{DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.log");
            _echo = echoToConsole;

            foreach (var old in Directory.GetFiles(directory, "recora-*.log"))
            {
                if (File.GetLastWriteTime(old) < DateTime.Now.AddDays(-7))
                {
                    TryDelete(old);
                }
            }
        }

        public static void Info(string message) => Write("INFO", message);

        public static void Warn(string message) => Write("WARN", message);

        public static void Error(string message, Exception? error = null) =>
            Write("ERROR", error == null ? message : message + Environment.NewLine + error);

        private static void Write(string level, string message)
        {
            var line = $"{DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)} [{level}] {message}";

            lock (Gate)
            {
                if (_echo)
                {
                    Console.WriteLine(line);
                }

                if (_path == null)
                {
                    return;
                }

                try
                {
                    File.AppendAllText(_path, line + Environment.NewLine, Encoding.UTF8);
                }
                catch (IOException)
                {
                    // A full or locked disk must not take recording down with it; the line is simply lost.
                }
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
