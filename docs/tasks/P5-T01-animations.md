---
id: P5-T01
title: "Chuyển động mượt cho giao diện, video demo trên CI"
phase: P5
lane: DESKTOP
status: review
deps: []
touches: [src/Recora.Core/Animation/**, tests/Recora.Core.Tests/Animation/**, src/Recora.App/UI/**, src/Recora.App/Program.cs, tools/ci/ui-screenshots.ps1, docs/plan/PHASES.md]
size: M
---

## Goal
Giao diện có chuyển động: hover, công tắc, ô chọn, đổi trang, nút ghi, mức âm, thanh trạng thái. Không tốn gì khi cửa sổ ẩn; tắt theo cài đặt Windows.

## Sub-tasks
- [x] `Tween`, `Easing` ở Core có test
- [x] `Animator` (một timer dùng chung, tự dừng, tôn trọng SPI_GETCLIENTAREAANIMATION)
- [x] Nút, công tắc, ô chọn, nút ghi, mức âm, chấm trạng thái, chấm đang ghi, đổi trang
- [x] `--ui-demo` quay video demo trên CI

## Acceptance criteria
- [ ] Test Core cho tween xanh
- [ ] CI có `ui-demo-1_5.mp4`, ảnh chụp tĩnh không đổi (Animator tắt khi chụp)

## Security
None.

## Out of scope
- Dựng phim (P5-T02..).
