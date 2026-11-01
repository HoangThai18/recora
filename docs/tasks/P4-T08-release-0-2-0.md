---
id: P4-T08
title: "Phát hành recora-0.2.0 và cập nhật trang tải"
phase: P4
lane: QA
status: todo
deps: [P4-T07]
touches: [Directory.Build.props, docs/release-notes/**, docs/**, "thaidev: config/software.php", "thaidev: public/assets/phan-mem/recora*", "thaidev: tests/Feature/SoftwarePageTest.php"]
size: S
---

## Goal
Người dùng tải được 0.2.0 có các tính năng mới; trang thaipro.store/phan-mem/recora mô tả đúng.

## Sub-tasks
- [ ] Lên 0.2.0, ghi chú phát hành
- [ ] Tag `recora-0.2.0`
- [ ] thaidev: thêm tính năng, câu hỏi, ảnh mới; test trang; push main

## Acceptance criteria
- [ ] Release `recora-0.2.0` đủ 4 file
- [ ] curl trang hiện 0.2.0 và tính năng mới

## Security
None.

## Out of scope
- Ký số bộ cài.
