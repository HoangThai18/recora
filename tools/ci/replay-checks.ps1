# "Lưu khoảnh khắc" on the runner: the ring wraps without growing, two saves work while it keeps running, each
# clip holds about the chosen length with picture and sound, and stopping removes the ring.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/../..").Path
$out = Join-Path $root 'artifacts/selftest/replay'
$exe = Join-Path $root 'src/Recora.App/bin/Release/net48/Recora.exe'
$ffprobe = Join-Path $root 'artifacts/ffmpeg/ffprobe.exe'
$env:RECORA_FFMPEG = Join-Path $root 'artifacts/ffmpeg/ffmpeg.exe'
$inv = [Globalization.CultureInfo]::InvariantCulture

$process = Start-Process -FilePath $exe -ArgumentList @('--selftest-replay', "`"$out`"") -NoNewWindow -Wait -PassThru
$info = @{}
Get-Content (Join-Path $out 'replay.txt') | ForEach-Object { $k, $v = $_ -split '=', 2; $info[$k] = $v }
if ($process.ExitCode -ne 0) { throw "replay self-test exited with $($process.ExitCode)" }

$chunks = [int]$info.chunks
$ring = [int]$info.ring
Write-Host "ring: $chunks chunk file(s) on disk, ring size $ring, encoder $($info.encoder)"
if ($chunks -gt $ring) { throw "the ring holds $chunks chunks, more than its $ring" }
if ($chunks -lt $ring - 1) { throw "only $chunks chunks after running longer than the ring: it never wrapped" }
if ($info.running_after_saves -ne 'True') { throw "the buffer stopped after saving" }
if ($info.folder_removed -ne 'True') { throw "stopping left the ring folder behind" }

$wanted = [double]::Parse($info.seconds, $inv)
foreach ($key in @('first', 'second')) {
    $file = $info[$key]
    if (-not $file -or -not (Test-Path $file)) { throw "$key save produced no file" }
    $p = & $ffprobe -v error -show_entries 'stream=codec_type,codec_name:format=duration' -of json $file | ConvertFrom-Json
    $types = @($p.streams | ForEach-Object codec_type) -join ','
    $seconds = [double]::Parse($p.format.duration, $inv)
    Write-Host ("{0}: {1} ({2}) {3:N2} s" -f $key, (Split-Path $file -Leaf), $types, $seconds)
    if ($types -ne 'video,audio') { throw "$key has streams '$types', expected video,audio" }
    if ($seconds -lt $wanted - 0.5 -or $seconds -gt $wanted + 2.5) { throw "$key lasts $seconds s, expected $wanted to $($wanted + 2.5) s" }
}
Write-Host "replay checks OK"
