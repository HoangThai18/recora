---
id: P6-T05
title: "Máy AMD ghi bằng CPU làm game tụt FPS, nhật ký báo lỗi"
phase: P6
lane: App
status: review
deps: [P6-T04]
touches: [src/Recora.Core/Encoding/FrameRoutes.cs, src/Recora.Core/Ffmpeg/FfmpegFailure.cs, src/Recora.Core/Ffmpeg/RecordingCommand.cs, src/Recora.Core/Encoding/VideoEncoderArgs.cs, src/Recora.App/Recording/CandidateRunner.cs, src/Recora.App/Recording/EncoderMemory.cs, src/Recora.App/Recording/FfmpegProcess.cs, src/Recora.App/UI/RecordingBar.cs, src/Recora.App/UI/SettingsPages.cs, src/Recora.App/Audio/AudioPump.cs]
size: M
---

## Goal
Chủ sản phẩm báo 01/11/2026: "báo lỗi", "quay lag kinh khủng, game tụt FPS". Nhật ký `recora-20261101.log`, Windows 10 19045, AMD Radeon Graphics tích hợp, Recora 0.3.0.

## Nguyên nhân
- `h264_amf` từ chối khung D3D11 BGRA ở khung đầu (`SubmitInput() failed with error 18`). QSV và NVENC không có trên máy này nên `libx264` 1080p60 chạy trên CPU ở mức ưu tiên thường: mọi lần ghi trong nhật ký đều vậy.
- Lý do lỗi chỉ lấy 4 dòng cuối stderr (`Qavg`, `Conversion failed!`), không có tên bộ mã hoá nên `EncoderMemory` không bao giờ ghi nhận hỏng.
- Mỗi lần thử hỏng làm đứt ống tiếng, ghi `ERROR` kèm stack (59 lần).
- Hộp chọn thiết bị tiếng dùng `DataSource` chưa có `BindingContext` khi tạo handle: `SelectedIndex = 0` trên danh sách rỗng ném lỗi trên luồng UI.
- `SetWindowDisplayAffinity` lỗi 8 chỉ xảy ra với `RecordingBar`, cửa sổ layered được gọi trước `UpdateLayeredWindow` đầu tiên (giả thuyết, cần máy thật xác nhận).

## Acceptance criteria
- [x] AMF: thử khung D3D11, hỏng thì thử NV12 qua bộ nhớ hệ thống trước khi lùi sang CPU; nhớ đường chạy được
- [x] x264 dùng `superfast`, FFmpeg chạy ưu tiên dưới thường khi mã hoá bằng CPU
- [x] Báo một lần mỗi phiên khi đang ghi bằng CPU ngoài ý người dùng
- [x] Lý do lỗi lấy dòng lỗi đầu tiên; test Core
- [x] Hộp chọn thiết bị tiếng không ném lỗi; ống tiếng đứt chỉ ghi `WARN`
- [x] Thanh đang ghi đặt affinity sau lần vẽ đầu
- [ ] Thử trên máy thật AMD: nhật ký có `h264_amf (system memory)`, game không tụt như trước
