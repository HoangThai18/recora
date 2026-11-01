---
id: P5-T04
title: "CI selftest trình dựng, phát hành 0.3.0, trang tải"
phase: P5
lane: QA
status: done
deps: [P5-T01, P5-T03]
touches: [tools/ci/**, .github/workflows/ci.yml, src/Recora.App/SelfTest*.cs, src/Recora.App/Program.cs, Directory.Build.props, docs/**, "thaidev: config/software.php", "thaidev: public/assets/phan-mem/recora*"]
size: M
---

## Goal
CI chứng minh trình dựng chạy trong Recora trên Windows; 0.3.0 lên trang tải.

## Sub-tasks
- [x] `--selftest-editor`, `editor-checks.ps1`
- [x] Lên 0.3.0, ghi chú phát hành, tag
- [x] Trang tải: tính năng Dựng phim, ảnh

## Acceptance criteria
- [x] CI xanh có ảnh `editor-embedded.png`
- [x] Release `recora-0.3.0` đủ file; curl trang hiện 0.3.0

## Security
Tải file chạy được từ mạng: chỉ từ Release của HoangThai18/thaipro-craft qua HTTPS, kiểm SHA-256 với SHA256SUMS.txt cùng Release trước khi giải nén; cổng điều khiển chỉ nghe 127.0.0.1, cổng ngẫu nhiên mỗi lần chạy.

## Out of scope
- Viết lại trình dựng trong Recora.
