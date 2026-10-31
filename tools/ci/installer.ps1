# Builds recora-<version>-windows-x64-setup.exe (Inno Setup, per-user) from the folder package.ps1 staged.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/../..").Path
$version = ([xml](Get-Content (Join-Path $root 'Directory.Build.props'))).Project.PropertyGroup.Version
$stage = Join-Path $root 'artifacts/package/Recora'
if (-not (Test-Path (Join-Path $stage 'Recora.exe'))) { throw "run package.ps1 first: $stage has no Recora.exe" }

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    choco install innosetup --no-progress -y | Out-Host
    $iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
}

$out = Join-Path $root 'artifacts'
& $iscc /Q "/DVersion=$version" "/DSource=$stage" "/DOutput=$out" "/DIcon=$(Join-Path $root 'src/Recora.App/Assets/recora.ico')" (Join-Path $root 'tools/installer/recora.iss')
if ($LASTEXITCODE -ne 0) { throw "ISCC failed ($LASTEXITCODE)" }
$setup = Join-Path $out "recora-$version-windows-x64-setup.exe"
$size = (Get-Item $setup).Length
Write-Host ("{0}: {1:N1} MB" -f $setup, ($size / 1MB))
if ($size -gt 20MB) { throw "installer is $size bytes, over the 20 MB budget" }

# Silent install and uninstall on the runner: the installed exe must start its self-check.
$install = Start-Process -FilePath $setup -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/TASKS=' -Wait -PassThru
if ($install.ExitCode -ne 0) { throw "silent install exited with $($install.ExitCode)" }
$installed = Join-Path $env:LOCALAPPDATA 'Programs\Recora\Recora.exe'
if (-not (Test-Path $installed) -or -not (Test-Path (Join-Path $env:LOCALAPPDATA 'Programs\Recora\ffmpeg.exe'))) { throw "installer did not put Recora.exe and ffmpeg.exe in place" }
$roundtrip = Start-Process -FilePath $installed -ArgumentList '--settings-roundtrip', "`"$(Join-Path $out 'installed-check')`"" -NoNewWindow -Wait -PassThru
if ($roundtrip.ExitCode -ne 0) { throw "installed Recora.exe failed to run ($($roundtrip.ExitCode))" }
$uninstaller = Get-ChildItem (Join-Path $env:LOCALAPPDATA 'Programs\Recora') -Filter 'unins*.exe' | Select-Object -First 1
$remove = Start-Process -FilePath $uninstaller.FullName -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -Wait -PassThru
Start-Sleep -Seconds 3
if (Test-Path $installed) { throw "uninstall left Recora.exe behind" }
Write-Host "installer OK: install, run, uninstall"
"setup=$setup" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
