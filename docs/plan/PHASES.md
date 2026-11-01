# Phases: Recora

| Phase | Mục tiêu | Trạng thái |
|---|---|---|
| P0 | Nền móng: solution, FFmpeg tự build, CI Windows ghi thử được | done |
| P1 | Ghi toàn màn hình kèm tiếng, tạm dừng, phím tắt, không mất video | done |
| P2 | Đủ các chế độ quay và công cụ đi kèm | done |
| P3 | Phát hành trên thaipro.store | done |
| P4 | Cho người chơi game: lưu khoảnh khắc, cắt video, file nhẹ, thử và chỉnh micro | done |
| P5 | Chuyển động mượt và Dựng phim (Clipora) ngay trong cửa sổ Recora | doing |

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
- [x] CI dựng `recora-x.y.z-windows-x64-setup.exe` và `-portable.zip`, chạy thử cài và gỡ cài
- [x] Release `recora-0.1.0` công khai; trang `/phan-mem/recora` hiện đúng phiên bản và nút tải

---

## P4: Cho người chơi game
**Mục tiêu:** người chơi Liên Minh, Delta Force giữ lại pha hay mà không phải ghi cả trận, cắt bớt đầu đuôi, file nhẹ hơn, và nghe thử micro, loa trước khi ghi.

**Phạm vi:**
- Lưu khoảnh khắc: giữ sẵn 15 giây đến 5 phút gần nhất trên ổ đĩa, bấm Alt+F10 là lưu thành video. Đang giữ mà bấm F12 thì chuyển sang ghi thường, ghi xong tự giữ lại.
- Cắt video trong Thư viện: chọn đầu, cuối, xem trước khung hình, lưu bản cắt mới bằng copy luồng (không giảm chất lượng).
- Mức chất lượng "Tiết kiệm" và giới hạn dung lượng mỗi giờ (1, 2, 4, 8 GB) áp cho mọi bộ mã hoá.
- Âm thanh: thu thử micro rồi phát lại, phát tiếng thử trái/phải ra loa, chỉnh micro (tăng âm, lọc tiếng ù, chặn ồn nền, nén tiếng, chống rè).

**Tiêu chí ra:**
- [x] Selftest giữ khoảnh khắc trên CI: lưu được clip đúng độ dài (±2,5 s), thư mục đệm không phình quá số đoạn đã định, lưu được lần hai khi vẫn đang giữ
- [x] Selftest cắt video: bản cắt có đủ hình và tiếng, độ dài đúng khoảng đã chọn (đầu có thể lùi tới một keyframe)
- [x] Mọi bộ tham số GPU (NVENC, AMF, QSV, MF) có và không có giới hạn dung lượng đều được FFmpeg 8.1.3 nhận (CI chạy thử, lỗi chỉ được là thiếu GPU, không phải sai tham số)
- [x] Test Core cho bộ xử lý micro: lọc tiếng ù, chặn ồn, nén, chống rè có số đo
- [x] Ảnh chụp trang Trang chủ (có ô lưu khoảnh khắc), Âm thanh (thử và chỉnh micro), hộp cắt video
- [x] Release `recora-0.2.0`, trang tải cập nhật tính năng mới

**Rủi ro:** muxer `segment` phải thêm vào bản dựng FFmpeg; đọc danh sách đoạn trong lúc FFmpeg đang ghi; xử lý âm thanh thêm độ trễ hoặc làm méo tiếng.

---

## P5: Chuyển động và Dựng phim trong một cửa sổ
**Mục tiêu:** giao diện có chuyển động mượt; quay xong dựng luôn trong cùng cửa sổ Recora bằng Clipora.

**Phạm vi:**
- Animation: hover, công tắc, ô chọn trượt, đổi trang mờ dần, nút ghi thở, thanh mức âm mượt, chấm trạng thái đổi màu; tôn trọng cài đặt "Show animations" của Windows; không chạy khi cửa sổ ẩn.
- Trang "Dựng phim": tìm Clipora đã cài hoặc tải bản portable (kiểm SHA256), nhúng cửa sổ Clipora vào trang, điều khiển qua cổng `--control` (nhập file, tạo timeline từ clip), tắt theo Recora.
- Nút "Dựng" ở Thư viện và sau khi lưu video, lưu khoảnh khắc.

**Tiêu chí ra:**
- [ ] Video demo chuyển động quay trên CI (`ui-demo-1_5.mp4`)
- [ ] Test Core cho tween, chọn bản Clipora, đọc SHA256SUMS
- [ ] CI: Clipora tải về đúng SHA256, chạy với `--control`, nhận `file.import`; ảnh trang Dựng phim có Clipora bên trong (nếu runner không có GPU thì ghi rõ)
- [ ] Release `recora-0.3.0`, trang tải cập nhật

**Rủi ro:** nhúng cửa sổ khác tiến trình (bàn phím, DPI, GPU swapchain); runner CI không có GPU cho Clipora (wgpu).

