---
id: P4-T07
title: "CI: kiểm tham số bộ mã hoá GPU, chạy các selftest mới, ảnh chụp"
phase: P4
lane: QA
status: review
deps: [P4-T04, P4-T05, P4-T06]
touches: [tools/ci/**, .github/workflows/ci.yml, src/Recora.App/SelfTest*.cs, src/Recora.App/Program.cs, src/Recora.Core/Ffmpeg/TrimCommand.cs, tests/Recora.Core.Tests/Ffmpeg/**, src/Recora.App/MainController.cs, src/Recora.App/UI/UiScreenshot.cs]
size: S
---

## Goal
CI chứng minh mọi bộ tham số GPU được FFmpeg nhận và các tính năng mới chạy trên Windows.

## Sub-tasks
- [ ] `encoder-options-check.ps1`: chạy từng bộ tham số với `testsrc2`; chỉ chấp nhận lỗi thiếu thiết bị/driver, không chấp nhận "Option not found", "Unable to parse", "Error setting option"
- [ ] Gọi `--selftest-replay`, `--selftest-trim` trong `modes-checks.ps1`
- [ ] Thêm ảnh chụp mới vào `ui-screenshots.ps1`

## Acceptance criteria
- [ ] Lượt CI phase/P4 xanh, log có dòng kết quả của từng kiểm

## Security
None.

## Out of scope
- Máy có GPU thật.
