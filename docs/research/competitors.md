# Research: tính năng chuẩn của phần mềm quay màn hình Windows

_Ngày: 2026-10-31 · Câu hỏi: người dùng Bandicam mong có những gì, phần nào là "phải có" cho bản đầu?_

## Kết luận
Bandicam đặt chuẩn cho người chơi game Việt: một phím F12 để ghi, file nhỏ mà nét nhờ mã hoá bằng GPU, nhiều chế độ chọn nguồn (toàn màn hình, vùng, cửa sổ, game, thiết bị, chỉ âm thanh). Recora phải có đủ các chế độ đó, ghi bằng GPU mặc định, và miễn phí không gắn logo, không giới hạn thời gian (hai điểm người dùng ghét nhất ở bản Bandicam miễn phí).

## Bảng tính năng

| Tính năng | Bandicam | OBS Studio | ShareX | Recora v1 |
|---|---|---|---|---|
| Toàn màn hình, chọn màn hình | có | có | có | P1 |
| Chọn vùng (kéo chuột, kích thước có sẵn) | có | có (crop) | có | P2 |
| Cửa sổ | có | có | có | P2 |
| Chế độ game (ghi game đang chạy, F12) | có (hook DirectX) | có (game capture) | không | P2, không hook: quay màn hình hoặc cửa sổ của game |
| Quanh con trỏ chuột | có | không | không | P2 (có thể dời) |
| Thiết bị: webcam, card capture | có | có | không | P2 |
| Chỉ ghi âm thanh | có | không | không | P2 |
| Tiếng hệ thống + micro, trộn hoặc tách track | có | có | có | P1 |
| Mã hoá GPU NVENC / AMF / QSV | có | có | có (qua FFmpeg) | P1 |
| H.264 / H.265 / AV1 | H.264, H.265 | cả ba | cả ba | P1 |
| Tạm dừng / tiếp tục | có | có | không | P1 |
| Phím tắt toàn cục | có | có | có | P1 |
| Chụp ảnh màn hình | có | có | có | P2 |
| Webcam nổi trên video (PIP) | có | có | không | P2 |
| Hiệu ứng click chuột | có | plugin | có | P2 |
| Tự dừng theo thời gian / dung lượng, hẹn giờ | có | plugin | không | P2 |
| Vẽ lên màn hình khi đang ghi | có | không | không | ngoài phạm vi v1 |
| Logo chìm, giới hạn 10 phút ở bản miễn phí | có | không | không | **không bao giờ** |

## Nguồn đã đọc
- Trang tính năng Bandicam (bandicam.com/features) và hướng dẫn chế độ game: F12 ghi, Shift+F12 tạm dừng, F11 chụp ảnh là phím mặc định quen tay.
- Tài liệu OBS Studio về Display Capture trên laptop hai GPU (màn đen khi OBS chạy trên GPU rời).
- Mã nguồn ShareX: dùng FFmpeg (gdigrab/ddagrab) làm lõi ghi hình, cách chọn vùng bằng lớp phủ toàn màn hình.

## Ghi chú
- Phím mặc định giữ theo Bandicam (F12, Shift+F12, F11) để người chuyển sang không phải học lại.
- Người dùng than phiền nhiều nhất về: logo chìm, giới hạn 10 phút, file to, giật game khi ghi bằng CPU. Ba điều đầu là quyết định sản phẩm; điều cuối là lý do mặc định dùng GPU.
