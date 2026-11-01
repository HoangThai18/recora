using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Recora.App.Logging;
using Recora.App.Native;
using Recora.App.Platform;
using Recora.App.UI;
using Recora.Core.Settings;

namespace Recora.App
{
    internal static class Program
    {
        public static string Version => typeof(Program).Assembly.GetName().Version.ToString(3);

        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var selfTest = OptionValue(args, "--selftest");
            var crash = OptionValue(args, "--selftest-crash");
            var recover = OptionValue(args, "--recover");
            var roundtrip = OptionValue(args, "--settings-roundtrip");
            var exclusion = OptionValue(args, "--selftest-exclusion");
            var modes = OptionValue(args, "--selftest-modes");
            var replay = OptionValue(args, "--selftest-replay");
            var trim = OptionValue(args, "--selftest-trim");
            var encoderArgs = OptionValue(args, "--dump-encoder-args");
            var editor = OptionValue(args, "--selftest-editor");
            var screenshot = OptionValue(args, "--ui-screenshot");

            if (selfTest != null || crash != null || recover != null || roundtrip != null || exclusion != null || modes != null || replay != null || trim != null || encoderArgs != null || editor != null || screenshot != null)
            {
                // A GUI-subsystem exe has no console; borrow the caller's so CI logs show progress.
                NativeMethods.AttachConsole(NativeMethods.AttachParentProcess);
            }

            if (selfTest != null)
            {
                return SelfTest.Run(selfTest);
            }

            if (crash != null)
            {
                return SelfTest.Crash(crash);
            }

            if (recover != null)
            {
                return SelfTest.Recover(recover);
            }

            if (roundtrip != null)
            {
                return SelfTest.SettingsRoundtrip(roundtrip);
            }

            if (replay != null)
            {
                return SelfTest.Replay(replay);
            }

            if (trim != null)
            {
                return SelfTest.Trim(trim);
            }

            if (editor != null)
            {
                return SelfTest.Editor(editor);
            }

            if (encoderArgs != null)
            {
                return SelfTest.DumpEncoderArgs(encoderArgs);
            }

            if (modes != null)
            {
                return SelfTest.Modes(modes);
            }

            if (exclusion != null)
            {
                return SelfTest.Exclusion(exclusion);
            }

            if (screenshot != null)
            {
                var scale = float.Parse(OptionValue(args, "--ui-scale") ?? "1", CultureInfo.InvariantCulture);
                var page = (PageKind)Enum.Parse(typeof(PageKind), OptionValue(args, "--page") ?? nameof(PageKind.Home), ignoreCase: true);
                return UiScreenshot.Run(screenshot, scale, page, OptionValue(args, "--library"), args.Contains("--recording"), OptionValue(args, "--dialog"), args.Contains("--demo"));
            }

            return RunApp(args.Contains("--minimized"));
        }

        private static int RunApp(bool minimized)
        {
            Log.Initialize(Log.DefaultDirectory, echoToConsole: false);
            using (var instance = SingleInstance.TryAcquire())
            {
                if (instance == null)
                {
                    Log.Info("Recora is already running; asked it to show its window");
                    return 0;
                }

                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (_, e) => Report(e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error("unhandled exception", e.ExceptionObject as Exception);

                var settings = SettingsStore.Load(AppPaths.SettingsFile);
                if (string.IsNullOrWhiteSpace(settings.OutputFolder))
                {
                    settings.OutputFolder = AppPaths.DefaultOutputFolder;
                }

                Log.Info($"Recora {Version} starting{(AppPaths.IsPortable ? " (portable)" : string.Empty)}, {Environment.OSVersion}");
                using (var form = new MainForm())
                using (var controller = new MainController(form, settings, AppPaths.SettingsFile))
                {
                    controller.Start(minimized);
                    Application.Run(controller.Context);
                }
            }

            return 0;
        }

        private static void Report(Exception error)
        {
            Log.Error("unexpected error on the UI thread", error);
            MessageBox.Show("Recora gặp lỗi không mong muốn nhưng vẫn chạy tiếp. Chi tiết đã ghi vào nhật ký (trang Giới thiệu).", "Recora", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private static string? OptionValue(string[] args, string name)
        {
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
