---
id: P5-T06
title: "Giao diện sáng kiểu Windows 11 và tối kiểu app gaming, đổi khi đang mở"
phase: P5
lane: App
status: done
deps: [P5-T01]
touches: [src/Recora.App/UI/**, src/Recora.App/Native/NativeMethods.cs, src/Recora.App/Program.cs, src/Recora.App/MainController.cs, src/Recora.Core/Settings/AppSettings.cs, tools/ci/ui-screenshots.ps1]
size: L
---

## Goal
Người dùng chê xanh pastel trông cũ; chọn có cả sáng lẫn tối, mặc định theo Windows.

## Sub-tasks
- [x] `Palette.Light`, `Palette.Dark`; `Theme.Use` đổi màu mọi điều khiển đang mở; theo dõi Windows đổi sáng/tối
- [x] Thanh tiêu đề tối (DWM), thanh cuộn, ComboBox, ListView theo bảng màu
- [x] Bỏ "// tiêu đề", "> dòng", thanh trạng thái xanh đậm; thẻ bo 12; Segoe UI Variable khi có
- [x] Chung → Giao diện: Theo Windows, Sáng, Tối
- [x] CI chụp bản tối mọi trang và cửa sổ đổi sáng sang tối khi đang mở

## Acceptance criteria
- [x] Ảnh `ui-dark-*` không còn mảng sáng sót; `ui-switch-home-1` giống `ui-dark-home-1`
- [x] Test cài đặt: theme, góc thanh, giá trị lạ về mặc định

## Out of scope
- Mica/Acrylic thật (WinForms vẽ nền đặc).
