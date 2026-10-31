---
id: P1-T06
title: App chạy thật - trang cài đặt, phím tắt toàn cục, khay hệ thống, thư viện, một phiên bản
phase: P1
lane: DESKTOP
status: done
deps: [P1-T05]
touches: [src/Recora.App/UI/**, src/Recora.App/Platform/HotkeyManager.cs, src/Recora.App/Platform/SingleInstance.cs, src/Recora.App/Platform/StartupRegistration.cs, src/Recora.App/MainController.cs, src/Recora.App/AppPaths.cs, src/Recora.App/Program.cs, src/Recora.App/SelfTest*.cs, tools/ci/ui-screenshots.ps1]
size: L
---

## Goal
Mở Recora là dùng được: chọn màn hình, bấm nút đỏ hoặc F12 để ghi, Shift+F12 tạm dừng, đóng cửa sổ thì app lui xuống khay mà vẫn ghi. Mọi lựa chọn ở các trang Video, Âm thanh, Phím tắt, Chung được lưu và áp cho lần ghi sau. Trang Thư viện liệt kê những gì đã ghi.

## Context to read
- docs/plan/PRD.md mục giao diện, docs/domain/DOMAIN.md BR-04, BR-05, BR-06
- src/Recora.App/UI/MainForm.cs, Pages.cs (khung UI của P0)

## Sub-tasks
- [x] Control: nhóm nút chọn một (segmented), ô bắt phím tắt, thẻ tự cao theo nội dung
- [x] Trang Video: chất lượng, FPS, độ phân giải, codec, bộ mã hoá (ghi rõ cái máy không có), định dạng, con trỏ chuột
- [x] Trang Âm thanh: tiếng máy + thiết bị + âm lượng, micro + thiết bị + âm lượng, tách hai track
- [x] Trang Phím tắt (ô đỏ khi app khác giữ phím), trang Chung (thư mục lưu, thu xuống khay, mở cùng Windows, ẩn Recora khỏi video), trang Giới thiệu (phiên bản, giấy phép, nhật ký)
- [x] Trang chủ: chọn màn hình, bật tắt tiếng máy và micro, bộ mã hoá sẽ dùng
- [x] `MainController`: nối UI với `AppSettings` (lưu sau mỗi thay đổi), `Recorder`, thanh trạng thái (giờ ghi, dung lượng, fps), kiểm ổ đĩa mỗi 5 giây khi ghi
- [x] `HotkeyManager` (RegisterHotKey + MOD_NOREPEAT trên cửa sổ message-only), `SingleInstance` (mutex + thông điệp gọi cửa sổ cũ lên), `StartupRegistration` (khoá Run của người dùng)
- [x] Khay hệ thống: chấm đỏ khi ghi, vàng khi tạm dừng; menu Mở, Ghi/Dừng, Tạm dừng, Mở thư mục, Thoát
- [x] `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` theo cài đặt; đóng khi đang ghi thì hỏi, Windows tắt máy thì để dành cho khôi phục
- [x] Trang Thư viện: danh sách mới nhất trước (dữ liệu từ `LibraryIndex` của P1-T05), Mở, Xem trong thư mục, Xoá vào Thùng rác
- [x] `--settings-roundtrip` cho CI; `--ui-screenshot` dựng đủ các trang thật

## Acceptance criteria
- [x] CI: `--settings-roundtrip` giữ mọi trường dưới .NET Framework
- [x] CI chụp trang Trang chủ, Video, Âm thanh, Phím tắt, Chung, Thư viện (có file của selftest), Giới thiệu ở 100% và 150%
- [x] Build xanh, không cảnh báo

## Security
- Xoá file: chỉ file nằm trong danh sách quét từ thư mục lưu, vào Thùng rác, có hỏi trước
- Khoá Run chỉ ghi đường dẫn exe của chính Recora, trong HKCU (không cần quyền admin)

## How to verify
```bash
dotnet test tests/Recora.Core.Tests
# CI: tools/ci/ui-screenshots.ps1, --settings-roundtrip
```

## Out of scope
- Các chế độ ngoài toàn màn hình, chụp ảnh, đếm ngược (P2)
