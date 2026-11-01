# "Dựng phim" on the runner: Clipora is downloaded and checked against SHA256SUMS, starts with --control, its window
# is embedded in a Recora form, a fresh recording lands on a timeline, and Clipora gets its own window back.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/../..").Path
$out = Join-Path $root 'artifacts/selftest/editor'
$exe = Join-Path $root 'src/Recora.App/bin/Release/net48/Recora.exe'
$env:RECORA_FFMPEG = Join-Path $root 'artifacts/ffmpeg/ffmpeg.exe'

$process = Start-Process -FilePath $exe -ArgumentList @('--selftest-editor', "`"$out`"") -NoNewWindow -Wait -PassThru
$info = @{}
if (Test-Path (Join-Path $out 'editor.txt')) {
    Get-Content (Join-Path $out 'editor.txt') -Encoding UTF8 | ForEach-Object { $k, $v = $_ -split '=', 2; $info[$k] = $v; Write-Host "$k = $v" }
}

if ($process.ExitCode -ne 0) {
    # Clipora keeps its own log next to clipora.exe in the portable build: show its end to see why.
    if ($info.clipora) {
        $logs = Get-ChildItem (Split-Path $info.clipora) -Recurse -Include *.log -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 2
        foreach ($log in $logs) { Write-Host "--- $($log.FullName)"; Get-Content $log -Tail 40 }
    }
    throw "editor self-test exited with $($process.ExitCode)"
}

if ($info.embedded -ne 'True') { throw "Clipora's window was not embedded" }
if ($info.fits -ne 'True') { throw "Clipora's window kept a frame or does not cover the page" }
if ([int]$info.added -lt 1) { throw "the recording did not reach Clipora" }
if ($info.released -ne 'True') { throw "Clipora's window was not given back" }
Write-Host "editor checks OK"
