# Renders the main window at 100% and a simulated 150% so reviewers can see the UI without a Windows PC.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/../..").Path
$out = Join-Path $root 'artifacts/screenshots'
New-Item -ItemType Directory -Force $out | Out-Null
$exe = Join-Path $root 'src/Recora.App/bin/Release/net48/Recora.exe'

# The runner's desktop starts at 1024x768, which would squeeze the 150% preview (a 1410x960 window) and hide layout
# bugs instead of showing them. Ask for 1920x1080 first, then refuse to call a squeezed picture a 150% check.
try { Set-DisplayResolution -Width 1920 -Height 1080 -Force -ErrorAction Stop } catch { Write-Host "Set-DisplayResolution: $_" }
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Write-Host "primary screen: $([System.Windows.Forms.Screen]::PrimaryScreen.Bounds)"

$library = Join-Path $root 'artifacts/selftest/recorder'
$shots = @()
foreach ($scale in @('1', '1.5')) {
    foreach ($page in @('Home', 'Video', 'Audio', 'Screenshot', 'Hotkeys', 'General', 'Library', 'Edit', 'About')) {
        $shots += ,@($scale, $page, $false)
    }
    $shots += ,@($scale, 'Home', $true)
    $shots += ,@($scale, 'Library', 'trim')
}

# The dark look: every page at 100%, plus one window switched from light to dark while open.
foreach ($page in @('Home', 'Video', 'Audio', 'Screenshot', 'Hotkeys', 'General', 'Library', 'Edit', 'About')) {
    $shots += ,@('1', $page, $false, 'dark')
}
$shots += ,@('1', 'Home', $true, 'dark')
$shots += ,@('1', 'Library', 'trim', 'dark')
$shots += ,@('1', 'Home', $false, 'switch')

foreach ($shot in $shots) {
    $scale, $page, $recording, $theme = $shot
    # A string marks a dialog shot; comparing $true -eq 'trim' would be true in PowerShell.
    $dialog = if ($recording -is [string]) { $recording } else { $null }
    if ($dialog) { $recording = $false }
    $suffix = if ($recording) { '-recording' } elseif ($dialog) { "-$dialog" } else { '' }
    $prefix = if ($theme) { "$theme-" } else { '' }
    $file = Join-Path $out ("ui-{0}{1}{2}-{3}.png" -f $prefix, $page.ToLower(), $suffix, $scale.Replace('.', '_'))
    $arguments = @('--ui-screenshot', "`"$file`"", '--ui-scale', $scale, '--page', $page)
    if ($theme) { $arguments += @('--theme', $theme) }
    if (Test-Path $library) { $arguments += @('--library', "`"$library`"") }
    if ($recording) { $arguments += '--recording' }
    if ($dialog) { $arguments += @('--dialog', $dialog) }
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -NoNewWindow -Wait -PassThru
    if ($process.ExitCode -ne 0 -or -not (Test-Path $file)) { throw "screenshot $file failed ($($process.ExitCode))" }
    $image = [System.Drawing.Image]::FromFile($file)
    $width = $image.Width; $height = $image.Height
    $image.Dispose()
    Write-Host "saved $file ($width x $height)"
    if ($scale -eq '1.5' -and $width -lt 1400 -and -not $dialog) { throw "150% screenshot is only $width px wide: the screen is too small to show the real layout" }
}

# The recording bar, and proof a screen capture doesn't see it (exit 2 means it showed up in the capture).
foreach ($theme in @('light', 'dark')) {
    $file = Join-Path $out "ui-recording-bar-$theme.png"
    $process = Start-Process -FilePath $exe -ArgumentList @('--ui-screenshot', "`"$file`"", '--dialog', 'bar', '--theme', $theme) -NoNewWindow -Wait -PassThru
    if ($process.ExitCode -ne 0 -or -not (Test-Path $file)) { throw "recording bar shot failed ($($process.ExitCode))" }
}

# A short film of the window moving (switches, picker, page changes, record button), for reviewing the animations.
$demo = Join-Path $out 'ui-demo-1_5.mp4'
$env:RECORA_FFMPEG = Join-Path $root 'artifacts/ffmpeg/ffmpeg.exe'
$process = Start-Process -FilePath $exe -ArgumentList @('--ui-screenshot', "`"$demo`"", '--ui-scale', '1.5', '--page', 'Home', '--demo') -NoNewWindow -Wait -PassThru
if ($process.ExitCode -ne 0 -or -not (Test-Path $demo)) { throw "demo video failed ($($process.ExitCode))" }
Write-Host "demo video: $demo ($([math]::Round((Get-Item $demo).Length / 1MB, 2)) MB)"

