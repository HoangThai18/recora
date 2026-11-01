# Research: bắt hình, mã hoá và đồng bộ tiếng trên Windows

_Ngày: 2026-10-31 · Câu hỏi: ghi thế nào để không tụt FPS game, file nhỏ mà nét, chạy trên GPU nào cũng được?_

## Kết luận
Dùng FFmpeg 8.1.3 tự build tối giản (MSYS2 UCRT64, liên kết tĩnh). Hình bắt bằng `ddagrab` (Desktop Duplication) cho màn hình và vùng, `gfxcapture` (Windows.Graphics.Capture) cho cửa sổ và cho màn hình nằm trên GPU khác. Khung hình ở nguyên trên GPU (texture D3D11) tới thẳng bộ mã hoá phần cứng: không chép về RAM, CPU gần như không làm gì, nên game không tụt FPS. Tiếng do app bắt bằng WASAPI (NAudio), trộn theo đồng hồ của app rồi đẩy vào FFmpeg qua named pipe.

## Bối cảnh
- FFmpeg 8.1.3 (tarball ffmpeg.org). Đọc thẳng mã nguồn nhánh `release/8.1` cho từng điều dưới đây.
- Windows 10 1903+ / Windows 11, 64-bit.

## Đường đi của khung hình theo bộ mã hoá

Nguồn `ddagrab` / `gfxcapture` cho ra khung D3D11 định dạng BGRA.

| Bộ mã hoá | Nhận khung D3D11 BGRA? | Chuỗi lọc cần thêm | Nguồn |
|---|---|---|---|
| NVENC (`h264_nvenc`, `hevc_nvenc`, `av1_nvenc`) | có (`ff_nvenc_pix_fmts` gồm `D3D11`, `RGB32`/`BGR32`) | không | `libavcodec/nvenc.c` |
| AMF (`h264_amf`, `hevc_amf`, `av1_amf`) | có (`D3D11`, `BGRA`) | không | `libavcodec/amfenc.c` |
| AMF, đường dự phòng | không cần | `hwdownload,format=bgra,scale=...,format=nv12`, dùng khi AMF từ chối khung D3D11 (`SubmitInput() failed with error 18`, thấy trên AMD Radeon tích hợp, Windows 10 19045) | nhật ký máy thật 01/11/2026 |
| QSV (`h264_qsv`, `hevc_qsv`, `av1_qsv`) | không, chỉ `NV12`/`QSV` | `scale_d3d11=format=nv12,hwmap=derive_device=qsv,format=qsv` | `qsvenc_h264.c`, `qsvenc_av1.c` |
| Media Foundation (`h264_mf`, `hevc_mf`) | chỉ D3D11 với NV12 | `hwdownload,format=bgra,format=nv12` (an toàn hơn đường D3D11 với MFT phần mềm) | `libavcodec/mfenc.c` |
| x264 (CPU) | không | `hwdownload,format=bgra,format=yuv420p` | |

Thu nhỏ độ phân giải trên GPU: `scale_d3d11=width=W:height=H` (tuỳ chọn `width`, `height`, `format`).

## Thang chất lượng

| Bộ mã hoá | Tham số | Dải |
|---|---|---|
| NVENC H.264/H.265 | `-rc vbr -cq N -b:v 0` | 0–51 |
| NVENC AV1 | `-rc vbr -cq N -b:v 0` | **0–63** |
| AMF H.264/H.265 | `-rc cqp -qp_i N -qp_p N` | 0–51 |
| AMF AV1 | `-rc qvbr -qvbr_quality_level N` | 0–51 (qp của AV1 là 0–255 nên không dùng cqp) |
| QSV | `-global_quality N` (ICQ) | 1–51 |
| MF | `-rate_control quality -quality N` | 0–100 |
| x264 | `-crf N` | 0–51 |

## Đồng bộ hình và tiếng
- `ddagrab` đặt pts = thời gian kể từ khung đầu tiên của chính nó (`vsrc_ddagrab.c`, `now - first_pts`). Tiếng raw PCM qua pipe đánh pts theo số mẫu, bắt đầu từ 0 khi pipe có dữ liệu.
- Nếu bắt hình bằng `-filter_complex`, FFmpeg mở mọi input (cả pipe tiếng) **trước** khi dựng filtergraph, nên tiếng bắt đầu sớm hơn hình 100–500 ms: tiếng trễ so với hình.
- Dùng `-f lavfi -i ddagrab=...` làm **input số 0** và pipe tiếng làm input số 1: FFmpeg mở input lần lượt, `ddagrab` lấy khung đầu khi dò input 0, rồi mới mở pipe. Lệch chỉ còn vài chục ms. Thiết bị `lavfi` chuyển khung GPU qua codec `wrapped_avframe` (`libavdevice/lavfi.c`), nên bản build phải có cả decoder `wrapped_avframe`.
- Loopback WASAPI im lặng thì không trả gói nào: app phải tự chèn im lặng theo đồng hồ của mình, nếu không tiếng sẽ ngắn hơn hình.

## Laptop hai GPU
- `ddagrab` tạo thiết bị D3D11 trên adapter mặc định và đánh số output theo adapter đó. Màn hình gắn vào GPU khác (cổng HDMI của laptop gaming thường nối GPU rời) không bắt được bằng `ddagrab` qua thiết bị `lavfi`.
- `gfxcapture` (WGC) bắt được qua adapter. App liệt kê adapter/output bằng DXGI: output thuộc adapter 0 thì dùng `ddagrab`, còn lại dùng `gfxcapture` theo `hmonitor`.
- NVENC chỉ nhận texture nằm trên GPU NVIDIA. Màn hình do GPU Intel vẽ thì NVENC hỏng ngay lúc mở, nên app phải tự thử bộ mã hoá kế tiếp.

## `gfxcapture`
- Tuỳ chọn: `hwnd`, `hmonitor`, `monitor_idx`, `window_title` (regex), `capture_cursor`, `display_border`, `max_framerate`, `width`/`height` (số âm: làm tròn xuống bội số), `crop_*`, `resize_mode`.
- Khung chỉ đến khi nội dung đổi (VFR): đầu ra cần `-fps_mode cfr -r N`.
- Windows 10 luôn vẽ viền vàng quanh vùng bị bắt; `display_border=0` chỉ có tác dụng từ Windows 11.
- Phụ thuộc lúc build: `cxx17`, `IGraphicsCaptureItemInterop`, `IGraphicsCaptureSession3` trong header của mingw-w64. Không có thì configure tự tắt; app hỏi `ffmpeg -filters` lúc chạy để biết.

## Thư viện tĩnh trên MSYS2 UCRT64 (đã tra packages.msys2.org)
`mingw-w64-ucrt-x86_64-libx264` (`libx264.a`), `-libvpl` (`libvpl.a`), `-ffnvcodec-headers`, `-amf-headers`. MP3 dùng `mp3_mf` của Windows, không cần LAME.

## Các phương án đã cân nhắc

| Phương án | Ưu | Nhược | Giấy phép |
|---|---|---|---|
| FFmpeg tự build tối giản | ~12 MB, đủ GPU, đã kiểm chứng | phải build trên CI, giữ mã nguồn kèm Release | GPL (vì x264) |
| FFmpeg bản dựng sẵn của BtbN | không phải build | ~200 MB, trái yêu cầu "nhẹ" | GPL |
| Tự viết bằng Media Foundation + WGC qua COM | nhẹ nhất | rất nhiều mã COM chưa kiểm được trên Mac, rủi ro cao | - |
| Hook DirectX như Bandicam | bắt được game fullscreen độc quyền | bị anti-cheat chặn, dễ crash game | - |

## Bẫy
- Chiều rộng, chiều cao phải chẵn (4:2:0); NVENC có kích thước tối thiểu, nên vùng chọn tối thiểu 160×90.
- Game fullscreen độc quyền thật sự (rất hiếm từ Windows 10 nhờ "fullscreen optimizations") có thể ra màn đen: hướng dẫn người dùng chọn chế độ "Borderless".
- Nội dung có DRM (Netflix trong Edge) ra màn đen: đó là chủ ý của Windows.
