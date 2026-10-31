---
id: P0-T03
title: Core - FfmpegArgs, nguồn hình, danh mục bộ mã hoá và mức chất lượng
phase: P0
lane: DESKTOP
status: done
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
- [x] `FfmpegArgs`: thêm đối số, nối thành dòng lệnh theo quy tắc `CommandLineToArgvW`
- [x] `CaptureSource` (màn hình, vùng, cửa sổ) và `EvenSize`
- [x] `EncoderKind`, `VideoCodec`, `EncoderCatalog` (tên FFmpeg, thứ tự theo hãng GPU)
- [x] `QualityPreset` → tham số NVENC, AMF, QSV, MF, x264 cho H.264, H.265, AV1

## Acceptance criteria
- [x] Test trích dẫn: dấu cách, ngoặc kép, dấu `\` cuối, chuỗi rỗng khớp quy tắc `CommandLineToArgvW`
- [x] Test làm chẵn: 801×601 → 800×600; nhỏ hơn 160×90 → lỗi `region_too_small`
- [x] Test thứ tự: GPU NVIDIA → NVENC đầu; Intel → QSV đầu; AMD → AMF đầu; luôn kết thúc bằng x264 rồi Media Foundation
- [x] Test thang: AV1 NVENC dùng dải 0–63, AMF AV1 dùng QVBR, mọi giá trị trong dải hợp lệ

## Security
None.

## How to verify
```bash
dotnet test tests/Recora.Core.Tests
```

## Out of scope
- Lệnh ghi đầy đủ và đọc tiến độ (P0-T04)

## Notes / decisions
- 2026-10-31: x264 đứng trước Media Foundation: cùng tốn CPU nhưng x264 cho file nhỏ hơn ở cùng độ nét; MF chỉ còn giá trị khi cần H.265 mà máy không có GPU mã hoá được. GPU ảo (`Microsoft`) bỏ qua các bộ mã hoá phần cứng để khỏi mất vài giây dò.

Review: [docs/reviews/P0-T03.md](../reviews/P0-T03.md) · PASS · merge vào phase/P0
