---
id: P5-T03
title: "Trang Dựng phim: nhúng cửa sổ Clipora, tách ra, nút Dựng ở Thư viện"
phase: P5
lane: DESKTOP
status: doing
deps: [P5-T02]
touches: [src/Recora.App/UI/**, src/Recora.App/MainController.cs]
size: M
---

## Goal
Trang Dựng phim chứa cửa sổ Clipora, co giãn theo Recora; Thư viện có nút Dựng đưa video lên timeline; thoát Recora thì Clipora tách ra cửa sổ riêng, không mất dự án.

## Sub-tasks
- [x] `WindowHost` (SetParent, bỏ khung, co giãn, trả lại khung)
- [x] `EditPage` (giới thiệu, tải có thanh tiến độ, đang chạy)
- [x] Nút Dựng ở Thư viện, icon Dựng phim ở cột trái

## Acceptance criteria
- [ ] CI: cửa sổ Clipora nằm trong form (GetParent), video vào timeline, tách ra được
- [ ] Ảnh trang Dựng phim 100% và 150%

## Security
Tải file chạy được từ mạng: chỉ từ Release của HoangThai18/thaipro-craft qua HTTPS, kiểm SHA-256 với SHA256SUMS.txt cùng Release trước khi giải nén; cổng điều khiển chỉ nghe 127.0.0.1, cổng ngẫu nhiên mỗi lần chạy.

## Out of scope
- Viết lại trình dựng trong Recora.
