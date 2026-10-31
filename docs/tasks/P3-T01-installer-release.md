---
id: P3-T01
title: Bộ cài Inno Setup, kiểm bản mới, job phát hành khi đẩy tag recora-*
phase: P3
lane: QA
status: done
deps: []
touches: [tools/installer/**, tools/ci/**, .github/workflows/ci.yml, src/Recora.Core/Updates/**, tests/Recora.Core.Tests/Updates/**, src/Recora.App/Platform/UpdateService.cs, src/Recora.App/UI/AboutPage.cs, src/Recora.App/MainController.cs, docs/release-notes/**]
size: M
---

## Goal
CI dựng bộ cài theo người dùng (không cần quyền admin) và zip portable; đẩy tag `recora-x.y.z` là ra GitHub Release đủ file mà trang thaipro.store đọc được. App tự báo khi có bản mới.

## Sub-tasks
- [x] `tools/installer/recora.iss` (per-user, `%LocalAppData%\Programs\Recora`, lối tắt Start, tuỳ chọn Desktop, gỡ sạch kể cả khoá Run)
- [x] `tools/ci/installer.ps1`: dựng, cài im lặng, chạy exe đã cài, gỡ im lặng
- [x] Job `release`: tag phải khớp `Directory.Build.props`; FFmpeg dựng mới từ mã nguồn trong lượt đó; kèm setup, portable, mã nguồn FFmpeg + x264, `SHA256SUMS.txt`; thân Release = ghi chú + `---` + miễn trừ
- [x] `UpdateCheck` (Core) + `UpdateService`: hỏi GitHub một lần sau khi mở và ở trang Giới thiệu

## Acceptance criteria
- [x] CI: `recora-0.1.0-windows-x64-setup.exe` dưới 20 MB, cài/chạy/gỡ im lặng qua
- [x] Test Core: chọn bản mới nhất đúng tiền tố, bỏ bản nháp/thử nghiệm/tag lạ, JSON lỗi không làm hỏng

## Security
- Tag luôn dựng FFmpeg từ mã nguồn (không nhận artifact của lượt khác)
- Kiểm bản mới không gửi định danh nào ngoài phiên bản trong User-Agent

## Out of scope
- Ký số bộ cài, tự cài bản mới
