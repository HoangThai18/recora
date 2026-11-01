# Every GPU encoder argument list the app builds must be accepted by the shipped ffmpeg.exe. The runner has no GPU,
# so the check reads FFmpeg's own help instead: each option must exist for that encoder (or be a generic codec
# option), and a named value must be one of that option's constants. Then each list really runs on a test pattern,
# where the only acceptable failure is a missing device or driver, never a rejected option.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/../..").Path
$exe = Join-Path $root 'src/Recora.App/bin/Release/net48/Recora.exe'
$ffmpeg = Join-Path $root 'artifacts/ffmpeg/ffmpeg.exe'
$dump = Join-Path $root 'artifacts/selftest/encoder-args.txt'

$process = Start-Process -FilePath $exe -ArgumentList @('--dump-encoder-args', "`"$dump`"") -NoNewWindow -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "dumping encoder arguments failed ($($process.ExitCode))" }

# "  -preset            <int>        E..V....... text" starts an option; deeper-indented lines under it are its
# named constants ("     p4              15           E..V....... medium").
function Read-Options([string[]]$help) {
    $options = @{}
    $current = $null
    foreach ($line in $help) {
        if ($line -match '^\s{1,3}-([A-Za-z0-9_:-]+)\s+<') {
            $current = $Matches[1]
            $options[$current] = New-Object System.Collections.Generic.HashSet[string]
        } elseif ($current -and $line -match '^\s{4,}([A-Za-z0-9_+.-]+)\s+\S*\s*E') {
            [void]$options[$current].Add($Matches[1])
        } elseif ($line -notmatch '^\s') {
            $current = $null
        }
    }
    return $options
}

$generic = Read-Options (& $ffmpeg -hide_banner -h full 2>&1 | Out-String -Stream)
$perEncoder = @{}
$checked = 0
$problems = @()

foreach ($line in Get-Content $dump -Encoding UTF8) {
    if (-not $line) { continue }
    $name, $joined = $line -split "`t", 2
    $list = $joined -split [char]0x1f
    if (-not $perEncoder.ContainsKey($name)) {
        $perEncoder[$name] = Read-Options (& $ffmpeg -hide_banner -h "encoder=$name" 2>&1 | Out-String -Stream)
        if ($perEncoder[$name].Count -eq 0) { $problems += "$name prints no options: is it in the build?" }
    }
    $own = $perEncoder[$name]

    for ($i = 0; $i -lt $list.Count; $i += 2) {
        $option = $list[$i].TrimStart('-') -replace ':v$', ''
        $value = $list[$i + 1]
        if ($option -eq 'c') { continue }
        # Assigned inside each branch: an empty set returned from an if-expression would be unrolled into $null.
        if ($own.ContainsKey($option)) { $known = $own[$option] }
        elseif ($generic.ContainsKey($option)) { $known = $generic[$option] }
        else { $problems += "$name does not know -$option"; continue }
        $isNumber = $value -match '^-?[0-9.]+[kKmM]?$'
        if (-not $isNumber -and $known.Count -gt 0 -and -not $known.Contains($value)) {
            $problems += "$name -$option has no value '$value' (known: $(@($known) -join ', '))"
        }
        $checked++
    }
}

# Real runs: options are applied before the encoder looks for its device, so a bad value fails here even without
# a GPU. Only device/driver errors are acceptable.
$rejected = 'Unrecognized option|Option not found|Error setting option|Unable to parse option|Invalid value|has not been used for any stream'
$ran = 0
foreach ($line in Get-Content $dump -Encoding UTF8 | Select-Object -Unique) {
    if (-not $line) { continue }
    $name, $joined = $line -split "`t", 2
    $list = $joined -split [char]0x1f
    $output = & $ffmpeg -hide_banner -nostdin -loglevel error -f lavfi -i 'testsrc2=size=1280x720:rate=60' -frames:v 3 -an @list -f null - 2>&1 | Out-String
    if ($output -match $rejected) { $problems += "$name rejected its options: $($output.Trim() -replace '\s+', ' ')" }
    $ran++
}

# The check must be able to fail: a real option is found, an invented one is not, and a bad value is rejected.
$nvenc = $perEncoder['h264_nvenc']
if (-not $nvenc -or -not $nvenc.ContainsKey('cq') -or -not $nvenc.ContainsKey('spatial-aq') -or -not $nvenc['rc'].Contains('vbr')) { $problems += "help parsing is broken: h264_nvenc -cq / -spatial-aq / -rc vbr not found" }
if ($nvenc -and $nvenc.ContainsKey('follow_mouse')) { $problems += "help parsing is broken: an invented option was found" }
$bad = & $ffmpeg -hide_banner -nostdin -loglevel error -f lavfi -i 'testsrc2=size=320x240:rate=30' -frames:v 1 -c:v h264_nvenc -rc notamode -f null - 2>&1 | Out-String
if ($bad -notmatch $rejected) { $problems += "a bad value was not rejected, so the run check proves nothing: $($bad.Trim())" }
Write-Host "sanity: h264_nvenc options found $($nvenc.Count), bad value rejected: $($bad -match $rejected)"

foreach ($p in $problems) { Write-Host "PROBLEM: $p" }
Write-Host "encoder options: $checked option/value pairs checked against ffmpeg help, $ran argument lists run on testsrc2"
if ($problems.Count -gt 0) { throw "$($problems.Count) encoder option problem(s)" }
Write-Host "encoder options OK"
