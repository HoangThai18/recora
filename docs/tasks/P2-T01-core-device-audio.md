---
id: P2-T01
title: Core - nguồn webcam/thiết bị (dshow), ghi chỉ âm thanh (M4A, MP3, WAV), ghép file âm thanh
phase: P2
lane: DESKTOP
status: done
deps: []
touches: [src/Recora.Core/Capture/**, src/Recora.Core/Ffmpeg/**, tests/Recora.Core.Tests/**]
size: M
---

## Goal
Lõi dựng được lệnh FFmpeg cho hai chế độ chưa có hình màn hình: ghi webcam hoặc thẻ capture (DirectShow) và chỉ ghi âm thanh, với cùng cách chia đoạn và ghép bằng stream copy như video.

## Context to read
- src/Recora.Core/Ffmpeg/RecordingCommand.cs, FinalizeCommand.cs; docs/domain/DOMAIN.md BR-01, BR-07

## Sub-tasks
- [x] `DeviceSource` + `CaptureBackend.Dshow`; chuỗi lọc CPU cho khung từ thiết bị (làm chẵn, thu nhỏ theo trần chiều cao, `nv12`/`yuv420p`), không gắn nhãn màu sai
- [x] `DshowDevices`: lệnh liệt kê và đọc tên webcam từ đầu ra của FFmpeg
- [x] `AudioRecordingCommand` (AAC/M4A, MP3 qua `mp3_mf`, WAV `pcm_s16le`) vào đoạn Matroska; `FinalizeCommand.BuildAudio` ra `.m4a` (muxer `ipod`), `.mp3`, `.wav`

## Acceptance criteria
- [x] Test: lệnh dshow có `-f dshow -i video=<tên>` và không có `hwdownload`/`scale_d3d11`; bộ chọn backend trả Dshow cho `DeviceSource`
- [x] Test: đọc đúng danh sách webcam từ mẫu đầu ra FFmpeg 8 (bỏ thiết bị audio)
- [x] Test: lệnh âm thanh đúng codec theo định dạng, có `-flush_packets 1`; ghép âm thanh đúng muxer và đuôi file

## Security
None.

## Out of scope
- Giao diện chọn chế độ (P2-T03)
