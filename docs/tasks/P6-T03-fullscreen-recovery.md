---
id: P6-T03
title: "Quay Liên Minh toàn màn hình ra video đen"
phase: P6
lane: App
status: review
deps: []
touches: [src/Recora.App/Recording/Recorder.cs, src/Recora.App/Recording/Session.cs, src/Recora.App/Recording/CandidateRunner.cs, src/Recora.App/SelfTest*.cs, tools/ci/recorder-checks.ps1]
size: M
---

## Goal
Chủ sản phẩm báo 01/11/2026: Liên Minh, chế độ Game, game để Toàn màn hình: video đen.

## Nguyên nhân
Desktop Duplication mất quyền (DXGI_ERROR_ACCESS_LOST) khi game vào/ra exclusive fullscreen; ddagrab của FFmpeg không tự mở lại mà thoát. Recorder không theo dõi FFmpeg giữa chừng: đồng hồ chạy, không có khung nào sau đó.

## Acceptance criteria
- [x] Bộ canh mỗi giây: FFmpeg tự thoát khi đang ghi thì đóng đoạn, mở đoạn mới (cùng backend 5 lần, rồi mọi backend), hết cách thì lưu phần đã ghi
- [ ] CI: giết FFmpeg giữa chừng, file có cả đen trước lẫn trắng sau, dài gần 5 s
- [ ] Thử trên máy thật với Liên Minh toàn màn hình
