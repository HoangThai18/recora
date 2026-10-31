# Checks the recorder the app uses (run after selftest.ps1): a recording with a pause comes out as one file of the
# right length, and a recording cut by a crash is recovered at the next start (BR-01).
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/../..").Path
$out = Join-Path $root 'artifacts/selftest'
$exe = Join-Path $root 'src/Recora.App/bin/Release/net48/Recora.exe'
$ffprobe = Join-Path $root 'artifacts/ffmpeg/ffprobe.exe'
$env:RECORA_FFMPEG = Join-Path $root 'artifacts/ffmpeg/ffmpeg.exe'
$inv = [Globalization.CultureInfo]::InvariantCulture

function Get-Seconds([string]$file) {
    $probe = & $ffprobe -v error -show_entries 'format=duration:stream=codec_type' -of json $file | ConvertFrom-Json
    $streams = @($probe.streams | ForEach-Object codec_type) -join ','
    return @{ Seconds = [double]::Parse($probe.format.duration, $inv); Streams = $streams }
}

# 1. Pause: 2 s recorded, 1 s paused, 2 s recorded -> about 4 s, video and audio.
$report = Get-Content (Join-Path $out 'selftest.json') -Raw | ConvertFrom-Json
if (-not $report.PausedOutput -or -not (Test-Path $report.PausedOutput)) { throw "recording with a pause produced no file" }
$paused = Get-Seconds $report.PausedOutput
Write-Host ("pause: {0} -> {1:N2} s ({2}), encoder {3}, recorder said {4:N2} s" -f (Split-Path $report.PausedOutput -Leaf), $paused.Seconds, $paused.Streams, $report.PausedEncoder, $report.PausedSeconds)
if ([math]::Abs($paused.Seconds - 4.0) -gt 0.5) { throw "paused recording lasts $($paused.Seconds) s, expected 4 +/- 0.5 s" }
if ($paused.Streams -ne 'video,audio') { throw "paused recording streams are '$($paused.Streams)', expected video,audio" }
if ($report.LeftoverSessions -ne 0) { throw "session folder left behind after a clean stop" }

# 2. Crash: kill Recora 2.5 s into a recording, then recover like the next start does.
$crash = Join-Path $out 'crash'
Remove-Item -Recurse -Force $crash -ErrorAction SilentlyContinue
$process = Start-Process -FilePath $exe -ArgumentList @('--selftest-crash', "`"$crash`"") -NoNewWindow -Wait -PassThru
Write-Host "crash run exited with $($process.ExitCode) (expected non-zero)"
Start-Sleep -Seconds 1
$segments = @(Get-ChildItem -Recurse -Force -Filter '*.mkv' (Join-Path $crash '.recora') -ErrorAction SilentlyContinue)
Write-Host ("left behind: {0}" -f (($segments | ForEach-Object { "$($_.Name) $($_.Length) B" }) -join ', '))
if ($segments.Count -eq 0) { throw "the crash left no segment to recover" }
if (Get-Process ffmpeg -ErrorAction SilentlyContinue) { throw "ffmpeg kept running after Recora was killed (job object did not kill it)" }

$process = Start-Process -FilePath $exe -ArgumentList @('--recover', "`"$crash`"") -NoNewWindow -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "recover exited with $($process.ExitCode)" }
$recovered = @(Get-ChildItem -File -Filter 'Recora *.mp4' $crash)
if ($recovered.Count -ne 1) { throw "expected 1 recovered mp4, found $($recovered.Count)" }
$result = Get-Seconds $recovered[0].FullName
Write-Host ("recovered: {0} -> {1:N2} s ({2})" -f $recovered[0].Name, $result.Seconds, $result.Streams)
if ($result.Seconds -lt 1.5) { throw "recovered recording is only $($result.Seconds) s" }
if (Test-Path (Join-Path $crash '.recora') -PathType Container) {
    $left = @(Get-ChildItem -Directory -Force (Join-Path $crash '.recora'))
    if ($left.Count -ne 0) { throw "recovery left $($left.Count) session folder(s)" }
}

# 3. Settings survive a save and reload under .NET Framework's serializer.
$process = Start-Process -FilePath $exe -ArgumentList @('--settings-roundtrip', "`"$(Join-Path $out 'settings')`"") -NoNewWindow -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "settings roundtrip failed" }

Write-Host "recorder checks OK"
