---
id: P4-T01
title: "Mức Tiết kiệm và giới hạn dung lượng mỗi giờ cho mọi bộ mã hoá"
phase: P4
lane: BE
status: done
deps: []
touches: [src/Recora.Core/Encoding/**, src/Recora.Core/Settings/AppSettings.cs, src/Recora.Core/Recording/RecordingPlan.cs, tests/Recora.Core.Tests/Encoding/**, tests/Recora.Core.Tests/Settings/**]
size: S
---

## Goal
Người dùng chọn mức "Tiết kiệm" hoặc đặt trần 1/2/4/8 GB mỗi giờ; mọi bộ mã hoá (NVENC, AMF, QSV, x264, MF) tôn trọng trần đó.

## Sub-tasks
- [x] `QualityPreset.Saver` (q 31, 0,06 bit/điểm ảnh, tiếng 96k)
- [x] `AppSettings.MaxGigabytesPerHour` (0, 1, 2, 4, 8; giá trị lạ về 0)
- [x] `VideoEncodeOptions.MaxVideoKbps`: trần = min(trần theo mức, dung lượng/giờ trừ tiếng)
- [x] Có trần: AMF H.264/H.265 chuyển `-rc qvbr`, QSV thêm `-b:v` + `-maxrate` (QVBR), x264 thêm `-maxrate`/`-bufsize`, MF `pc_vbr`

## Acceptance criteria
- [x] Test: 1 GB/giờ ở 1080p60 cho trần video ≤ 2 222 kbps trừ tiếng, với mọi bộ mã hoá
- [x] Test: không đặt trần thì tham số không đổi so với 0.1.1 (trừ mức mới)
- [x] Tên tham số khớp mã nguồn FFmpeg 8.1.3 (`qsvenc.c` chọn QVBR khi có global_quality + bit_rate + maxrate; `amfenc_h264.c`/`amfenc_hevc.c` có `qvbr`, `qvbr_quality_level`; `mfenc.c` có `pc_vbr`)

## Security
None.

## Out of scope
- Giao diện chọn mức và trần (P4-T04 làm ở trang Video).
