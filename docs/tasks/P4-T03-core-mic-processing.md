---
id: P4-T03
title: "Bộ xử lý micro: tăng âm, lọc tiếng ù, chặn ồn nền, nén tiếng, chống rè"
phase: P4
lane: BE
status: todo
deps: []
touches: [src/Recora.Core/Audio/**, src/Recora.Core/Settings/AppSettings.cs, tests/Recora.Core.Tests/Audio/**, tests/Recora.Core.Tests/Settings/**]
size: M
---

## Goal
Tiếng micro qua một chuỗi xử lý trong app (trước khi trộn) giống phần mềm livestream, có số đo trong test.

## Sub-tasks
- [ ] `MicProcessingSettings` trong `AppSettings` (tăng âm 0–20 dB, lọc ù, chặn ồn + ngưỡng, nén, chống rè)
- [ ] `MicProcessor`: lọc thông cao 80 Hz (biquad), cổng ồn (bao hình, giữ, mở/đóng mượt), nén 3:1 có bù, chống rè trần −1 dBFS
- [ ] Xử lý từng khối, trạng thái giữ qua các khối, `Reset()`

## Acceptance criteria
- [ ] Test: tiếng 30 Hz giảm ≥ 12 dB khi bật lọc ù, 1 kHz giữ trong ±1 dB
- [ ] Test: ồn −55 dBFS bị chặn xuống dưới −80 dBFS khi ngưỡng −45, giọng −20 dBFS đi qua
- [ ] Test: chống rè giữ đỉnh ≤ −1 dBFS với tín hiệu +6 dB; nén thu hẹp chênh lệch to/nhỏ
- [ ] Test: tắt hết thì đầu ra bằng đầu vào

## Security
None.

## Out of scope
- Giao diện, thu thử (P4-T06).
