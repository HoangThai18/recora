# Phases: Recora

| Phase | Mục tiêu | Trạng thái |
|---|---|---|
| P0 | Nền móng: solution, FFmpeg tự build, CI Windows ghi thử được | done |
| P1 | Ghi toàn màn hình kèm tiếng, tạm dừng, phím tắt, không mất video | done |
| P2 | Đủ các chế độ quay và công cụ đi kèm | done |
| P3 | Phát hành trên thaipro.store | doing |

---

## P0: Nền móng
**Mục tiêu:** mọi phase sau xây trên đây: code build và test được trên Mac, CI Windows build FFmpeg tối giản, build app, chạy ghi thử thật.

**Phạm vi:**
- Solution `Recora.Core` (netstandard2.0), `Recora.App` (net48 WinForms), `Recora.Core.Tests` (net10.0 xUnit)
- Script build FFmpeg 8.1.3 tối giản trên MSYS2 UCRT64, cache trên CI
- `Recora.Core`: dựng lệnh FFmpeg cho ghi màn hình, thứ tự bộ mã hoá, đọc tiến độ
- App: cửa sổ chính dạng khung, chế độ `--selftest` ghi 3 giây không cần giao diện
- CI: build, test, selftest, kiểm file bằng ffprobe, đóng gói zip

**Tiêu chí ra (kiểm được):**
- [x] `dotnet test` xanh trên Mac và trên CI
- [x] CI xanh: `ffmpeg.exe` dưới 20 MB, có `ddagrab`, `h264_nvenc`, `h264_amf`, `h264_qsv`, `h264_mf`, `libx264`
- [x] `Recora.exe --selftest` trên runner tạo MP4 có một luồng hình và một luồng tiếng, thời lượng 3 ± 0,5 giây (ffprobe)
- [x] Artifact zip portable dưới 20 MB, kèm ảnh chụp cửa sổ chính

Bằng chứng: [docs/reviews/P0.md](../reviews/P0.md), CI [41212947105](https://github.com/HoangThai18/recora/actions/runs/41212947105).

**Thiết kế cần có:** bố cục cửa sổ chính (thanh chế độ, menu trái, nút Ghi đỏ, thanh trạng thái), giao diện sáng xanh pastel của thaipro.store.

**Rủi ro:** build FFmpeg trên MSYS2; runner không có GPU và thiết bị âm thanh.

---

## P1: Ghi toàn màn hình
**Mục tiêu:** người chơi game bấm F12 là ghi màn hình đang chơi kèm tiếng game và micro, tạm dừng được, không bao giờ mất video.

**Phạm vi:**
- Liệt kê màn hình qua DXGI (adapter, output), chọn `ddagrab` hay `gfxcapture`
- Bắt tiếng hệ thống và micro (WASAPI), trộn hoặc hai track, âm lượng từng nguồn
- `Recorder`: phiên, đoạn, tạm dừng, dừng, hoàn tất, tự lùi bộ mã hoá, khôi phục khi mở app
- Dò bộ mã hoá lúc mở app, lưu kết quả
- Cài đặt: chất lượng, FPS, codec, bộ mã hoá, định dạng, thư mục lưu, con trỏ chuột
- Phím tắt toàn cục, khay hệ thống, thư viện file

**Tiêu chí ra:** (bằng chứng: CI [41216351844](https://github.com/HoangThai18/recora/actions/runs/41216351844), docs/reviews/P1.md)
- [x] Selftest trên CI: hai đoạn (có tạm dừng) ghép thành một MP4, thời lượng bằng tổng
- [x] Test Core cho mọi bộ mã hoá × codec × nguồn
- [x] Giết tiến trình giữa chừng rồi mở lại: file được hoàn tất (`--selftest-crash` rồi `--recover`)
- [x] Ảnh chụp cửa sổ chính ở các tab Video, Âm thanh, Phím tắt, Chung, Thư viện
- [x] Cửa sổ Recora không lọt vào video của chính nó (`--selftest-exclusion`)

---

## P2: Đủ chế độ quay
**Mục tiêu:** đủ các chế độ của một app quay màn hình: vùng, cửa sổ, game, thiết bị, chỉ âm thanh, cùng chụp ảnh, webcam nổi, hiệu ứng click, tự dừng. Hẹn giờ bắt đầu ghi và vùng đi theo con trỏ để bản sau.

**Tiêu chí ra:** (bằng chứng: CI [41218563510](https://github.com/HoangThai18/recora/actions/runs/41218563510), docs/reviews/P2.md)
- [x] Mỗi chế độ có test Core cho lệnh FFmpeg và chạy được trong selftest (trừ thiết bị: runner không có webcam)
- [x] Viền chọn vùng và cửa sổ Recora không lọt vào video (`WDA_EXCLUDEFROMCAPTURE`)
- [x] Chụp ảnh ra PNG đúng kích thước nguồn

**Rủi ro:** `gfxcapture` có thể không build được; quanh con trỏ tốn CPU hơn.

---

## P3: Phát hành
**Mục tiêu:** người dùng tải Recora từ thaipro.store/phan-mem/recora.

**Phạm vi:** bộ cài Inno Setup (cài theo người dùng, không cần quyền admin), zip portable, `SHA256SUMS.txt`, mã nguồn FFmpeg và x264 kèm Release, kiểm tra bản mới, mục Recora trên trang tải, ảnh giới thiệu.

**Tiêu chí ra:**
- [ ] CI dựng `recora-x.y.z-windows-x64-setup.exe` và `-portable.zip`, chạy thử cài và gỡ cài
- [ ] Release `recora-0.1.0` công khai; trang `/phan-mem/recora` hiện đúng phiên bản và nút tải
