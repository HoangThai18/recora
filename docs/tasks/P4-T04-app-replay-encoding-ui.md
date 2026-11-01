---
id: P4-T04
title: "App: giữ khoảnh khắc (Alt+F10), ô trên Trang chủ, khay; mức Tiết kiệm và trần dung lượng ở trang Video"
phase: P4
lane: DESKTOP
status: todo
deps: [P4-T01, P4-T02]
touches: [src/Recora.App/Recording/**, src/Recora.App/MainController.cs, src/Recora.App/UI/**, src/Recora.App/Platform/HotkeyManager.cs, src/Recora.App/SelfTest*.cs, src/Recora.Core/Settings/AppSettings.cs]
size: M
---

## Goal
Bật "Lưu khoảnh khắc" là Recora giữ sẵn N giây gần nhất của màn hình đang chọn; Alt+F10 lưu thành video trong Thư viện; F12 vẫn ghi thường như cũ.

## Sub-tasks
- [ ] `ReplayBuffer` (App): dùng chung vòng thử bộ mã hoá với `Recorder`, thư mục `.recora\replay`, dọn khi tắt và khi mở app
- [ ] Lưu: chờ đoạn đang ghi xong (≤ 3 s), chép các đoạn cần sang thư mục tạm, ghép bằng `FinalizeCommand`
- [ ] F12 khi đang giữ: dừng giữ, ghi thường, ghi xong giữ lại; ổ đầy thì dừng giữ
- [ ] Ô trên Trang chủ, chọn độ dài 15 s/30 s/1/2/5 phút, phím lưu ở trang Phím tắt, trạng thái ở khay
- [ ] Trang Video: thêm "Tiết kiệm" (chọn luôn H.265 nếu có, 30 khung/giây) và "Giới hạn dung lượng"
- [ ] `--selftest-replay`

## Acceptance criteria
- [ ] `--selftest-replay` trên CI: clip 6 s dài 6–8,5 s, có hình và tiếng; thư mục đệm ≤ W đoạn; lưu lần hai được
- [ ] Ảnh chụp Trang chủ có ô lưu khoảnh khắc ở 100% và 150%

## Security
None.

## Out of scope
- Ghi thường song song với giữ khoảnh khắc (hai bộ mã hoá cùng lúc).
