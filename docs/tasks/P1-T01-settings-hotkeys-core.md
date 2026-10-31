---
id: P1-T01
title: Core - cài đặt (đọc ghi JSON, mặc định, file hỏng) và phím tắt
phase: P1
lane: DESKTOP
status: doing
deps: []
touches: [src/Recora.Core/Settings/**, src/Recora.Core/Hotkeys/**, tests/Recora.Core.Tests/Settings/**, tests/Recora.Core.Tests/Hotkeys/**]
size: M
---

## Goal
Mọi lựa chọn của người dùng nằm trong một `AppSettings` có giá trị mặc định hợp lý, lưu ở `%AppData%\Recora\settings.json`, đọc lỗi thì không mất app. Phím tắt đọc và ghi được dạng chữ `Shift+F12` (BR-04).

## Context to read
- docs/plan/ARCHITECTURE.md mục 4, docs/domain/DOMAIN.md BR-04, BR-08

## Sub-tasks
- [ ] `AppSettings` (DataContract, enum lưu bằng chữ để thêm giá trị mới không làm hỏng file cũ), `Normalize()` kẹp giá trị ngoài dải
- [ ] `SettingsStore.Load/Save`: thiếu file → mặc định; hỏng → đổi tên `.bak` rồi mặc định; ghi qua file tạm rồi thay
- [ ] `Hotkey` (Ctrl, Alt, Shift, Win + phím F1–F24, A–Z, 0–9, PrintScreen, Pause...) `TryParse` / `ToString`

## Acceptance criteria
- [ ] Test: file thiếu → mặc định (F12, Shift+F12, F11, Cân bằng, H.264, 60 fps, MP4, tiếng máy bật, micro tắt)
- [ ] Test: JSON hỏng → tạo `.bak`, trả mặc định; JSON có enum lạ (`"quality":"Ultra"`) → giữ mặc định cho trường đó, giữ trường khác
- [ ] Test: lưu rồi đọc lại bằng nhau; FPS 0 hay 999 bị kẹp về [5, 240]
- [ ] Test: `Ctrl+Shift+F12` đọc ra đúng phím và bổ trợ, `ToString` trả lại đúng chuỗi; chuỗi rác → `TryParse` false

## Security
None.

## How to verify
```bash
dotnet test tests/Recora.Core.Tests
```

## Out of scope
- Giao diện cài đặt (P1-T06), đăng ký phím với Windows (P1-T07)
