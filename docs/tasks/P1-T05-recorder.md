---
id: P1-T05
title: Recorder - phiên nhiều đoạn, tạm dừng, tự lùi bộ mã hoá, hoàn tất, khôi phục; dò bộ mã hoá lúc mở app
phase: P1
lane: DESKTOP
status: done
deps: [P1-T01, P1-T02, P1-T03, P1-T04]
touches: [src/Recora.App/Recording/Recorder.cs, src/Recora.App/Recording/Session.cs, src/Recora.App/Recording/Finalizer.cs, src/Recora.App/Recording/EncoderMemory.cs, src/Recora.App/Recording/RecoveryService.cs, src/Recora.App/Recording/CaptureEnvironment.cs, src/Recora.App/SelfTest*.cs, src/Recora.App/Program.cs, src/Recora.App/Recording/LibraryFiles.cs, src/Recora.Core/Library/**, tests/Recora.Core.Tests/Library/**, tools/ci/recorder-checks.ps1, .github/workflows/ci.yml]
size: L
---

## Goal
Một lớp lo trọn phiên ghi: bắt đầu bằng bộ mã hoá tốt nhất còn dùng được (BR-02), tạm dừng và tiếp tục bằng đoạn mới, dừng thì ghép thành một file, tắt ngang thì lần mở sau hoàn tất (BR-01), ổ gần đầy thì dừng gọn (BR-06). Selftest chạy qua chính lớp này.

## Sub-tasks
- [x] `CaptureEnvironment`: khả năng FFmpeg (bộ lọc, bộ mã hoá), danh sách màn hình, lấy một lần lúc mở app
- [x] `EncoderMemory`: nhớ bộ mã hoá chạy được và hỏng trên máy này (kèm dấu vân tay GPU + phiên bản FFmpeg), lần sau thử cái chạy được trước; codec người dùng chọn luôn đứng trước H.264 dự phòng
- [x] `Recorder`: Start / Pause / Resume / Stop, mỗi đoạn một FFmpeg + một `AudioPump`, FFmpeg chết trong 4 giây đầu chưa có khung → thử ứng viên kế tiếp; sự kiện trạng thái (thời gian, dung lượng, fps)
- [x] `RecoveryService`: quét `.recora` trong thư mục lưu (nhanh, xong trước khi cho ghi), hoàn tất theo `RecoveryPlanner` (chạy nền)
- [x] Ghi thời lượng file vừa lưu vào `library.json` (`LibraryIndex`, `DurationStore`) cho trang Thư viện
- [x] Selftest dùng `Recorder`: ghi, tạm dừng, ghi tiếp, dừng; `--selftest-crash` tự giết giữa chừng, `--recover <thư mục>` hoàn tất

## Acceptance criteria
- [x] CI: selftest hai đoạn ra một MP4, thời lượng bằng tổng hai đoạn ± 0,5 giây
- [x] CI: sau `--selftest-crash`, `--recover` ra MP4 dài ≥ 1,5 giây
- [x] CI: báo cáo ghi rõ các ứng viên đã thử và lý do hỏng
- [x] Test Core: chỉ mục thời lượng đọc ghi được, file ngoài app không có thời lượng vẫn hiện, ảnh được đánh dấu ảnh

## Security
None.

## Out of scope
- Giao diện điều khiển (P1-T06)
