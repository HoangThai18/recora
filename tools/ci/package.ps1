# Builds recora-<version>-windows-x64-portable.zip: Recora.exe, its DLLs, ffmpeg.exe and the licenses.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/../..").Path
$version = ([xml](Get-Content (Join-Path $root 'Directory.Build.props'))).Project.PropertyGroup.Version
$bin = Join-Path $root 'src/Recora.App/bin/Release/net48'
$stage = Join-Path $root 'artifacts/package/Recora'
Remove-Item -Recurse -Force (Join-Path $root 'artifacts/package') -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force (Join-Path $stage 'licenses') | Out-Null

Copy-Item (Join-Path $bin '*') $stage -Include '*.exe', '*.dll', '*.config'
Copy-Item (Join-Path $root 'artifacts/ffmpeg/ffmpeg.exe') $stage
Copy-Item (Join-Path $root 'LICENSE') (Join-Path $stage 'licenses/Recora-LICENSE.txt')
Copy-Item (Join-Path $root 'artifacts/ffmpeg/sources/COPYING.GPLv2') (Join-Path $stage 'licenses/FFmpeg-COPYING.GPLv2.txt')
Copy-Item (Join-Path $PSScriptRoot 'THIRD-PARTY-NOTICES.txt') $stage

$zip = Join-Path $root "artifacts/recora-$version-windows-x64-portable.zip"
Remove-Item $zip -ErrorAction SilentlyContinue
Compress-Archive -Path $stage -DestinationPath $zip
Get-ChildItem $stage -Recurse | Select-Object FullName, Length | Format-Table -AutoSize | Out-String | Write-Host
$size = (Get-Item $zip).Length
Write-Host ("{0}: {1:N1} MB" -f $zip, ($size / 1MB))
if ($size -gt 20MB) { throw "portable zip is $size bytes, over the 20 MB budget" }
"zip=$zip" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
