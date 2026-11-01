#!/usr/bin/env bash
# Builds Recora's minimal, fully static ffmpeg.exe and ffprobe.exe.
# Run inside an MSYS2 UCRT64 shell with the packages listed in .github/workflows/ci.yml.
# Output: $OUT_DIR (default artifacts/ffmpeg) with the binaries, BUILDINFO.txt and the GPL sources.
set -euo pipefail

here=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
root=$(cd "$here/../.." && pwd)
# shellcheck source=components.sh
. "$here/components.sh"

work=${WORK_DIR:-$root/build/ffmpeg}
out=${OUT_DIR:-$root/artifacts/ffmpeg}
jobs=${JOBS:-$(nproc)}

join() { local IFS=,; echo "$*"; }

mkdir -p "$work" "$out/sources"
tarball="ffmpeg-$FFMPEG_VERSION.tar.xz"

# ffmpeg.org sometimes resets connections from CI runners (curl 35, which plain --retry does not retry). Every
# Recora release carries the same tarball in its sources archive, so that is the fallback; the SHA-256 check below
# makes the origin irrelevant.
fetch() { curl -fsSL --retry 5 --retry-all-errors --retry-delay 5 -o "$1" "$2"; }
mirror="https://github.com/HoangThai18/recora/releases/download/$FALLBACK_RELEASE/$FALLBACK_RELEASE-ffmpeg-sources.tar.gz"

if [ ! -f "$work/$tarball" ]; then
    if ! fetch "$work/$tarball" "https://ffmpeg.org/releases/$tarball"; then
        echo "ffmpeg.org failed; using the copy in $FALLBACK_RELEASE"
        fetch "$work/mirror.tar.gz" "$mirror"
        tar -xzf "$work/mirror.tar.gz" -C "$work" "sources/$tarball"
        mv "$work/sources/$tarball" "$work/$tarball"
    fi
fi
echo "$FFMPEG_SHA256  $work/$tarball" | sha256sum -c -

src="$work/src"
rm -rf "$src"
mkdir -p "$src"
tar -xf "$work/$tarball" -C "$src" --strip-components=1
cd "$src"

echo "pkg-config vpl: $(pkg-config --static --libs vpl 2>&1 || true)"
echo "pkg-config x264: $(pkg-config --static --libs x264 2>&1 || true)"

# --disable-autodetect keeps the binary free of whatever the build machine happens to have (zlib, iconv...);
# every library and hardware API is then opted in explicitly. Option order matters to configure.
# shellcheck disable=SC2086
./configure \
    --prefix="$work/prefix" \
    --extra-version=recora \
    --pkg-config-flags=--static \
    --extra-ldflags=-static \
    --extra-libs="-lstdc++" \
    --disable-debug --disable-doc --disable-ffplay --disable-network \
    --disable-autodetect \
    --enable-gpl --enable-libx264 --enable-libvpl \
    --enable-d3d11va --enable-ffnvcodec --enable-nvenc --enable-amf --enable-mediafoundation \
    --disable-everything \
    --enable-indev="$(join $INDEVS)" \
    --enable-filter="$(join $FILTERS)" \
    --enable-encoder="$(join $ENCODERS)" \
    --enable-decoder="$(join $DECODERS)" \
    --enable-demuxer="$(join $DEMUXERS)" \
    --enable-muxer="$(join $MUXERS)" \
    --enable-parser="$(join $PARSERS)" \
    --enable-protocol="$(join $PROTOCOLS)" \
    --enable-bsfs \
    > "$out/configure.log" 2>&1 || {
        cat "$out/configure.log"
        echo "---- tail of ffbuild/config.log" >&2
        tail -n 120 ffbuild/config.log >&2
        exit 1
    }
cat "$out/configure.log"

# gfxcapture needs WinRT headers that not every mingw-w64 has; the app checks `ffmpeg -filters` at runtime.
if grep -q '^#define CONFIG_GFXCAPTURE_FILTER 1' config_components.h; then
    gfxcapture=yes
else
    gfxcapture=no
fi

make -j"$jobs"
strip ffmpeg.exe ffprobe.exe
cp ffmpeg.exe ffprobe.exe "$out/"
cp COPYING.GPLv2 COPYING.GPLv3 COPYING.LGPLv2.1 COPYING.LGPLv3 LICENSE.md "$out/sources/"

# GPL: ship the exact sources next to the binaries (FFmpeg tarball plus the MSYS2 source package of x264).
cp "$work/$tarball" "$out/sources/"
x264_version=$(pacman -Q mingw-w64-ucrt-x86_64-libx264 | awk '{print $2}')
x264_source="mingw-w64-x264-$x264_version.src.tar.zst"
if ! fetch "$out/sources/$x264_source" "https://repo.msys2.org/mingw/sources/$x264_source"; then
    [ -f "$work/mirror.tar.gz" ] || fetch "$work/mirror.tar.gz" "$mirror"
    echo "repo.msys2.org failed; using the copy in $FALLBACK_RELEASE"
    tar -xzf "$work/mirror.tar.gz" -C "$work" "sources/$x264_source" \
        || { echo "$x264_source is not in $FALLBACK_RELEASE either" >&2; exit 1; }
    cp "$work/sources/$x264_source" "$out/sources/$x264_source"
fi

dlls=$(objdump -p ffmpeg.exe | awk '/DLL Name/ {print $3}' | sort -u)
{
    echo "ffmpeg $FFMPEG_VERSION (sha256 $FFMPEG_SHA256)"
    echo "gfxcapture=$gfxcapture"
    echo "ffmpeg.exe bytes=$(stat -c %s ffmpeg.exe)"
    echo "ffprobe.exe bytes=$(stat -c %s ffprobe.exe)"
    echo "-- packages"
    pacman -Q mingw-w64-ucrt-x86_64-gcc mingw-w64-ucrt-x86_64-crt-git mingw-w64-ucrt-x86_64-headers-git \
        mingw-w64-ucrt-x86_64-libx264 mingw-w64-ucrt-x86_64-libvpl \
        mingw-w64-ucrt-x86_64-ffnvcodec-headers mingw-w64-ucrt-x86_64-amf-headers 2>/dev/null || true
    echo "-- imported DLLs"
    echo "$dlls"
    echo "-- ffmpeg -version"
    ./ffmpeg.exe -hide_banner -version
} > "$out/BUILDINFO.txt"
cat "$out/BUILDINFO.txt"

# A DLL from MSYS2 (libwinpthread-1.dll, libstdc++-6.dll, libx264-*.dll...) would not exist on users' PCs.
if echo "$dlls" | grep -qi '^lib'; then
    echo "error: ffmpeg.exe depends on non-system DLLs:" >&2
    echo "$dlls" | grep -i '^lib' >&2
    exit 1
fi
