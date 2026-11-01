using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Recora.App.Logging;
using Recora.App.Native;
using Recora.Core;
using Recora.Core.Ffmpeg;

namespace Recora.App.Recording
{
    /// <summary>"Cắt video": the length of any file, a still at any moment, and the stream-copy cut itself. Blocking.</summary>
    internal static class TrimService
    {
        public static TimeSpan? Duration(string ffmpeg, string path)
        {
            var run = FfmpegProcess.Run(ffmpeg, MediaInfoCommand.Build(path), TimeSpan.FromSeconds(20));
            return MediaInfoCommand.ParseDuration(run.Error);
        }

        /// <summary>A <see cref="FramePreviewCommand.Width"/>×<see cref="FramePreviewCommand.Height"/> still, or null when FFmpeg can't decode it (AV1).</summary>
        public static Bitmap? Frame(string ffmpeg, string path, TimeSpan at)
        {
            var start = new ProcessStartInfo(ffmpeg, FramePreviewCommand.Build(path, at).ToCommandLine())
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using (var process = Process.Start(start)!)
            {
                ChildProcessJob.Attach(process);
                var error = process.StandardError.ReadToEndAsync();
                var bytes = new byte[FramePreviewCommand.FrameBytes];
                var read = 0;
                var stream = process.StandardOutput.BaseStream;
                while (read < bytes.Length)
                {
                    var n = stream.Read(bytes, read, bytes.Length - read);
                    if (n <= 0)
                    {
                        break;
                    }

                    read += n;
                }

                if (!process.WaitForExit(10000))
                {
                    process.Kill();
                }

                if (read < bytes.Length)
                {
                    Log.Warn($"no preview frame at {at} of {Path.GetFileName(path)}: {error.Result.Trim()}");
                    return null;
                }

                var bitmap = new Bitmap(FramePreviewCommand.Width, FramePreviewCommand.Height, PixelFormat.Format32bppRgb);
                var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
                try
                {
                    for (var y = 0; y < bitmap.Height; y++)
                    {
                        Marshal.Copy(bytes, y * bitmap.Width * 4, data.Scan0 + y * data.Stride, bitmap.Width * 4);
                    }
                }
                finally
                {
                    bitmap.UnlockBits(data);
                }

                return bitmap;
            }
        }

        /// <summary>Writes the cut next to the source and returns its path; the original is never touched.</summary>
        public static string Cut(string ffmpeg, string source, TimeSpan from, TimeSpan to)
        {
            var output = TrimCommand.OutputPath(source, File.Exists);
            var run = FfmpegProcess.Run(ffmpeg, TrimCommand.Build(source, output, from, to), TimeSpan.FromMinutes(10));
            if (run.ExitCode != 0 || !File.Exists(output) || new FileInfo(output).Length == 0)
            {
                Log.Error("trim failed: " + run.Error);
                try
                {
                    File.Delete(output);
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                {
                }

                throw new RecoraException("trim_failed", "ffmpeg exited with " + run.ExitCode);
            }

            LibraryFiles.Remember(output, to - from);
            Log.Info($"trimmed {source} {from}..{to} -> {output}");
            return output;
        }
    }
}
