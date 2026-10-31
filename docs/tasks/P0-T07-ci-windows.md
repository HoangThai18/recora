---
id: P0-T07
title: CI Windows - build app, test, selftest, kiểm bằng ffprobe, đóng gói zip
phase: P0
lane: INFRA
status: doing
deps: [P0-T02, P0-T05, P0-T06]
touches: [.github/workflows/ci.yml, tools/ci/**]
size: M
---

## Goal
Mỗi lần push, CI trên `windows-latest` build app, chạy test, chạy selftest ghi thật với FFmpeg vừa build, kiểm file bằng ffprobe, chụp giao diện, đóng gói zip portable.

## Context to read
- AGENTS.md (lệnh), P0-T02 (job ffmpeg)

## Sub-tasks
- [ ] Job `app`: setup .NET 10, build Release, test
- [ ] Selftest với `ffmpeg.exe` từ job `ffmpeg`, `tools/ci/check-recording.ps1` dùng ffprobe
- [ ] `--ui-screenshot` ở 100% và 150% DPI, upload ảnh
- [ ] Đóng gói `recora-<version>-windows-x64-portable.zip`, upload artifact

## Acceptance criteria
- [ ] CI xanh trên `phase/P0`; artifact zip dưới 20 MB; ảnh cửa sổ có trong artifact
- [ ] ffprobe: 1 luồng hình, 1 luồng tiếng, thời lượng 3 ± 0,5 giây

## Security
None.

## How to verify
Xem lượt chạy CI trên nhánh `phase/P0`.

## Out of scope
- Bộ cài, Release (P3)

## Notes / decisions
