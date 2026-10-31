---
id: P2-T03
title: Vùng đi theo con trỏ, webcam nổi, vòng tròn chỗ bấm chuột; follow-up của P1
phase: P2
lane: DESKTOP
status: done
deps: [P2-T02]
touches: [src/Recora.Core/Capture/**, src/Recora.Core/Ffmpeg/**, src/Recora.App/**, tests/Recora.Core.Tests/**]
size: M
---

## Goal
Hoàn thiện danh sách của P2: khung 1280×720 đi theo con trỏ (bài hướng dẫn trên màn lớn), webcam nổi ở góc khi ghi màn hình, vòng tròn chỗ bấm chuột. Dọn các follow-up 🔵 của review P1.

## Sub-tasks
- [x] `FollowMouseSource` (gdigrab `-follow_mouse centered`), công tắc ở trang chủ chế độ Chọn vùng
- [x] `WebcamPreviewCommand` + cửa sổ webcam nổi kéo được, hiện trước khi bắt đầu ghi
- [x] Vòng tròn khi bấm chuột (hook chuột cấp thấp, cửa sổ bấm xuyên)
- [x] Follow-up P1: đánh dấu phiên đã lưu để lần quét sau dọn thư mục sót; báo khay khi khôi phục thiếu chỗ; nút lệch ở trang Chung

## Acceptance criteria
- [x] Test Core: theo chuột luôn dùng gdigrab có `-follow_mouse centered`, khung không lớn hơn màn hình; gdigrab thường không theo chuột; lệnh webcam nổi ra khung BGRA 320×240 cố định
- [x] Test Core: phiên có dấu "đã lưu" bị dọn, không bị ghép lại
- [x] CI xanh, ảnh trang Chung có ba công tắc mới

## Security
- Hook chuột chỉ đọc toạ độ và nút bấm, chỉ cài khi đang ghi và tắt khi dừng

## Out of scope
- Hẹn giờ bắt đầu ghi (để bản sau)
