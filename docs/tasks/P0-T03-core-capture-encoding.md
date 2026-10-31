---
id: P0-T03
title: Core - FfmpegArgs, nguồn hình, danh mục bộ mã hoá và mức chất lượng
phase: P0
lane: DESKTOP
status: todo
deps: [P0-T01]
touches: [src/Recora.Core/Ffmpeg/FfmpegArgs.cs, src/Recora.Core/Capture/**, src/Recora.Core/Encoding/**, tests/Recora.Core.Tests/Ffmpeg/FfmpegArgsTests.cs, tests/Recora.Core.Tests/Capture/**, tests/Recora.Core.Tests/Encoding/**]
size: M
---

## Goal
Các khối dựng lệnh cơ bản: trích dẫn dòng lệnh Windows, mô tả nguồn hình (làm chẵn kích thước, BR-07), danh mục bộ mã hoá kèm thứ tự dự phòng theo hãng GPU (BR-02), quy đổi mức chất lượng ra tham số từng bộ mã hoá.

## Context to read
- docs/research/capture-encode.md (bảng đường đi khung, thang chất lượng)
- docs/domain/DOMAIN.md BR-02, BR-07

## Sub-tasks
- [ ] `FfmpegArgs`: thêm đối số, nối thành dòng lệnh theo quy tắc `CommandLineToArgvW`
- [ ] `CaptureSource` (màn hình, vùng, cửa sổ) và `EvenSize`
- [ ] `EncoderKind`, `VideoCodec`, `EncoderCatalog` (tên FFmpeg, thứ tự theo hãng GPU)
- [ ] `QualityPreset` → tham số NVENC, AMF, QSV, MF, x264 cho H.264, H.265, AV1

## Acceptance criteria
- [ ] Test trích dẫn: dấu cách, ngoặc kép, dấu `\` cuối, chuỗi rỗng khớp quy tắc `CommandLineToArgvW`
- [ ] Test làm chẵn: 801×601 → 800×600; nhỏ hơn 160×90 → lỗi `region_too_small`
- [ ] Test thứ tự: GPU NVIDIA → NVENC đầu; Intel → QSV đầu; AMD → AMF đầu; luôn kết thúc bằng MF rồi x264
- [ ] Test thang: AV1 NVENC dùng dải 0–63, AMF AV1 dùng QVBR, mọi giá trị trong dải hợp lệ

## Security
None.

## How to verify
```bash
dotnet test tests/Recora.Core.Tests
```

## Out of scope
- Lệnh ghi đầy đủ và đọc tiến độ (P0-T04)

## Notes / decisions
