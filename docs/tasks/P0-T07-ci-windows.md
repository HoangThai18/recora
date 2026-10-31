---
id: P0-T07
title: CI Windows - build app, test, selftest, kiểm bằng ffprobe, đóng gói zip
phase: P0
lane: INFRA
status: done
deps: [P0-T02, P0-T05, P0-T06]
touches: [.github/workflows/ci.yml, tools/ci/**]
size: M
---

## Goal
Mỗi lần push, CI trên `windows-latest` build app, chạy test, chạy selftest ghi thật với FFmpeg vừa build, kiểm file bằng ffprobe, chụp giao diện, đóng gói zip portable.

## Context to read
- AGENTS.md (lệnh), P0-T02 (job ffmpeg)

## Sub-tasks
- [x] Job `app`: setup .NET 10, build Release, test
- [x] Selftest với `ffmpeg.exe` từ job `ffmpeg`, `tools/ci/check-recording.ps1` dùng ffprobe
- [x] `--ui-screenshot` ở 100% và 150% DPI, upload ảnh
- [x] Đóng gói `recora-<version>-windows-x64-portable.zip`, upload artifact

## Acceptance criteria
- [x] CI xanh trên `phase/P0`; artifact zip dưới 20 MB; ảnh cửa sổ có trong artifact
- [x] ffprobe: 1 luồng hình, 1 luồng tiếng dài bằng nhau (lệch ≤ 0,15 giây), cả file trong 2,5–6 giây, tiếng thử có trong file (RMS > 0,05)
- [x] Đồng bộ đo trực tiếp: màn hình đổi trắng và tiếng bíp bắt đầu cùng lúc; trong file, khung trắng đầu tiên trừ mẫu tiếng đầu tiên nằm trong -100…+150 ms

## Security
None.

## How to verify
Xem lượt chạy CI trên nhánh `phase/P0`.

## Out of scope
- Bộ cài, Release (P3)

## Notes / decisions
- 2026-10-31, lượt CI [41210439903](https://github.com/HoangThai18/recora/actions/runs/41210439903): đuôi tiếng hụt tới 0,53 giây (dao động theo tốc độ runner), nên sửa tận gốc thay vì nới ngưỡng: `AudioPump.Drain` chờ FFmpeg đọc hết pipe rồi mới `q`, và hoàn tất cắt mỗi đoạn ở độ dài tiếng (`outpoint` của concat). Bước kiểm quay lại đòi hai luồng dài bằng nhau.
- 2026-10-31, lượt CI [41210004445](https://github.com/HoangThai18/recora/actions/runs/41210004445) xanh (đồng bộ: hình trễ tiếng 45 ms; zip 5,3 MB). Ảnh 150% cho thấy form dựng bằng code không tự phóng theo DPI (thiếu `SuspendLayout`/`ResumeLayout` quanh `AutoScaleDimensions`): chữ to mà khung không to. Sửa trong `MainForm.cs`, nới ô chế độ, tăng chiều cao cửa sổ để thẻ cuối không bị cắt (thêm `src/Recora.App/UI/**` vào phạm vi).
- 2026-10-31, lượt CI [41209633975](https://github.com/HoangThai18/recora/actions/runs/41209633975): sau khi sửa dò input, đầu file còn lệch ~0,05 giây; phần lệch 0,26 giây nằm ở **cuối**: bộ lập lịch FFmpeg 7+ không cho luồng tiếng chạy trước luồng hình, x264 trên CPU yếu (tốc độ 0,92x) trễ ~0,26 giây, nên chừng ấy tiếng còn trong pipe lúc `q` bị bỏ. Độ dài không phân biệt được lệch đầu (lỗi đồng bộ) với hụt đuôi (vô hại), nên thêm phép đo đồng bộ trực tiếp bằng mốc chớp trắng + tiếng bíp; độ dài chỉ còn kiểm tiếng không dài hơn hình và hụt không quá 0,5 giây.
- 2026-10-31, lượt CI [41209388443](https://github.com/HoangThai18/recora/actions/runs/41209388443): bước kiểm mới bắt lỗi đồng bộ thật: hình 4,19 giây, tiếng 3,52 giây. `-probesize 100M` của gdigrab bắt FFmpeg dò nhiều khung trước khi mở pipe tiếng. Sửa trong `RecordingCommand.cs` (thêm vào phạm vi) và thêm test hồi quy `VideoInput_ProbesOneFrameOnly_*`.
- 2026-10-31, lượt CI [41209176637](https://github.com/HoangThai18/recora/actions/runs/41209176637): selftest OK nhưng file dài 4,25 giây dù dừng khi `out_time` = 2,75 giây. `out_time` là thời gian đã mux, trễ hơn thời điểm bắt hình bởi độ trễ của bộ mã hoá (lookahead, B-frame, luồng của x264), nên "3 ± 0,5 giây" không đo tính đúng. Đổi sang so độ dài luồng hình với luồng tiếng (thứ quyết định đồng bộ) và kiểm tiếng thật có trong file.
- 2026-10-31, lượt CI [41208875155](https://github.com/HoangThai18/recora/actions/runs/41208875155): ghi được thật (gdigrab + x264 + tiếng qua pipe) nhưng lộ 2 lỗi của P0-T04/T06, sửa luôn trong task này (thêm vào phạm vi: `ProbeCommand.cs`, `ProbeCommandTests.cs`, `AudioPump.cs`, `SelfTest.cs`): FFmpeg 8.1 in 2 cột cờ cho `-filters` (test cũ dựng theo giả định 3 cột); FFmpeg treo sau `q` vì app ngừng bơm tiếng trước, luồng đọc pipe kẹt chờ dữ liệu.

Review: [docs/reviews/P0-T07.md](../reviews/P0-T07.md) · PASS · merge vào phase/P0
