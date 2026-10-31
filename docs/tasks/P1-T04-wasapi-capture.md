---
id: P1-T04
title: Bắt tiếng hệ thống và micro bằng WASAPI (NAudio), đưa về 48 kHz stereo
phase: P1
lane: DESKTOP
status: todo
deps: []
touches: [src/Recora.App/Audio/AudioDevices.cs, src/Recora.App/Audio/WasapiSource.cs, src/Recora.App/Recora.App.csproj, THIRD-PARTY-NOTICES.txt]
size: M
---

## Goal
Tiếng máy (loopback của loa hoặc tai nghe đang chọn) và micro chạy suốt phiên ghi, đổ vào `AudioRingBuffer` ở 48 kHz stereo float; rút thiết bị thì nguồn đó thành im lặng chứ không làm hỏng phiên (DOMAIN mục 6).

## Sub-tasks
- [ ] Thêm NuGet NAudio.Wasapi 2.2.1 (MIT): WASAPI qua COM tự viết rủi ro hơn
- [ ] `AudioDevices`: danh sách loa và micro đang hoạt động, thiết bị mặc định
- [ ] `WasapiSource`: loopback hoặc capture, chuyển byte → float, về stereo, resample bằng `WdlResampler` khi tần số khác 48 kHz
- [ ] Thiết bị bị rút → ghi nhật ký, báo một lần, tiếp tục bằng im lặng

## Acceptance criteria
- [ ] Build xanh; selftest không có thiết bị âm thanh vẫn chạy (runner CI) và ghi nhận "không có thiết bị"

## Security
Micro: chỉ mở khi người dùng bật Micro; dữ liệu chỉ đi vào pipe chỉ-người-dùng-hiện-tại (P0-T06).

## Out of scope
- Đồng hồ đo mức âm (P2)
