---
id: P0-T06
title: Chạy FFmpeg, bơm tiếng qua pipe và chế độ --selftest
phase: P0
lane: DESKTOP
status: doing
deps: [P0-T04]
touches: [src/Recora.Core/Audio/**, tests/Recora.Core.Tests/Audio/**, src/Recora.App/Recording/FfmpegProcess.cs, src/Recora.App/Recording/FfmpegLocator.cs, src/Recora.App/Audio/AudioPump.cs, src/Recora.App/SelfTest.cs, src/Recora.App/Program.cs, src/Recora.App/Logging/**, src/Recora.App/Native/**, src/Recora.App/Recora.App.csproj]
size: M
---

## Goal
Đường ghi thật tối thiểu: chạy `ffmpeg.exe` ẩn, đọc tiến độ, dừng bằng `q`; `AudioPump` ghi tiếng theo đồng hồ app vào named pipe (chèn im lặng khi nguồn không có dữ liệu); `--selftest` ghi 3 giây bằng bộ mã hoá đầu tiên chạy được rồi ghi báo cáo JSON.

## Context to read
- docs/plan/ARCHITECTURE.md mục 5, docs/research/capture-encode.md mục đồng bộ
- src/Recora.Core/Ffmpeg/RecordingCommand.cs (P0-T04)

## Sub-tasks
- [ ] Core: `AudioRingBuffer` (độ trễ tối đa), `AudioMixer` (trộn, giới hạn biên độ) + test
- [ ] App: `FfmpegProcess` (ẩn cửa sổ, stdin `q`, stderr → `ProgressParser`, nhật ký), `FfmpegLocator`
- [ ] App: `AudioPump` theo `Stopwatch`, named pipe, nguồn im lặng tổng hợp
- [ ] `--selftest <dir>`: dò bộ mã hoá, ghi 3 giây, báo cáo `selftest.json`, mã thoát 0/1

## Acceptance criteria
- [ ] Test Core: bộ đệm thiếu dữ liệu thì đệm 0; vượt độ trễ tối đa thì bỏ phần cũ; trộn hai nguồn có giới hạn [-1, 1]
- [ ] Trên CI (P0-T07): selftest ra MP4 3 ± 0,5 giây có hình và tiếng

## Security
None.

## How to verify
```bash
dotnet test tests/Recora.Core.Tests && dotnet build Recora.sln -c Release
```

## Out of scope
- Thiết bị WASAPI thật, tạm dừng, nhiều đoạn (P1)

## Notes / decisions
- 2026-10-31: thêm `Native/**` (AttachConsole cho chế độ dòng lệnh, Job Object để FFmpeg chết theo Recora, MonitorFromPoint) và csproj (tham chiếu System.Runtime.Serialization cho báo cáo JSON).
