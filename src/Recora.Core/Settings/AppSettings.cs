using System;
using System.Runtime.Serialization;
using Recora.Core.Audio;
using Recora.Core.Encoding;
using Recora.Core.Ffmpeg;
using Recora.Core.Recording;

namespace Recora.Core.Settings
{
    public enum CaptureMode
    {
        FullScreen,
        Region,
        Window,
        Game,
        Device,
        AudioOnly,
    }

    /// <summary>"Auto" lets Recora pick the encoder from the probe; the others force one kind first.</summary>
    public enum EncoderChoice
    {
        Auto,
        Nvenc,
        Amf,
        Qsv,
        X264,
    }

    public enum ScreenshotFormat
    {
        Png,
        Jpg,
    }

    /// <summary>
    /// Everything the user can change. Enums are stored as their names, so a newer Recora adding values never breaks
    /// an older settings file, and an unknown name falls back to that field's default instead of failing the whole file.
    /// </summary>
    [DataContract]
    public sealed class AppSettings
    {
        public const int MinFps = 5;
        public const int MaxFps = 240;

        /// <summary>The size limits offered in the Video page, in GB per hour (0: no limit).</summary>
        public static readonly int[] SizeLimits = { 0, 1, 2, 4, 8 };

        public string OutputFolder { get; set; } = string.Empty;

        public CaptureMode Mode { get; set; } = CaptureMode.FullScreen;

        [DataMember(Name = "monitor", Order = 3)] public string MonitorDevice { get; set; } = string.Empty;

        public QualityPreset Quality { get; set; } = QualityPreset.Balanced;

        public VideoCodec Codec { get; set; } = VideoCodec.H264;

        public EncoderChoice Encoder { get; set; } = EncoderChoice.Auto;

        [DataMember(Name = "fps", Order = 8)] public int Fps { get; set; } = 60;

        public OutputScale Scale { get; set; } = OutputScale.Original;

        public ContainerFormat Container { get; set; } = ContainerFormat.Mp4;

        [DataMember(Name = "drawCursor", Order = 11)] public bool DrawCursor { get; set; } = true;

        [DataMember(Name = "systemAudio", Order = 12)] public bool SystemAudio { get; set; } = true;

        [DataMember(Name = "systemAudioDevice", Order = 13)] public string SystemAudioDevice { get; set; } = string.Empty;

        [DataMember(Name = "systemVolume", Order = 14)] public float SystemVolume { get; set; } = 1f;

        [DataMember(Name = "microphone", Order = 15)] public bool Microphone { get; set; }

        [DataMember(Name = "microphoneDevice", Order = 16)] public string MicrophoneDevice { get; set; } = string.Empty;

        [DataMember(Name = "microphoneVolume", Order = 17)] public float MicrophoneVolume { get; set; } = 1f;

        [DataMember(Name = "separateTracks", Order = 18)] public bool SeparateTracks { get; set; }

        [DataMember(Name = "hotkeyRecord", Order = 19)] public string HotkeyRecord { get; set; } = "F12";

        [DataMember(Name = "hotkeyPause", Order = 20)] public string HotkeyPause { get; set; } = "Shift+F12";

        [DataMember(Name = "hotkeyScreenshot", Order = 21)] public string HotkeyScreenshot { get; set; } = "F11";

        public ScreenshotFormat ScreenshotFormat { get; set; } = ScreenshotFormat.Png;

        [DataMember(Name = "screenshotToClipboard", Order = 23)] public bool ScreenshotToClipboard { get; set; } = true;

        [DataMember(Name = "minimizeToTray", Order = 24)] public bool MinimizeToTrayOnClose { get; set; } = true;

        [DataMember(Name = "startWithWindows", Order = 25)] public bool StartWithWindows { get; set; }

        [DataMember(Name = "hideFromCapture", Order = 26)] public bool HideRecoraFromVideo { get; set; } = true;

        [DataMember(Name = "countdown", Order = 27)] public bool Countdown { get; set; }

        [DataMember(Name = "encoderProbe", Order = 28)] public EncoderProbeCache? EncoderProbe { get; set; }

        /// <summary>The DirectShow name of the webcam or capture card for the "Webcam" mode; empty means the first one.</summary>
        [DataMember(Name = "videoDevice", Order = 29)] public string VideoDevice { get; set; } = string.Empty;

        public AudioFormat AudioFormat { get; set; } = AudioFormat.M4a;

        /// <summary>Stop by itself after this many minutes; 0 never.</summary>
        [DataMember(Name = "autoStopMinutes", Order = 31)] public int AutoStopMinutes { get; set; }

        [DataMember(Name = "screenshotSound", Order = 32)] public bool ScreenshotSound { get; set; } = true;

        /// <summary>Show the chosen webcam in a corner while recording the screen.</summary>
        [DataMember(Name = "webcamOverlay", Order = 34)] public bool WebcamOverlay { get; set; }

        /// <summary>Draw a ring at each mouse click while recording.</summary>
        [DataMember(Name = "clickHighlight", Order = 35)] public bool ClickHighlight { get; set; }

        /// <summary>Hourly size ceiling for video recordings in GB, one of <see cref="SizeLimits"/>; 0 means none.</summary>
        [DataMember(Name = "maxGigabytesPerHour", Order = 36)] public int MaxGigabytesPerHour { get; set; }

        /// <summary>The microphone chain (gain, rumble filter, noise gate, compressor, limiter).</summary>
        [DataMember(Name = "micProcessing", Order = 37)] public MicProcessingSettings MicProcessing { get; set; } = new MicProcessingSettings();

        [DataMember(Name = "audioFormat", Order = 30)]
        private string AudioFormatText { get => AudioFormat.ToString(); set => AudioFormat = Parse(value, AudioFormat.M4a); }

        [DataMember(Name = "outputFolder", Order = 0)]
        private string OutputFolderText { get => OutputFolder; set => OutputFolder = value ?? string.Empty; }

        [DataMember(Name = "mode", Order = 2)]
        private string ModeText { get => Mode.ToString(); set => Mode = Parse(value, CaptureMode.FullScreen); }

        [DataMember(Name = "quality", Order = 4)]
        private string QualityText { get => Quality.ToString(); set => Quality = Parse(value, QualityPreset.Balanced); }

        [DataMember(Name = "codec", Order = 5)]
        private string CodecText { get => Codec.ToString(); set => Codec = Parse(value, VideoCodec.H264); }

        [DataMember(Name = "encoder", Order = 6)]
        private string EncoderText { get => Encoder.ToString(); set => Encoder = Parse(value, EncoderChoice.Auto); }

        [DataMember(Name = "scale", Order = 9)]
        private string ScaleText { get => Scale.ToString(); set => Scale = Parse(value, OutputScale.Original); }

        [DataMember(Name = "container", Order = 10)]
        private string ContainerText { get => Container.ToString(); set => Container = Parse(value, ContainerFormat.Mp4); }

        [DataMember(Name = "screenshotFormat", Order = 22)]
        private string ScreenshotFormatText { get => ScreenshotFormat.ToString(); set => ScreenshotFormat = Parse(value, ScreenshotFormat.Png); }

        /// <summary>Clamps values a hand-edited or older file could carry outside the supported range.</summary>
        public AppSettings Normalize()
        {
            Fps = Math.Max(MinFps, Math.Min(MaxFps, Fps));
            SystemVolume = ClampVolume(SystemVolume);
            MicrophoneVolume = ClampVolume(MicrophoneVolume);
            HotkeyRecord ??= "F12";
            HotkeyPause ??= "Shift+F12";
            HotkeyScreenshot ??= "F11";
            MonitorDevice ??= string.Empty;
            SystemAudioDevice ??= string.Empty;
            MicrophoneDevice ??= string.Empty;
            VideoDevice ??= string.Empty;
            AutoStopMinutes = Math.Max(0, Math.Min(24 * 60, AutoStopMinutes));
            MaxGigabytesPerHour = Array.IndexOf(SizeLimits, MaxGigabytesPerHour) >= 0 ? MaxGigabytesPerHour : 0;
            MicProcessing = (MicProcessing ?? new MicProcessingSettings()).Normalize();
            return this;
        }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context) => ApplyDefaults();

        private void ApplyDefaults()
        {
            // DataContractJsonSerializer skips constructors and field initializers; missing keys must still get defaults.
            var defaults = new AppSettings();
            foreach (var property in typeof(AppSettings).GetProperties())
            {
                if (property.CanWrite && property.CanRead)
                {
                    property.SetValue(this, property.GetValue(defaults));
                }
            }
        }

        private static float ClampVolume(float value) => float.IsNaN(value) ? 1f : Math.Max(0f, Math.Min(2f, value));

        private static T Parse<T>(string? text, T fallback)
            where T : struct =>
            Enum.TryParse<T>(text, ignoreCase: true, out var parsed) && Enum.IsDefined(typeof(T), parsed) ? parsed : fallback;
    }

    /// <summary>Which encoder worked last time, valid while the GPU set and the FFmpeg build stay the same.</summary>
    [DataContract]
    public sealed class EncoderProbeCache
    {
        [DataMember(Name = "fingerprint", Order = 0)] public string Fingerprint { get; set; } = string.Empty;

        [DataMember(Name = "working", Order = 1)] public string[] Working { get; set; } = new string[0];

        [DataMember(Name = "failed", Order = 2)] public string[] Failed { get; set; } = new string[0];
    }
}
