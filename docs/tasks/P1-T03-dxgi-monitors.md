---
id: P1-T03
title: Liệt kê màn hình qua DXGI và chọn đường bắt hình cho từng màn hình
phase: P1
lane: DESKTOP
status: todo
deps: []
touches: [src/Recora.App/Native/Dxgi.cs, src/Recora.App/Platform/MonitorService.cs]
size: M
---

## Goal
Biết mỗi màn hình thuộc GPU nào và là output thứ mấy của adapter mặc định, để màn hình trên GPU khác (HDMI của laptop gaming) dùng gfxcapture, và thứ tự bộ mã hoá theo đúng hãng GPU vẽ màn hình đó.

## Sub-tasks
- [ ] COM interop tối thiểu: `CreateDXGIFactory1`, `EnumAdapters1`, `GetDesc1`, `EnumOutputs`, `GetDesc` của output
- [ ] `MonitorService.List()` → `MonitorTarget` (tên, toạ độ desktop, output index nếu thuộc adapter 0, HMONITOR, hãng GPU, chính hay phụ), đánh số "Màn hình 1, 2" theo thứ tự trái sang phải
- [ ] Lỗi DXGI (máy ảo, RDP) → lùi về `Screen.AllScreens` với hãng Unknown, không làm app chết

## Acceptance criteria
- [ ] Build xanh; selftest trên CI ghi bằng màn hình lấy từ `MonitorService` và báo cáo ghi rõ adapter, hãng, output index

## Security
None.

## How to verify
CI: `selftest.json` có `monitors`.

## Out of scope
- Chọn vùng, cửa sổ (P2)
