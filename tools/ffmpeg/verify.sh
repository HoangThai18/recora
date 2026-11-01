#!/usr/bin/env bash
# Checks a built FFmpeg directory has everything Recora relies on. Usage: verify.sh <dir with ffmpeg.exe>
# Runs in any bash (Git Bash on the Windows runner), so a cache hit can be verified without MSYS2.
set -euo pipefail

here=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
# shellcheck source=components.sh
. "$here/components.sh"

dir=${1:?usage: verify.sh <dir>}
ffmpeg="$dir/ffmpeg.exe"
status=0

for f in ffmpeg.exe ffprobe.exe BUILDINFO.txt "sources/ffmpeg-$FFMPEG_VERSION.tar.xz"; do
    [ -f "$dir/$f" ] || { echo "missing $f" >&2; status=1; }
done

size=$(wc -c < "$ffmpeg")
echo "ffmpeg.exe: $size bytes (limit $MAX_FFMPEG_BYTES)"
[ "$size" -le "$MAX_FFMPEG_BYTES" ] || { echo "ffmpeg.exe is too large" >&2; status=1; }

filters=$("$ffmpeg" -hide_banner -filters 2>/dev/null | awk '{print $2}')
for f in $REQUIRED_FILTERS; do
    echo "$filters" | grep -qx "$f" || { echo "missing filter $f" >&2; status=1; }
done

encoders=$("$ffmpeg" -hide_banner -encoders 2>/dev/null | awk '{print $2}')
for e in $REQUIRED_ENCODERS; do
    echo "$encoders" | grep -qx "$e" || { echo "missing encoder $e" >&2; status=1; }
done

muxers=$("$ffmpeg" -hide_banner -muxers 2>/dev/null | awk '{print $2}')
for m in $REQUIRED_MUXERS; do
    echo "$muxers" | grep -qx "$m" || { echo "missing muxer $m" >&2; status=1; }
done

if echo "$filters" | grep -qx gfxcapture; then echo "gfxcapture: yes"; else echo "gfxcapture: no (window capture falls back to a rectangle)"; fi

# A real encode through the CPU path proves the binary runs and the static x264 links work.
"$ffmpeg" -hide_banner -loglevel error -f lavfi -i testsrc2=size=320x240:rate=30 -f lavfi -i sine=frequency=440 \
    -t 1 -vf format=yuv420p -c:v libx264 -preset veryfast -c:a aac -f matroska -y "$dir/verify.mkv"
"$dir/ffprobe.exe" -v error -show_entries stream=codec_name -of csv=p=0 "$dir/verify.mkv"
rm -f "$dir/verify.mkv"

exit $status
