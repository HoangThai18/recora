# Records a few seconds on the runner through Recora's real pipeline, then checks the MP4 with ffprobe.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/../..").Path
$out = Join-Path $root 'artifacts/selftest'
New-Item -ItemType Directory -Force $out | Out-Null
$exe = Join-Path $root 'src/Recora.App/bin/Release/net48/Recora.exe'
$ffmpeg = Join-Path $root 'artifacts/ffmpeg/ffmpeg.exe'
$ffprobe = Join-Path $root 'artifacts/ffmpeg/ffprobe.exe'
$env:RECORA_FFMPEG = $ffmpeg

$process = Start-Process -FilePath $exe -ArgumentList @('--selftest', "`"$out`"") -NoNewWindow -Wait -PassThru
Get-Content (Join-Path $out 'selftest.json') -Raw | Write-Host
if ($process.ExitCode -ne 0) { throw "selftest exited with $($process.ExitCode)" }

$mp4 = Join-Path $out 'selftest.mp4'
$probe = & $ffprobe -v error -show_entries 'stream=codec_type,codec_name,width,height,start_time,duration:format=duration' -of json $mp4 | ConvertFrom-Json
$probe | ConvertTo-Json -Depth 5 | Write-Host
$video = @($probe.streams | Where-Object codec_type -eq 'video')
$audio = @($probe.streams | Where-Object codec_type -eq 'audio')
$inv = [Globalization.CultureInfo]::InvariantCulture
$duration = [double]::Parse($probe.format.duration, $inv)
if ($video.Count -ne 1 -or $audio.Count -ne 1) { throw "expected 1 video + 1 audio stream, got $($video.Count) + $($audio.Count)" }

# Lengths: FFmpeg's scheduler keeps audio from running ahead of video, so when 'q' arrives the audio still queued
# in the pipe (about the video encoder's delay) is dropped; audio may end a little early, never late.
$videoSeconds = [double]::Parse($video[0].duration, $inv)
$audioSeconds = [double]::Parse($audio[0].duration, $inv)
Write-Host ("video {0:N3} s, audio {1:N3} s, file {2:N3} s" -f $videoSeconds, $audioSeconds, $duration)
if ($audioSeconds - $videoSeconds -gt 0.1 -or $videoSeconds - $audioSeconds -gt 0.5) { throw "audio ($audioSeconds s) vs video ($videoSeconds s) lengths are off" }
if ($duration -lt 2.5 -or $duration -gt 6.0) { throw "duration $duration s is outside the 2.5-6 s sanity range" }

# The test tone must really be in the file: decode to mono float and measure its RMS (a 0.2 sine gives ~0.14).
$raw = Join-Path $out 'audio.f32'
& $ffmpeg -v error -y -i $mp4 -map 0:a:0 -ac 1 -ar 8000 -c:a pcm_f32le -f f32le $raw
$bytes = [IO.File]::ReadAllBytes($raw)
$sum = 0.0
for ($i = 0; $i -lt $bytes.Length; $i += 4) { $s = [BitConverter]::ToSingle($bytes, $i); $sum += $s * $s }
$rms = [math]::Sqrt($sum / [math]::Max(1, $bytes.Length / 4))
Write-Host ("audio rms={0:N3} over {1} samples" -f $rms, ($bytes.Length / 4))
if ($rms -lt 0.05) { throw "audio track is silent (rms $rms)" }
# Sync: the screen turned white and the beep started at the same instant. Find both in the file and compare.
$gray = Join-Path $out 'frames.gray'
& $ffmpeg -v error -y -i $mp4 -map 0:v:0 -vf 'fps=30,scale=32:18,format=gray' -f rawvideo $gray
$px = [IO.File]::ReadAllBytes($gray)
$frameSize = 32 * 18
$firstWhite = -1
for ($f = 0; $f -lt [math]::Floor($px.Length / $frameSize); $f++) {
    $sum = 0
    for ($i = 0; $i -lt $frameSize; $i++) { $sum += $px[$f * $frameSize + $i] }
    if ($sum / $frameSize -gt 200) { $firstWhite = $f; break }
}
$firstLoud = -1
for ($i = 0; $i -lt $bytes.Length; $i += 4) { if ([math]::Abs([BitConverter]::ToSingle($bytes, $i)) -gt 0.1) { $firstLoud = $i / 4; break } }
if ($firstWhite -lt 0 -or $firstLoud -lt 0) { throw "sync markers not found (white frame $firstWhite, beep sample $firstLoud)" }
$videoAt = $firstWhite / 30.0
$audioAt = $firstLoud / 8000.0
$offsetMs = ($videoAt - $audioAt) * 1000
Write-Host ("sync: white frame at {0:N3} s, beep at {1:N3} s, video minus audio = {2:N0} ms" -f $videoAt, $audioAt, $offsetMs)
# The flash needs a compositor frame or two to reach the screen, so video may trail a little; sound must not.
if ($offsetMs -lt -100 -or $offsetMs -gt 150) { throw "audio/video offset $offsetMs ms is out of range" }

Write-Host "selftest OK: $($video[0].codec_name) $($video[0].width)x$($video[0].height), $($audio[0].codec_name), $duration s"
