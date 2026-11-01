---
id: P4-T02
title: "Lõi giữ khoảnh khắc và cắt video: lệnh FFmpeg, đọc danh sách đoạn, chọn đoạn"
phase: P4
lane: BE
status: done
deps: []
touches: [src/Recora.Core/Ffmpeg/**, src/Recora.Core/Recording/**, tests/Recora.Core.Tests/Ffmpeg/**, tests/Recora.Core.Tests/Recording/**, tools/ffmpeg/components.sh, tools/ffmpeg/verify.sh, docs/reviews/P4-T02.md]
size: M
---

## Goal
Có lệnh FFmpeg ghi vòng các đoạn 2 giây và chọn đúng các đoạn phủ N giây gần nhất; có lệnh cắt video bằng copy luồng và lệnh lấy một khung hình xem trước.

## Sub-tasks
- [x] Thêm muxer `segment` vào `components.sh`
- [x] `ReplayCommand`: như `RecordingCommand` nhưng ra `-f segment -segment_format matroska -segment_time 2 -segment_wrap W -segment_list replay.csv -segment_list_size W -reset_timestamps 1`
- [x] `ReplayChunks.Parse` (CSV của muxer, bỏ dòng hỏng) và `Pick(entries, seconds)`
- [x] `TrimCommand` (`-ss`, `-to` trước `-i`, `-c copy`, `-avoid_negative_ts make_zero`) và `FramePreviewCommand` (một khung BGRA qua pipe)

## Acceptance criteria
- [x] Test lệnh: đủ tham số segment, số đoạn vòng W = ceil(N/2)+5
- [x] Test `Pick`: lấy từ đoạn mới nhất ngược về cho đủ N giây, ít đoạn hơn thì lấy hết, dòng CSV hỏng bị bỏ
- [x] Test `TrimCommand`: thời điểm định dạng bất biến văn hoá, H.265 trong MP4 có `hvc1`

## Security
None.

## Out of scope
- Chạy FFmpeg, giao diện (P4-T04, P4-T05).
