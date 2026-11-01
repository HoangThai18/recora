---
id: P4-T05
title: "App: hộp Cắt video trong Thư viện"
phase: P4
lane: DESKTOP
status: done
deps: [P4-T02]
touches: [src/Recora.App/UI/LibraryPage.cs, src/Recora.App/UI/TrimDialog.cs, src/Recora.App/Recording/**, src/Recora.App/SelfTest*.cs, src/Recora.App/UI/UiScreenshot.cs, src/Recora.App/Program.cs, src/Recora.App/MainController.cs, src/Recora.Core/Ffmpeg/TrimCommand.cs, tests/Recora.Core.Tests/Ffmpeg/**, tools/ci/trim-checks.ps1, tools/ci/ui-screenshots.ps1, .github/workflows/ci.yml]
size: S
---

## Goal
Chọn video trong Thư viện, bấm Cắt, kéo điểm đầu và cuối có ảnh xem trước, lưu thành file mới "<tên> (cắt).mp4".

## Sub-tasks
- [x] `TrimDialog`: hai thanh kéo, ô giờ, ảnh xem trước (debounce, huỷ yêu cầu cũ), ghi chú điểm đầu có thể lùi tới 2 giây
- [x] Lưu trên luồng nền, xong thì thêm vào Thư viện và chọn file mới
- [x] `--selftest-trim`

## Acceptance criteria
- [x] `--selftest-trim` trên CI: bản cắt có hình và tiếng, dài trong khoảng đã chọn (đầu lùi ≤ 2,1 s)
- [x] Ảnh chụp hộp Cắt video

## Security
None.

## Out of scope
- Cắt chính xác từng khung (phải mã hoá lại), ghép nhiều video.
