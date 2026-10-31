# BR-05: Recora's own window must not appear in its videos. The self-test puts the window (excluded from capture) in
# the middle of a black screen and records; here the window's area in the video must be as black as the rest.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/../..").Path
$out = Join-Path $root 'artifacts/selftest/exclusion'
$exe = Join-Path $root 'src/Recora.App/bin/Release/net48/Recora.exe'
$ffmpeg = Join-Path $root 'artifacts/ffmpeg/ffmpeg.exe'
$env:RECORA_FFMPEG = $ffmpeg

$process = Start-Process -FilePath $exe -ArgumentList @('--selftest-exclusion', "`"$out`"") -NoNewWindow -Wait -PassThru
$info = @{}
Get-Content (Join-Path $out 'exclusion.txt') | ForEach-Object { $k, $v = $_ -split '=', 2; $info[$k] = $v }
$info.GetEnumerator() | ForEach-Object { Write-Host "$($_.Key) = $($_.Value)" }
if ($process.ExitCode -ne 0) { throw "exclusion self-test exited with $($process.ExitCode)" }

$x, $y, $w, $h = $info.window -split ',' | ForEach-Object { [int]$_ }
# Stay a few pixels inside the window so the shadow/border antialiasing at its edge doesn't count.
$crop = "crop={0}:{1}:{2}:{3}" -f ($w - 40), ($h - 40), ($x + 20), ($y + 20)
$gray = Join-Path $out 'window.gray'
& $ffmpeg -v error -y -ss 1.0 -i $info.output -frames:v 1 -vf "$crop,scale=32:18,format=gray" -f rawvideo $gray
$px = [IO.File]::ReadAllBytes($gray)
if ($px.Length -eq 0) { throw "could not read a frame from $($info.output)" }
$mean = ($px | Measure-Object -Average).Average
Write-Host ("window area in the video: mean brightness {0:N1} of 255 (the window itself is light blue, about 240)" -f $mean)
if ($mean -gt 40) { throw "Recora's window shows up in its own video (brightness $mean)" }
Write-Host "exclusion OK"
