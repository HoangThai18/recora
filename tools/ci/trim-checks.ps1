# "Cắt video" on the runner: the length of a file is read like the dialog reads foreign files, a preview still
# decodes, and the cut keeps picture and sound for the chosen span (the start may move back to a keyframe).
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/../..").Path
$out = Join-Path $root 'artifacts/selftest/trim'
$exe = Join-Path $root 'src/Recora.App/bin/Release/net48/Recora.exe'
$ffprobe = Join-Path $root 'artifacts/ffmpeg/ffprobe.exe'
$env:RECORA_FFMPEG = Join-Path $root 'artifacts/ffmpeg/ffmpeg.exe'
$inv = [Globalization.CultureInfo]::InvariantCulture

$process = Start-Process -FilePath $exe -ArgumentList @('--selftest-trim', "`"$out`"") -NoNewWindow -Wait -PassThru
$info = @{}
Get-Content (Join-Path $out 'trim.txt') | ForEach-Object { $k, $v = $_ -split '=', 2; $info[$k] = $v }
if ($process.ExitCode -ne 0) { throw "trim self-test exited with $($process.ExitCode)" }

function Probe([string]$file) {
    & $ffprobe -v error -show_entries 'stream=codec_type:format=duration' -of json $file | ConvertFrom-Json
}

$source = Probe $info.source
$sourceSeconds = [double]::Parse($source.format.duration, $inv)
$read = [double]::Parse($info.source_seconds, $inv)
Write-Host ("source: {0:N2} s by ffprobe, {1:N2} s read by Recora, preview {2}" -f $sourceSeconds, $read, $info.preview_ok)
if ([math]::Abs($sourceSeconds - $read) -gt 0.1) { throw "Recora read $read s, ffprobe says $sourceSeconds s" }

$cut = Probe $info.output
$types = @($cut.streams | ForEach-Object codec_type) -join ','
$seconds = [double]::Parse($cut.format.duration, $inv)
$span = [double]::Parse($info.to, $inv) - [double]::Parse($info.from, $inv)
Write-Host ("cut: {0} ({1}) {2:N2} s for a {3:N2} s span" -f (Split-Path $info.output -Leaf), $types, $seconds, $span)
if ($types -ne 'video,audio') { throw "the cut has streams '$types', expected video,audio" }
# A keyframe up to 0.25 s after the start is taken, one before it at most 2 s back.
if ($seconds -lt $span - 0.35 -or $seconds -gt $span + 2.1) { throw "the cut lasts $seconds s for a $span s span" }
if (-not (Split-Path $info.output -Leaf).Contains('(cắt)')) { throw "unexpected cut name $($info.output)" }
Write-Host "trim checks OK"
