---
id: P6-T02
title: "Cắm tai nghe quay video thì tai nghe mất tiếng game"
phase: P6
lane: App
status: review
deps: []
touches: [src/Recora.App/Audio/Ducking.cs, src/Recora.App/Audio/AudioDevices.cs, src/Recora.App/Audio/WasapiSource.cs, src/Recora.Core/Audio/MicrophonePick.cs, src/Recora.App/MainController.cs, src/Recora.App/SelfTest.cs]
size: S
---

## Goal
Chủ sản phẩm báo 01/11/2026: "kết nối tai nghe quay video cái tai nghe không nghe tiếng game nữa".

## Nguyên nhân
Micro được mở là micro liên lạc mặc định (thường là micro tai nghe): Windows coi là cuộc gọi, giảm âm thanh khác 80% (Communications ducking); tai nghe Bluetooth còn chuyển sang Hands-Free, tiếng stereo của game mất. Lưu khoảnh khắc giữ micro mở suốt nên lỗi kéo dài.

## Acceptance criteria
- [x] `SetDuckingPreference(TRUE)` trước khi mở luồng micro; selftest gọi trên mọi thiết bị của runner
- [x] Micro Mặc định tránh micro Bluetooth Hands-Free khi có micro khác (test `MicrophonePickTests`); tự chọn thì cảnh báo
- [ ] Thử trên máy thật có tai nghe Bluetooth
