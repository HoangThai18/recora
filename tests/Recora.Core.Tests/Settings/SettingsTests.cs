using Recora.Core.Audio;
using Recora.Core.Ffmpeg;
using System.IO;
using Recora.Core.Encoding;
using Recora.Core.Hotkeys;
using Recora.Core.Recording;
using Recora.Core.Settings;

namespace Recora.Core.Tests.Settings
{
    public sealed class SettingsStoreTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "recora-tests-" + Guid.NewGuid().ToString("N"));

        public SettingsStoreTests() => Directory.CreateDirectory(_folder);

        public void Dispose() => Directory.Delete(_folder, recursive: true);

        private string SettingsPath => Path.Combine(_folder, "settings.json");

        [Fact]
        public void Load_WhenFileMissing_ReturnsDefaults()
        {
            var settings = SettingsStore.Load(SettingsPath);

            Assert.Equal("F12", settings.HotkeyRecord);
            Assert.Equal("Shift+F12", settings.HotkeyPause);
            Assert.Equal("F11", settings.HotkeyScreenshot);
            Assert.Equal(QualityPreset.Balanced, settings.Quality);
            Assert.Equal(VideoCodec.H264, settings.Codec);
            Assert.Equal(60, settings.Fps);
            Assert.Equal(ContainerFormat.Mp4, settings.Container);
            Assert.True(settings.SystemAudio);
            Assert.False(settings.Microphone);
            Assert.Equal(EncoderChoice.Auto, settings.Encoder);
        }

        [Fact]
        public void Load_WhenJsonIsBroken_KeepsBackupAndReturnsDefaults()
        {
            File.WriteAllText(SettingsPath, "{ \"quality\": ");

            var settings = SettingsStore.Load(SettingsPath);

            Assert.Equal(QualityPreset.Balanced, settings.Quality);
            Assert.Equal("{ \"quality\": ", File.ReadAllText(SettingsPath + ".bak"));
        }

        [Fact]
        public void Load_UnknownEnumValue_FallsBackForThatFieldOnly()
        {
            File.WriteAllText(SettingsPath, "{\"quality\":\"Ultra\",\"fps\":30,\"codec\":\"Hevc\"}");

            var settings = SettingsStore.Load(SettingsPath);

            Assert.Equal(QualityPreset.Balanced, settings.Quality);
            Assert.Equal(30, settings.Fps);
            Assert.Equal(VideoCodec.Hevc, settings.Codec);
            Assert.Equal("F12", settings.HotkeyRecord);
        }

        [Fact]
        public void Load_NewFieldsFromAnOlderFile_GetDefaults_AndBadValuesAreFixed()
        {
            File.WriteAllText(SettingsPath, "{\"audioFormat\":\"Flac\",\"autoStopMinutes\":-5}");

            var settings = SettingsStore.Load(SettingsPath);

            Assert.Equal(AudioFormat.M4a, settings.AudioFormat);
            Assert.Equal(0, settings.AutoStopMinutes);
            Assert.Equal(string.Empty, settings.VideoDevice);
            Assert.True(settings.ScreenshotSound);
        }

        [Theory]
        [InlineData("{\"maxGigabytesPerHour\":3}", 0)]
        [InlineData("{\"maxGigabytesPerHour\":-1}", 0)]
        [InlineData("{\"maxGigabytesPerHour\":4}", 4)]
        [InlineData("{}", 0)]
        public void Load_SizeLimit_OnlyOfferedValuesSurvive(string json, int expected)
        {
            File.WriteAllText(SettingsPath, json);

            Assert.Equal(expected, SettingsStore.Load(SettingsPath).MaxGigabytesPerHour);
        }

        [Fact]
        public void Load_MicProcessing_MissingOrPartial_GetsSafeDefaults()
        {
            File.WriteAllText(SettingsPath, "{\"micProcessing\":{\"gainDb\":50}}");
            var partial = SettingsStore.Load(SettingsPath).MicProcessing;

            Assert.Equal(MicProcessingSettings.MaxGainDb, partial.GainDb);
            Assert.True(partial.Limiter);
            Assert.Equal(-45f, partial.GateThresholdDb);

            File.WriteAllText(SettingsPath, "{\"micProcessing\":null}");
            Assert.True(SettingsStore.Load(SettingsPath).MicProcessing.Limiter);
        }

        [Theory]
        [InlineData("{\"replaySeconds\":45}", 30)]
        [InlineData("{\"replaySeconds\":300}", 300)]
        [InlineData("{}", 30)]
        public void Load_ReplayLength_OnlyOfferedValuesSurvive(string json, int expected)
        {
            File.WriteAllText(SettingsPath, json);

            var settings = SettingsStore.Load(SettingsPath);
            Assert.Equal(expected, settings.ReplaySeconds);
            Assert.False(settings.ReplayEnabled);
        }

        [Fact]
        public void Load_SaverQuality_RoundTrips()
        {
            SettingsStore.Save(SettingsPath, new AppSettings { Quality = QualityPreset.Saver });

            Assert.Equal(QualityPreset.Saver, SettingsStore.Load(SettingsPath).Quality);
        }

        [Theory]
        [InlineData(0, AppSettings.MinFps)]
        [InlineData(999, AppSettings.MaxFps)]
        [InlineData(144, 144)]
        public void Load_ClampsFps(int stored, int expected)
        {
            File.WriteAllText(SettingsPath, "{\"fps\":" + stored + "}");

            Assert.Equal(expected, SettingsStore.Load(SettingsPath).Fps);
        }

        [Fact]
        public void SaveThenLoad_RoundTripsEveryField()
        {
            var original = new AppSettings
            {
                OutputFolder = @"D:\Video\Quay màn hình",
                Mode = CaptureMode.Region,
                Quality = QualityPreset.High,
                Codec = VideoCodec.Av1,
                Encoder = EncoderChoice.Qsv,
                Fps = 120,
                Scale = OutputScale.P720,
                Container = ContainerFormat.Mkv,
                Microphone = true,
                MicrophoneVolume = 1.5f,
                SeparateTracks = true,
                HotkeyRecord = "Ctrl+Shift+R",
                ScreenshotFormat = ScreenshotFormat.Jpg,
                VideoDevice = "HD Pro Webcam C920",
                AudioFormat = AudioFormat.Mp3,
                AutoStopMinutes = 30,
                ScreenshotSound = false,
                WebcamOverlay = true,
                ClickHighlight = true,
                MaxGigabytesPerHour = 2,
                ReplayEnabled = true,
                ReplaySeconds = 120,
                HotkeyReplay = "Ctrl+F9",
                MicProcessing = new MicProcessingSettings { GainDb = 6, HighPass = true, NoiseGate = true, GateThresholdDb = -50, Compressor = true, Limiter = false },
                EncoderProbe = new EncoderProbeCache { Fingerprint = "10de:2684|8.1.3", Working = new[] { "h264_nvenc" } },
            };

            SettingsStore.Save(SettingsPath, original);
            SettingsStore.Save(SettingsPath, original);
            var loaded = SettingsStore.Load(SettingsPath);

            Assert.Equal(original.OutputFolder, loaded.OutputFolder);
            Assert.Equal(CaptureMode.Region, loaded.Mode);
            Assert.Equal(QualityPreset.High, loaded.Quality);
            Assert.Equal(VideoCodec.Av1, loaded.Codec);
            Assert.Equal(EncoderChoice.Qsv, loaded.Encoder);
            Assert.Equal(120, loaded.Fps);
            Assert.Equal(OutputScale.P720, loaded.Scale);
            Assert.Equal(ContainerFormat.Mkv, loaded.Container);
            Assert.True(loaded.Microphone);
            Assert.Equal(1.5f, loaded.MicrophoneVolume);
            Assert.True(loaded.SeparateTracks);
            Assert.Equal("Ctrl+Shift+R", loaded.HotkeyRecord);
            Assert.Equal(ScreenshotFormat.Jpg, loaded.ScreenshotFormat);
            Assert.Equal("HD Pro Webcam C920", loaded.VideoDevice);
            Assert.Equal(AudioFormat.Mp3, loaded.AudioFormat);
            Assert.Equal(30, loaded.AutoStopMinutes);
            Assert.False(loaded.ScreenshotSound);
            Assert.True(loaded.WebcamOverlay && loaded.ClickHighlight);
            Assert.Equal(2, loaded.MaxGigabytesPerHour);
            Assert.True(loaded.ReplayEnabled);
            Assert.Equal(120, loaded.ReplaySeconds);
            Assert.Equal("Ctrl+F9", loaded.HotkeyReplay);
            Assert.Equal(6f, loaded.MicProcessing.GainDb);
            Assert.True(loaded.MicProcessing.HighPass && loaded.MicProcessing.NoiseGate && loaded.MicProcessing.Compressor);
            Assert.Equal(-50f, loaded.MicProcessing.GateThresholdDb);
            Assert.False(loaded.MicProcessing.Limiter);
            Assert.Equal(new[] { "h264_nvenc" }, loaded.EncoderProbe!.Working);
            Assert.False(File.Exists(SettingsPath + ".tmp"));
        }
    }

    public class HotkeyTests
    {
        [Fact]
        public void TryParse_ReadsModifiersAndKey()
        {
            Assert.True(Hotkey.TryParse("Ctrl+Shift+F12", out var hotkey));

            Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Shift, hotkey.Modifiers);
            Assert.Equal(0x7B, hotkey.VirtualKey);
            Assert.Equal("Ctrl+Shift+F12", hotkey.ToString());
        }

        [Theory]
        [InlineData("F12", "F12")]
        [InlineData("shift + f12", "Shift+F12")]
        [InlineData("alt+printscreen", "Alt+PrintScreen")]
        [InlineData("Win+Alt+R", "Alt+Win+R")]
        [InlineData("Ctrl+Num5", "Ctrl+Num5")]
        public void ToString_IsCanonical(string text, string expected)
        {
            Assert.True(Hotkey.TryParse(text, out var hotkey));
            Assert.Equal(expected, hotkey.ToString());
        }

        [Theory]
        [InlineData("")]
        [InlineData("Ctrl+Shift")]
        [InlineData("F12+F11")]
        [InlineData("Ctrl+Foo")]
        [InlineData(null)]
        public void TryParse_RejectsGarbage(string? text)
        {
            Assert.False(Hotkey.TryParse(text, out _));
        }

        [Fact]
        public void Defaults_MatchBandicamHabits()
        {
            Assert.Equal("F12", Hotkey.Record.ToString());
            Assert.Equal("Shift+F12", Hotkey.Pause.ToString());
            Assert.Equal("F11", Hotkey.Screenshot.ToString());
            Assert.Equal("Alt+F10", Hotkey.SaveReplay.ToString());
            Assert.Equal(Hotkey.SaveReplay.ToString(), new AppSettings().HotkeyReplay);
        }
    }
}
