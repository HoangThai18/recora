using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Recora.App.Logging;
using Recora.App.Native;
using Recora.Core.Ffmpeg;

namespace Recora.App.Recording
{
    internal readonly record struct FfmpegRun(int ExitCode, string Output, string Error, bool TimedOut);

    /// <summary>
    /// One hidden ffmpeg.exe. Progress arrives on stdout (<c>-progress pipe:1</c>), logs on stderr, and stopping is a
    /// <c>q</c> on stdin so FFmpeg closes the file properly instead of being killed mid-write.
    /// </summary>
    internal sealed class FfmpegProcess : IDisposable
    {
        private const int TailLines = 40;
        private readonly Process _process;
        private readonly string _name;
        private readonly ProgressParser _parser = new ProgressParser();
        private readonly Queue<string> _tail = new Queue<string>();

        private FfmpegProcess(Process process, string name)
        {
            _process = process;
            _name = name;
        }

        public event Action<FfmpegProgress>? Progress;

        public FfmpegProgress? LastProgress { get; private set; }

        public bool HasExited => _process.HasExited;

        public int ExitCode => _process.ExitCode;

        /// <summary>The last stderr lines, for the log when a segment fails.</summary>
        public string ErrorTail
        {
            get
            {
                lock (_tail)
                {
                    return string.Join(Environment.NewLine, _tail);
                }
            }
        }

        public static FfmpegProcess Start(string ffmpegPath, FfmpegArgs args, string name)
        {
            var process = new Process { StartInfo = StartInfo(ffmpegPath, args), EnableRaisingEvents = true };
            var ffmpeg = new FfmpegProcess(process, name);

            process.OutputDataReceived += (_, e) => ffmpeg.OnOutput(e.Data);
            process.ErrorDataReceived += (_, e) => ffmpeg.OnError(e.Data);

            Log.Info($"[{name}] ffmpeg {args.ToCommandLine()}");
            process.Start();
            ChildProcessJob.Attach(process);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return ffmpeg;
        }

        /// <summary>Runs to completion (probes, finalizing) and returns everything it printed.</summary>
        public static FfmpegRun Run(string ffmpegPath, FfmpegArgs args, TimeSpan timeout)
        {
            using (var process = new Process { StartInfo = StartInfo(ffmpegPath, args) })
            {
                Log.Info($"ffmpeg {args.ToCommandLine()}");
                process.Start();
                ChildProcessJob.Attach(process);
                process.StandardInput.Close();

                // Both streams are drained concurrently; reading one to the end first can deadlock on a full pipe.
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();

                if (!process.WaitForExit((int)timeout.TotalMilliseconds))
                {
                    TryKill(process);
                    process.WaitForExit();
                    Task.WaitAll(output, error);
                    return new FfmpegRun(-1, output.Result, error.Result, TimedOut: true);
                }

                Task.WaitAll(output, error);
                return new FfmpegRun(process.ExitCode, output.Result, error.Result, TimedOut: false);
            }
        }

        public void RequestStop()
        {
            try
            {
                _process.StandardInput.Write('q');
                _process.StandardInput.Flush();
            }
            catch (IOException)
            {
                // FFmpeg already exited and closed its stdin.
            }
            catch (InvalidOperationException)
            {
            }
        }

        public bool WaitForExit(TimeSpan timeout)
        {
            if (!_process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                return false;
            }

            // The parameterless overload waits for the async stdout/stderr readers to drain.
            _process.WaitForExit();
            return true;
        }

        public void Kill() => TryKill(_process);

        public void Dispose()
        {
            if (!_process.HasExited)
            {
                TryKill(_process);
            }

            _process.Dispose();
        }

        private static ProcessStartInfo StartInfo(string ffmpegPath, FfmpegArgs args) => new ProcessStartInfo(ffmpegPath, args.ToCommandLine())
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = Path.GetDirectoryName(ffmpegPath) ?? Environment.CurrentDirectory,
        };

        private static void TryKill(Process process)
        {
            try
            {
                process.Kill();
            }
            catch (InvalidOperationException)
            {
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
        }

        private void OnOutput(string? line)
        {
            if (line == null)
            {
                return;
            }

            var snapshot = _parser.Feed(line);
            if (snapshot != null)
            {
                LastProgress = snapshot;
                Progress?.Invoke(snapshot);
            }
        }

        private void OnError(string? line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            Log.Info($"[{_name}] {line}");
            lock (_tail)
            {
                _tail.Enqueue(line!);
                while (_tail.Count > TailLines)
                {
                    _tail.Dequeue();
                }
            }
        }
    }
}
