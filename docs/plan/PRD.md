# PRD: Recora

_Phiên bản v0.1 · 2026-10-31 · Trạng thái: đã duyệt (các quyết định chính chốt với chủ sản phẩm ngày 2026-10-31)_

## 1. Vấn đề
Người chơi game và người làm hướng dẫn trên Windows cần quay màn hình kiểu Bandicam: một phím là ghi, file nhỏ mà nét, không làm game giật. Bandicam bản miễn phí gắn logo và giới hạn 10 phút; OBS mạnh nhưng rối với người chỉ muốn bấm F12; các app miễn phí khác hay ghi bằng CPU nên tụt FPS và file to. Recora là app miễn phí, nhẹ, ghi bằng GPU, đủ các chế độ quay quen thuộc, giao diện tiếng Việt.

## 2. Người dùng

| Vai trò | Là ai | Việc chính |
|---|---|---|
| Người chơi game | Quay trận đấu, highlight | Bấm F12 trong game, file nhỏ để đăng TikTok, YouTube |
| Người làm hướng dẫn, giáo viên | Quay thao tác phần mềm, bài giảng | Chọn vùng hoặc cửa sổ, ghi kèm micro và webcam |
| Người dùng phổ thông | Quay cuộc gọi, video trên mạng | Toàn màn hình, tiếng máy, không cần chỉnh gì |

## 3. Mục tiêu bản này
- Ghi 1080p60 bằng GPU mà CPU của Recora cộng FFmpeg dưới 5% trên máy 4 nhân, RAM dưới 150 MB.
- Mức "Cân bằng": 10 phút chơi game 1080p60 bằng H.264 dưới 1 GB, bằng H.265 dưới 700 MB, không vỡ hình khi cảnh chuyển động nhanh.
- Từ lúc mở app tới lúc đang ghi: một cú bấm (F12).
- File cài và app dưới 20 MB.

## 4. Luồng chính
1. Là **người chơi game**, tôi bấm **F12 khi đang chơi** để ghi màn hình đang chơi kèm tiếng game và micro.
   - Tiêu chí: ghi đúng màn hình có game; tiếng game và micro khớp hình (lệch dưới 50 ms); bấm F12 lần nữa thì có file MP4 trong thư mục.
2. Là **người dùng**, tôi **tạm dừng và tiếp tục** khi đang ghi.
   - Tiêu chí: file cuối là một video liền, không có đoạn đã tạm dừng, thời lượng bằng tổng thời gian ghi.
3. Là **người làm hướng dẫn**, tôi **kéo chọn một vùng** hoặc **chọn một cửa sổ** để ghi.
   - Tiêu chí: video đúng kích thước vùng (làm chẵn); viền chọn vùng của Recora không lọt vào video.
4. Là **người dùng**, tôi **chụp ảnh** bằng F11.
   - Tiêu chí: file PNG hoặc JPG đúng nguồn của chế độ đang chọn, tuỳ chọn có trong clipboard.
5. Là **người dùng**, tôi **xem lại các file đã ghi** trong app.
   - Tiêu chí: danh sách có tên, thời lượng, dung lượng; mở bằng trình phát mặc định; xoá vào Thùng rác.
6. Là **người dùng**, tôi **không mất video** khi máy tắt đột ngột.
   - Tiêu chí: mở lại app thì phần đã ghi được hoàn tất thành file (BR-01).

## 5. Phạm vi
**Trong phạm vi:** toàn màn hình (chọn màn hình), vùng, cửa sổ, game, thiết bị (webcam, card capture), chỉ âm thanh, quanh con trỏ; tiếng hệ thống và micro (trộn chung hoặc hai track); H.264 / H.265 / AV1 bằng NVENC, AMF, QSV, Media Foundation, x264; MP4 / MKV; tạm dừng; phím tắt toàn cục; khay hệ thống; chụp ảnh; webcam nổi; hiệu ứng click chuột; tự dừng theo thời gian hoặc dung lượng; hẹn giờ ghi; thư viện file; khôi phục khi tắt đột ngột; kiểm tra bản mới.

**Ngoài phạm vi bản này:** hook DirectX/OpenGL/Vulkan và hiện FPS trong game; livestream; chỉnh sửa video; vẽ lên màn hình khi đang ghi; HDR; bản Mac, Linux.

## 6. Yêu cầu phi chức năng
- Nhiều khách hàng dùng chung hệ thống: không. Không có máy chủ.
- Dữ liệu cá nhân: video và ảnh chỉ nằm trên máy người dùng. App không gửi gì đi (BR-09), chỉ hỏi GitHub về bản mới.
- Hiệu năng: như mục 3. Khung hình ở nguyên trên GPU với NVENC, AMF, QSV.
- Nền tảng: Windows 10 1903+ và Windows 11, 64-bit. Dùng .NET Framework 4.8 có sẵn trong Windows, không bắt cài thêm runtime.
- Ngôn ngữ: giao diện tiếng Việt.
- Offline: chạy hoàn toàn offline.

## 7. Tích hợp

| Hệ thống | Mục đích | Bắt buộc bản này? |
|---|---|---|
| GitHub Releases của `HoangThai18/recora` | Phát hành, kiểm tra bản mới | có |
| thaipro.store/phan-mem | Trang giới thiệu và tải | có |

## 8. Giả định và câu hỏi mở
**Giả định:**
- Máy người dùng có GPU tích hợp hoặc rời ra đời sau 2013; nếu không, x264 hoặc Media Foundation vẫn ghi được nhưng tốn CPU hơn.
- Game chạy ở chế độ Borderless hoặc fullscreen thông thường của Windows 10/11.

**Câu hỏi mở:**
- Chưa có chữ ký số Windows: lần đầu mở SmartScreen sẽ cảnh báo (giống các app khác trên thaipro.store).
