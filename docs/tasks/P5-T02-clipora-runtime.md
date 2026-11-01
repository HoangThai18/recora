---
id: P5-T02
title: "Tìm hoặc tải Clipora (kiểm SHA-256), cổng điều khiển"
phase: P5
lane: BE
status: doing
deps: []
touches: [src/Recora.Core/Editor/**, tests/Recora.Core.Tests/Editor/**, src/Recora.App/Editor/**, src/Recora.App/Recora.App.csproj]
size: M
---

## Goal
Recora tìm Clipora đã cài hoặc tải bản portable mới nhất từ Release `clipora-*`, kiểm SHA256SUMS, giải nén vào %LocalAppData%\Recora\Clipora, chạy với `--control` và gửi lệnh.

## Sub-tasks
- [x] `CliporaRelease.Pick`, `Sha256Sums.Find`, `CliporaControl` (Core, có test)
- [x] `CliporaRuntime` (tìm, tải có tiến độ, băm khi tải, giải nén, dọn bản cũ)
- [x] `CliporaSession` (cổng trống, chờ cửa sổ, nhập file, tạo timeline từ clip)

## Acceptance criteria
- [ ] Test Core chọn bản, đọc SHA256SUMS, dựng và đọc lệnh
- [ ] CI tải Clipora đúng SHA-256 và nhận `file.import`

## Security
Tải file chạy được từ mạng: chỉ từ Release của HoangThai18/thaipro-craft qua HTTPS, kiểm SHA-256 với SHA256SUMS.txt cùng Release trước khi giải nén; cổng điều khiển chỉ nghe 127.0.0.1, cổng ngẫu nhiên mỗi lần chạy.

## Out of scope
- Viết lại trình dựng trong Recora.
