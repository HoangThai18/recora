---
id: P0-T04
title: Core - lệnh ghi một đoạn, lệnh hoàn tất và đọc tiến độ FFmpeg
phase: P0
lane: DESKTOP
status: todo
deps: [P0-T03]
touches: [src/Recora.Core/Ffmpeg/RecordingCommand.cs, src/Recora.Core/Ffmpeg/FinalizeCommand.cs, src/Recora.Core/Ffmpeg/ProbeCommand.cs, src/Recora.Core/Ffmpeg/ProgressParser.cs, src/Recora.Core/Recording/RecordingPlan.cs, tests/Recora.Core.Tests/Ffmpeg/RecordingCommandTests.cs, tests/Recora.Core.Tests/Ffmpeg/FinalizeCommandTests.cs, tests/Recora.Core.Tests/Ffmpeg/ProgressParserTests.cs]
size: M
---

## Goal
Từ một kế hoạch ghi (nguồn, bộ mã hoá, chất lượng, FPS, tiếng) dựng ra đúng lệnh FFmpeg: hình là input 0 qua `-f lavfi` (ADR 4), tiếng là pipe f32le, chuỗi lọc theo bộ mã hoá, ra `.mkv`. Thêm lệnh ghép đoạn, lệnh dò bộ mã hoá, và bộ đọc dòng tiến độ.

## Context to read
- docs/research/capture-encode.md, docs/plan/ARCHITECTURE.md mục 5
- Code mẫu: src/Recora.Core/Ffmpeg/FfmpegArgs.cs (P0-T03)

## Sub-tasks
- [ ] `RecordingPlan` và `RecordingCommand.Build` cho ddagrab, gfxcapture, gdigrab
- [ ] Chuỗi lọc theo bộ mã hoá (không lọc / `scale_d3d11` + `hwmap` / `hwdownload`) và thu nhỏ độ phân giải
- [ ] Tiếng: không tiếng / một pipe trộn / hai track
- [ ] `FinalizeCommand`: danh sách concat (trích dẫn đường dẫn), chép luồng sang MP4 hoặc MKV
- [ ] `ProbeCommand` và `ProgressParser` (khung, fps, kích thước, thời gian, dòng lỗi)

## Acceptance criteria
- [ ] Test: input 0 luôn là nguồn hình, pipe tiếng đứng sau (`RecordingCommand_AudioPipe_ComesAfterVideoInput`)
- [ ] Test: NVENC và AMF không có `hwdownload`; QSV có `hwmap=derive_device=qsv`; x264 có `hwdownload`
- [ ] Test: gfxcapture có `-fps_mode cfr`; ddagrab truyền `output_idx`, `offset_x`, `offset_y`, `video_size` khi ghi vùng
- [ ] Test: đường dẫn có dấu `'` và chữ tiếng Việt trong danh sách concat được thoát đúng
- [ ] Test: đọc dòng `frame=  120 fps= 60 ... time=00:00:02.00 ...` ra số khung 120 và 2 giây

## Security
None.

## How to verify
```bash
dotnet test tests/Recora.Core.Tests
```

## Out of scope
- Chạy tiến trình (P0-T06)

## Notes / decisions
