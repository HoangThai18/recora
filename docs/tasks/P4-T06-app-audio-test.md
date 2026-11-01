---
id: P4-T06
title: "App: thử micro, thử loa, chỉnh micro ở trang Âm thanh"
phase: P4
lane: DESKTOP
status: review
deps: [P4-T03]
touches: [src/Recora.App/Audio/**, src/Recora.App/UI/SettingsPages.cs, src/Recora.App/UI/**, src/Recora.App/MainController.cs, src/Recora.App/SelfTest*.cs]
size: M
---

## Goal
Trang Âm thanh có nút thu thử micro 5 giây rồi phát lại (đã qua xử lý), nút phát tiếng thử trái/phải ra loa đang chọn, và các chỉnh micro; tiếng micro trong video đi qua cùng bộ xử lý.

## Sub-tasks
- [ ] `MicTest` (thu 5 s qua `WasapiSource` + `MicProcessor`, phát bằng `WasapiOut` trên loa đã chọn)
- [ ] `SpeakerTest` (tiếng chuông ngắn trái rồi phải)
- [ ] `ProcessedInput` bọc micro trong `AudioPump`
- [ ] Thẻ "chỉnh micro" ở trang Âm thanh, thanh đo mức sau xử lý

## Acceptance criteria
- [ ] Selftest: tiếng micro giả qua `ProcessedInput` vẫn đồng bộ (bài đo đồng bộ hiện có chạy với xử lý bật)
- [ ] Ảnh chụp trang Âm thanh ở 100% và 150%

## Security
None.

## Out of scope
- Khử ồn bằng mạng nơ-ron (RNNoise), VST.
