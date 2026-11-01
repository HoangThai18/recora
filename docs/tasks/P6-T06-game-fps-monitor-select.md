---
id: P6-T06
title: "Quay nhẹ hơn cho game, chọn màn hình bằng ô select"
phase: P6
lane: App
status: review
deps: [P6-T05]
touches: [src/Recora.Core/Recording/RecordingPlan.cs, src/Recora.Core/Ffmpeg/RecordingCommand.cs, src/Recora.Core/Settings/AppSettings.cs, src/Recora.App/Recording/CandidateRunner.cs, src/Recora.App/Recording/Recorder.cs, src/Recora.App/Recording/CaptureEnvironment.cs, src/Recora.App/MainController.cs, src/Recora.App/UI/Pages.cs, src/Recora.App/UI/SettingsPages.cs, src/Recora.App/UI/Controls/HomeWidgets.cs, tools/ci/recorder-checks.ps1]
size: M
---

## Goal
Chủ sản phẩm yêu cầu 01/11/2026: "quay sao cho đỡ lag game fps", "cho chọn màn hình, cho select", "test lỗi này kia".

## Việc đã làm
- Xem trước Trang chủ đọc cả màn hình qua GDI mỗi giây dù đang ở trong game, kể cả khi bộ giữ khoảnh khắc chạy: giờ chỉ chạy khi Recora ở trước.
- Khi phải mã hoá bằng CPU: mặc định tối đa 720p, 30 khung/giây (có công tắc, tắt được; người tự chọn CPU không bị giảm). Thu nhỏ bằng `fast_bilinear` trên đường tải khung về RAM.
- FFmpeg ưu tiên dưới thường cho mọi đường dùng CPU, gồm cả AMF qua RAM và GDI.
- Tiếp tục sau tạm dừng giữ nguyên kích thước và tốc độ của đoạn đầu để nối file không hỏng.
- Bộ giữ khoảnh khắc chạy bằng CPU cũng báo người dùng.
- Trang chủ chọn màn hình bằng ô select; danh sách tự cập nhật khi cắm, rút màn hình hoặc đổi độ phân giải (trước đây phải mở lại app).

## Acceptance criteria
- [x] Test Core: LightCpuProfile, UsesCpu, chuỗi lọc
- [x] Selftest CI: yêu cầu 60 fps kích thước gốc với hồ sơ nhẹ, có tạm dừng; video ra tối đa 720p, 30 fps, giải mã hết file
- [ ] Thử trên máy thật AMD: FPS game và log 0.3.5 chưa có
- [ ] Thử trên máy có hai màn hình
