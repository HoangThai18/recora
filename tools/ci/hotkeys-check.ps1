# P6-T04: F12 must reach Recora while a game that blocks RegisterHotKey (raw input with RIDEV_NOHOTKEYS) is in front,
# exactly once per press; and the Home window list must show an open window.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/../..").Path
$out = Join-Path $root 'artifacts/selftest/hotkeys'
$exe = Join-Path $root 'src/Recora.App/bin/Release/net48/Recora.exe'

$process = Start-Process -FilePath $exe -ArgumentList @('--selftest-hotkeys', "`"$out`"") -NoNewWindow -Wait -PassThru
$info = @{}
Get-Content (Join-Path $out 'hotkeys.txt') | ForEach-Object { $k, $v = $_ -split '=', 2; $info[$k] = $v }
$info.GetEnumerator() | Sort-Object Key | ForEach-Object { Write-Host "$($_.Key) = $($_.Value)" }
if ($info['poll.presses'] -ne '2') { throw "2 taps of F9 with a key-blocking window in front gave $($info['poll.presses']) presses, expected 2" }
if ($info['notepad.listed'] -ne 'True') { throw 'the window list does not show the Notepad window' }
if ($process.ExitCode -ne 0) { throw "hotkeys self-test exited with $($process.ExitCode)" }
Write-Host "hotkeys OK (RegisterHotKey alone saw $($info['control.presses']) of 2)"
