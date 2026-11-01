---
id: P6-T01
title: "Cập nhật ngay trong app (tải, kiểm SHA-256, cài im lặng, tự mở lại)"
phase: P6
lane: App
status: review
deps: []
touches: [src/Recora.Core/Updates/UpdateCheck.cs, src/Recora.App/Platform/Updater.cs, src/Recora.App/Platform/Downloads.cs, src/Recora.App/Editor/CliporaRuntime.cs, src/Recora.App/UI/AboutPage.cs, src/Recora.App/MainController.cs, tools/installer/recora.iss, tools/ci/installer.ps1]
size: M
---

## Goal
Chủ sản phẩm (01/11/2026): "sao không có nút cập nhật trên app, chứ sao phải lên web tải về nữa".

## Acceptance criteria
- [x] UpdateCheck đọc bộ cài và SHA256SUMS của bản mới (test)
- [ ] CI: cài đè im lặng với /RELAUNCH=1 mở lại Recora
- [x] Đang ghi thì không cài; bản portable mở trang tải

## Security
Chỉ tải từ Release của HoangThai18/recora qua HTTPS, so SHA-256 với SHA256SUMS.txt cùng Release trước khi chạy.
