---
id: P5-T05
title: "Thanh đang ghi trên màn hình (overlay), không lọt vào video"
phase: P5
lane: App
status: done
deps: [P5-T01]
touches: [src/Recora.App/UI/RecordingBar.cs, src/Recora.App/UI/WindowExclusion.cs, src/Recora.App/MainController.cs, src/Recora.Core/Capture/OverlayPlacement.cs, src/Recora.Core/Settings/AppSettings.cs, src/Recora.App/UI/SettingsPages.cs, tools/ci/ui-screenshots.ps1]
size: M
---

## Goal
Lúc ghi, người chơi thấy trên màn hình đang ghi: chấm đỏ, đồng hồ, dung lượng, fps, nút tạm dừng và dừng, như Bandicam.

## Sub-tasks
- [x] `RecordingBar`: cửa sổ layered trong suốt từng điểm ảnh, WS_EX_NOACTIVATE, loại khỏi capture, kéo được, vẽ lại 2 lần/giây
- [x] Không hiện khi Windows không loại được khỏi capture (trước 10 2004)
- [x] Cài đặt `recordingOverlay`, `overlayCorner`; `OverlayPlacement` có test
- [x] CI `--dialog bar`: chụp ảnh thanh và kiểm ảnh chụp màn hình không thấy thanh

## Acceptance criteria
- [x] CI in `hidden from capture: True` cho cả sáng và tối
- [x] Test vị trí 4 góc và màn nhỏ hơn thanh

## Out of scope
- Đo FPS của game (cần móc vào game như Bandicam/MSI Afterburner); thanh hiện fps của bản ghi.
- Hiện trên game toàn màn hình độc quyền.
