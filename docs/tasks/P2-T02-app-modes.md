---
id: P2-T02
title: Đủ chế độ trong app - chọn vùng, cửa sổ, Game, webcam, chỉ âm thanh; chụp ảnh F11, đếm ngược, tự dừng
phase: P2
lane: DESKTOP
status: done
deps: [P2-T01]
touches: [src/Recora.App/**, src/Recora.Core/Settings/**, tests/Recora.Core.Tests/Settings/**, tools/ci/**, .github/workflows/ci.yml]
size: L
---

## Goal
Sáu nút chế độ trên thanh trên cùng đều ghi được. Bấm Ghi ở Chọn vùng thì kéo chuột chọn vùng (bấm một cửa sổ để lấy cả cửa sổ); Cửa sổ thì bấm cửa sổ cần ghi; Game ghi màn hình đang chơi khi bấm F12 trong game; Webcam ghi webcam hoặc thẻ capture; Âm thanh ghi tiếng máy và micro ra M4A, MP3, WAV. F11 chụp đúng thứ chế độ đó sẽ ghi.

## Sub-tasks
- [x] Lớp phủ chọn vùng/cửa sổ (mỗi màn hình một lớp, làm tối, nhãn kích thước, Esc huỷ); khung viền nét đứt quanh vùng đang ghi, bấm xuyên được, không lọt video
- [x] Game: màn hình chứa cửa sổ đang ở trước lúc bấm phím
- [x] Webcam: danh sách dshow, chọn ở trang chủ; webcam bị app khác giữ thì báo rõ
- [x] Âm thanh: Recorder mở đoạn âm thanh (sẵn sàng khi out_time > 0), Finalizer ghép theo định dạng trong sổ phiên
- [x] Trang chủ đổi theo chế độ (màn hình, webcam, định dạng âm thanh, ảnh giữ chỗ)
- [x] F11 (PNG/JPG, clipboard, âm báo) và trang Ảnh chụp; đếm ngược 3 giây; tự dừng sau N phút; nhớ chế độ
- [x] Selftest `--selftest-modes` và `tools/ci/modes-checks.ps1`

## Acceptance criteria
- [x] CI: vùng 640×360 ra MP4 đúng 640×360
- [x] CI: âm thanh ra `.m4a` (aac) và `.wav` (pcm), chỉ có luồng tiếng, khoảng 2 giây
- [x] CI: F11 ra PNG đúng kích thước màn hình chính
- [x] Test Core: cài đặt mới đọc ghi được, giá trị lạ về mặc định
- [x] Ảnh CI trang chủ, trang Ảnh chụp, trang Chung ở 100% và 150%

## Security
- Lớp phủ và khung viền đặt WDA_EXCLUDEFROMCAPTURE, không bao giờ lọt vào video hay ảnh chụp

## Out of scope
- Webcam nổi trên màn hình khi ghi màn hình, hiệu ứng bấm chuột
