---
id: P6-T04
title: "F12 không chạy trong game, không chọn được cửa sổ để ghi"
phase: P6
lane: App
status: review
deps: [P6-T03]
touches: [src/Recora.Core/Hotkeys/HotkeyPoller.cs, src/Recora.Core/Capture/WindowChoice.cs, src/Recora.Core/Settings/AppSettings.cs, src/Recora.App/Platform/HotkeyManager.cs, src/Recora.App/UI/CapturePicker.cs, src/Recora.App/UI/Pages.cs, src/Recora.App/MainController.cs, src/Recora.App/SelfTest.Hotkeys.cs, tools/ci/hotkeys-check.ps1]
size: M
---

## Goal
Chủ sản phẩm báo 01/11/2026: "game toàn màn hình bấm F12 vẫn không quay", "không chọn được cửa sổ nào để quay".

## Nguyên nhân
- Phím tắt chỉ đi qua RegisterHotKey. Game đăng ký raw input bàn phím với RIDEV_NOHOTKEYS (Liên Minh và nhiều game khác) thì Windows không gửi WM_HOTKEY cho app khác khi game đang được chọn.
- Chế độ Cửa sổ chỉ có khung chọn bằng chuột hiện ra sau khi bấm Ghi. Bấm F12 trong game toàn màn hình thì khung đó không hiện được trên game; trang chủ không có chỗ chọn cửa sổ trước.

## Acceptance criteria
- [x] Đọc phím trực tiếp mỗi 15 ms (GetAsyncKeyState, như OBS) song song với RegisterHotKey; modifier phải khớp đúng; một lần bấm chỉ tính một lần (HotkeyGate 400 ms)
- [x] Trang chủ, chế độ Cửa sổ: danh sách cửa sổ đang mở (đọc lại mỗi lần mở danh sách), lưu theo tên chương trình + tiêu đề; chương trình chưa mở vẫn hiện "(chưa mở)"
- [x] Chưa chọn cửa sổ: bấm phím khi cửa sổ khác Recora đang ở trước thì ghi cửa sổ đó; bấm từ Recora thì vẫn chọn bằng chuột
- [x] Test Core: HotkeyPoller, HotkeyGate, WindowChoice
- [x] CI: cửa sổ giả game (RIDEV_NOHOTKEYS) ở trước, SendInput F9 hai lần → đúng 2 lần; danh sách có Notepad (run 41295765909: RegisterHotKey một mình nhận 0/2, có đọc phím 2/2)
- [ ] Thử trên máy thật với Liên Minh
