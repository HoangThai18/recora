---
id: P3-T02
title: Phát hành recora-0.1.0 và đăng lên thaipro.store/phan-mem/recora
phase: P3
lane: QA
status: done
deps: [P3-T01]
touches: [docs/**, "thaidev: config/software.php", "thaidev: public/assets/phan-mem/recora*"]
size: S
---

## Goal
Người dùng vào thaipro.store/phan-mem/recora thấy Recora 0.1.0 với nút tải bộ cài và bản portable.

## Sub-tasks
- [x] Đẩy tag `recora-0.1.0`, Release công khai đủ file
- [x] Mục `recora` trong `config/software.php` của thaidev, biểu tượng và 3 ảnh giới thiệu chụp từ CI
- [x] Test thaidev cho trang phần mềm vẫn xanh; push main (deploy tự động)
- [x] Bản thử zip portable vào ~/Downloads

## Acceptance criteria
- [x] Release `recora-0.1.0` công khai có setup.exe, portable.zip, mã nguồn FFmpeg, SHA256SUMS.txt
- [x] Trang `/phan-mem/recora` hiện phiên bản 0.1.0 và nút tải (kiểm bằng curl sau deploy)

## Security
None.

## Out of scope
- Thử tay trên máy có GPU (ghi chú trong review)
