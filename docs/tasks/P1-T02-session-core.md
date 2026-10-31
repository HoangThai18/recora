---
id: P1-T02
title: Core - phiên ghi, đặt tên file, sổ phiên và kế hoạch khôi phục
phase: P1
lane: DESKTOP
status: done
deps: []
touches: [src/Recora.Core/Recording/SessionState.cs, src/Recora.Core/Recording/SessionManifest.cs, src/Recora.Core/Recording/OutputNaming.cs, src/Recora.Core/Recording/RecoveryPlanner.cs, tests/Recora.Core.Tests/Recording/**]
size: M
---

## Goal
Luật của một phiên ghi nằm trong Core để test được: chuyển trạng thái hợp lệ (DOMAIN mục 4), tên file `Recora 2026-10-31 20-15-03.mp4` không trùng (BR-08), sổ phiên `session.json` cạnh các đoạn, và quyết định phiên nào cần hoàn tất khi mở app (BR-01), ổ đầy thì dừng (BR-06).

## Sub-tasks
- [x] `SessionStateMachine`: Idle → Starting → Recording ⇄ Paused → Finalizing → Idle/Failed, chặn chuyển sai
- [x] `OutputNaming`: tên theo giờ máy, thêm ` (2)` khi trùng, ký tự cấm trong tên Windows bị thay
- [x] `SessionManifest` (DataContract): id, giờ bắt đầu, định dạng, codec, tên file đích, danh sách đoạn
- [x] `RecoveryPlanner`: từ các thư mục phiên còn sót, ra danh sách cần hoàn tất (có đoạn khác rỗng) và cần dọn
- [x] `DiskGuard.ShouldStop(freeBytes)` dưới 500 MB

## Acceptance criteria
- [x] Test chuyển trạng thái: Pause khi Idle bị từ chối; Recording → Paused → Starting → Recording hợp lệ
- [x] Test tên: trùng hai lần → ` (2)`, ` (3)`; tên tuỳ chỉnh có `:` `?` bị thay bằng `-`
- [x] Test khôi phục: phiên có 2 đoạn (một rỗng) → hoàn tất với đoạn không rỗng; phiên toàn đoạn rỗng → dọn; thư mục lạ không có manifest → bỏ qua
- [x] Test sổ phiên lưu rồi đọc lại bằng nhau

## Security
None.

## How to verify
```bash
dotnet test tests/Recora.Core.Tests
```

## Out of scope
- Chạy FFmpeg, giao diện (P1-T05)
