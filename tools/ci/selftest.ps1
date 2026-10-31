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
$probe = & $ffprobe -v error -show_entries 'stream=codec_type,codec_name,width,height,start_time:format=duration' -of json $mp4 | ConvertFrom-Json
$probe | ConvertTo-Json -Depth 5 | Write-Host
$video = @($probe.streams | Where-Object codec_type -eq 'video')
$audio = @($probe.streams | Where-Object codec_type -eq 'audio')
$duration = [double]::Parse($probe.format.duration, [Globalization.CultureInfo]::InvariantCulture)
if ($video.Count -ne 1 -or $audio.Count -ne 1) { throw "expected 1 video + 1 audio stream, got $($video.Count) + $($audio.Count)" }
if ([math]::Abs($duration - 3.0) -gt 0.5) { throw "duration $duration s is outside 3 +- 0.5 s" }

# The test tone must really be in the file: decode to mono float and measure its RMS (a 0.2 sine gives ~0.14).
$raw = Join-Path $out 'audio.f32'
& $ffmpeg -v error -y -i $mp4 -map 0:a:0 -ac 1 -ar 8000 -c:a pcm_f32le -f f32le $raw
$bytes = [IO.File]::ReadAllBytes($raw)
$sum = 0.0
for ($i = 0; $i -lt $bytes.Length; $i += 4) { $s = [BitConverter]::ToSingle($bytes, $i); $sum += $s * $s }
$rms = [math]::Sqrt($sum / [math]::Max(1, $bytes.Length / 4))
Write-Host ("audio rms={0:N3} over {1} samples" -f $rms, ($bytes.Length / 4))
if ($rms -lt 0.05) { throw "audio track is silent (rms $rms)" }
Write-Host "selftest OK: $($video[0].codec_name) $($video[0].width)x$($video[0].height), $($audio[0].codec_name), $duration s"
