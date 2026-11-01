---
id: P6-T07
title: "Máy AMD quay game nhẹ hơn mà giữ nét và 60 fps"
phase: P6
lane: App
status: review
deps: [P6-T06]
touches: [src/Recora.Core/Encoding/FrameRoutes.cs, src/Recora.Core/Recording/RecordingPlan.cs, src/Recora.Core/Ffmpeg/RecordingCommand.cs, src/Recora.Core/Encoding/VideoEncoderArgs.cs, src/Recora.App/Recording/CandidateRunner.cs, src/Recora.App/Recording/EncoderMemory.cs]
size: M
---

## Goal
Chủ sản phẩm báo 01/11/2026: quay Delta Force vẫn lag trên máy 16 GB RAM, và không chịu giảm: "vẫn muốn nét, vẫn muốn 60fps". Nhật ký trước đó: AMD Radeon tích hợp, Windows 10 19045.

## Nguyên nhân (đọc từ code, chưa đo trên máy thật)
AMF từ chối khung D3D11 BGRA nên P6-T05 chép cả khung BGRA về RAM rồi swscale đổi NV12 trên CPU, ở nguyên độ phân giải và khung hình người dùng chọn. 1080p60 là khoảng 500 MB/s chép qua bộ nhớ dùng chung với game, thêm đổi màu trên CPU. Hồ sơ nhẹ của P6-T06 không áp cho đường này.

## Việc đã làm
- Bốn đường tới AMF, nhẹ trước: texture BGRA, texture NV12 do GPU đổi, NV12 do GPU đổi rồi chép về RAM, BGRA qua CPU. Nhớ đường nào máy từ chối; khoá `+textures` cũ vẫn đọc được.
- Đường NV12 không tính là dùng CPU, nên FFmpeg giữ ưu tiên thường để video không rớt khung.
- AMF: `-usage lowlatency -quality speed`. QP không đổi nên độ nét giữ nguyên.

## Acceptance criteria
- [x] Test Core: thứ tự đường, chuỗi lọc, thẻ màu, UsesCpu
- [ ] CI xanh (tham số AMF qua encoder-options-check; runner không có GPU nên đường mới không chạy thật)
- [ ] Máy AMD thật: nhật ký ghi đường nào, FPS game khi quay so với khi không quay, màu video đúng
