---
id: P0-T05
title: Khung cửa sổ chính và chụp giao diện bằng --ui-screenshot
phase: P0
lane: DESKTOP
status: todo
deps: [P0-T01]
touches: [src/Recora.App/UI/**, src/Recora.App/Program.cs, src/Recora.App/Recora.App.csproj, src/Recora.App/Assets/**, tools/icon/**]
size: M
---

## Goal
Cửa sổ chính có bố cục cuối cùng (thanh chế độ, menu trái, nút Ghi đỏ, thanh trạng thái), giao diện sáng xanh pastel của thaipro.store, icon app; `--ui-screenshot` lưu ảnh cửa sổ để kiểm trên CI.

## Context to read
- docs/plan/PRD.md, docs/research/competitors.md (bố cục quen thuộc)

## Sub-tasks
- [ ] `Theme` (màu, font Segoe UI), các control vẽ tay: nút chế độ, nút Ghi, mục menu
- [ ] `MainForm`: thanh chế độ, menu trái, vùng nội dung theo trang, thanh trạng thái
- [ ] Icon app (`.ico` nhiều cỡ) sinh bằng script
- [ ] `--ui-screenshot <file>` vẽ cửa sổ ra PNG rồi thoát

## Acceptance criteria
- [ ] Build xanh; CI chụp được ảnh cửa sổ (P0-T07), chữ tiếng Việt hiện đúng dấu
- [ ] Cửa sổ chạy ở 100% và 150% DPI không vỡ bố cục (ảnh chụp hai mức trên CI)

## Security
None.

## How to verify
```bash
dotnet build Recora.sln -c Release
```

## Out of scope
- Nội dung các trang cài đặt (P1)

## Notes / decisions
