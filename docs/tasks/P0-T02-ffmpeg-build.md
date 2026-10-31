---
id: P0-T02
title: Script build FFmpeg 8.1.3 tối giản và job CI có cache
phase: P0
lane: INFRA
status: todo
deps: []
touches: [tools/ffmpeg/**, .github/workflows/ci.yml]
size: M
---

## Goal
Có `ffmpeg.exe` (và `ffprobe.exe` cho test) liên kết tĩnh, chỉ gồm phần Recora cần, dưới 20 MB, build trên GitHub Actions và được cache.

## Context to read
- docs/research/capture-encode.md (danh sách bộ lọc, bộ mã hoá, gói MSYS2)

## Sub-tasks
- [ ] `tools/ffmpeg/build.sh`: tải tarball FFmpeg 8.1.3 (kiểm SHA-256), configure `--disable-everything` rồi bật đúng thành phần, `-static`, strip
- [ ] `tools/ffmpeg/verify.sh`: kiểm có đủ thành phần bắt buộc và kích thước
- [ ] Job `ffmpeg` trong `ci.yml`: MSYS2 UCRT64, cache theo hash của `tools/ffmpeg/**`, upload artifact
- [ ] Ghi kèm artifact: phiên bản, cấu hình, mã nguồn FFmpeg và x264 (cho Release sau này)

## Acceptance criteria
- [ ] Job `ffmpeg` xanh; `ffmpeg -hide_banner -filters` có `ddagrab`, `scale_d3d11`, `hwmap`, `hwdownload`; `-encoders` có `h264_nvenc`, `hevc_nvenc`, `av1_nvenc`, `h264_amf`, `h264_qsv`, `h264_mf`, `libx264`, `aac`, `mp3_mf`
- [ ] `ffmpeg.exe` dưới 20 MB, chạy được không cần DLL nào ngoài DLL hệ thống
- [ ] Lần chạy thứ hai dùng cache (không build lại)

## Security
Tải mã nguồn từ ffmpeg.org: kiểm SHA-256 ghi cứng trong script.

## How to verify
Xem log job `ffmpeg` trên GitHub Actions.

## Out of scope
- Build app, selftest (P0-T07)

## Notes / decisions
`gfxcapture` cần header WinRT của mingw-w64; không có thì để tắt, ghi lại trong log.
