# Renders the main window at 100% and a simulated 150% so reviewers can see the UI without a Windows PC.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/../..").Path
$out = Join-Path $root 'artifacts/screenshots'
New-Item -ItemType Directory -Force $out | Out-Null
$exe = Join-Path $root 'src/Recora.App/bin/Release/net48/Recora.exe'

foreach ($scale in @('1', '1.5')) {
    foreach ($page in @('Home', 'Video')) {
        $file = Join-Path $out ("ui-{0}-{1}.png" -f $page.ToLower(), $scale.Replace('.', '_'))
        $process = Start-Process -FilePath $exe -ArgumentList @('--ui-screenshot', "`"$file`"", '--ui-scale', $scale, '--page', $page) -NoNewWindow -Wait -PassThru
        if ($process.ExitCode -ne 0 -or -not (Test-Path $file)) { throw "screenshot $file failed ($($process.ExitCode))" }
        Write-Host "saved $file"
    }
}
