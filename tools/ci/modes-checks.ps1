# P2 modes on the runner: a region comes out at its exact size, sound-only files have sound and no picture,
# and F11 saves a PNG the size of the screen.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/../..").Path
$out = Join-Path $root 'artifacts/selftest/modes'
$exe = Join-Path $root 'src/Recora.App/bin/Release/net48/Recora.exe'
$ffprobe = Join-Path $root 'artifacts/ffmpeg/ffprobe.exe'
$env:RECORA_FFMPEG = Join-Path $root 'artifacts/ffmpeg/ffmpeg.exe'
$inv = [Globalization.CultureInfo]::InvariantCulture

$process = Start-Process -FilePath $exe -ArgumentList @('--selftest-modes', "`"$out`"") -NoNewWindow -Wait -PassThru
$info = @{}
Get-Content (Join-Path $out 'modes.txt') | ForEach-Object { $k, $v = $_ -split '=', 2; $info[$k] = $v }
if ($process.ExitCode -ne 0) { throw "modes self-test exited with $($process.ExitCode)" }

function Probe([string]$file) {
    & $ffprobe -v error -show_entries 'stream=codec_type,codec_name,width,height:format=duration' -of json $file | ConvertFrom-Json
}

$region = Probe $info.region
$v = @($region.streams | Where-Object codec_type -eq 'video')[0]
Write-Host ("region: {0}x{1} {2}, {3} s" -f $v.width, $v.height, $v.codec_name, $region.format.duration)
if ($v.width -ne 640 -or $v.height -ne 360) { throw "region recorded at $($v.width)x$($v.height), expected 640x360" }

$window = Probe $info.window
$w = @($window.streams | Where-Object codec_type -eq 'video')[0]
Write-Host ("window: {0}x{1} {2}, {3} s" -f $w.width, $w.height, $w.codec_name, $window.format.duration)
if ($w.width -ne 800 -or $w.height -ne 600) { throw "window recorded at $($w.width)x$($w.height), expected 800x600" }

$audioKeys = @('audio_m4a', 'audio_wav')
if ($info.audio_mp3) { $audioKeys += 'audio_mp3' } else { Write-Host "audio_mp3: mp3_mf (Media Foundation) is not available on this runner; see the app log" }
foreach ($key in $audioKeys) {
    $file = $info[$key]
    $p = Probe $file
    $types = @($p.streams | ForEach-Object codec_type) -join ','
    $seconds = [double]::Parse($p.format.duration, $inv)
    Write-Host ("{0}: {1} ({2}, {3}) {4:N2} s" -f $key, (Split-Path $file -Leaf), $types, (@($p.streams | ForEach-Object codec_name) -join ','), $seconds)
    if ($types -ne 'audio') { throw "$key has streams '$types', expected audio only" }
    if ([math]::Abs($seconds - 2.0) -gt 0.6) { throw "$key lasts $seconds s, expected about 2 s" }
}
if ([IO.Path]::GetExtension($info.audio_m4a) -ne '.m4a' -or [IO.Path]::GetExtension($info.audio_wav) -ne '.wav') { throw "wrong file extensions" }

Add-Type -AssemblyName System.Drawing
$image = [System.Drawing.Image]::FromFile($info.screenshot)
$size = "{0}x{1}" -f $image.Width, $image.Height
$image.Dispose()
Write-Host "screenshot: $(Split-Path $info.screenshot -Leaf) $size (screen $($info.screen))"
if ($size -ne $info.screen) { throw "screenshot is $size, screen is $($info.screen)" }
Write-Host "modes checks OK"
