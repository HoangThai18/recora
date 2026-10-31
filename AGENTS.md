# Recora

App quay màn hình miễn phí cho Windows 10/11 kiểu Bandicam (F12 là ghi, file nhỏ mà nét, ghi bằng GPU nên không tụt FPS game), do thaipro.store viết, mã nguồn mở MIT. Giao diện tiếng Việt.

## Lệnh
- Build (Mac hoặc Windows, cần .NET SDK 10): `dotnet build Recora.sln -c Release`. Trên Mac vẫn biên dịch được `Recora.App` (net48) nhờ gói reference assemblies, nhưng không chạy được.
- Test: `dotnet test tests/Recora.Core.Tests` · một lớp: `dotnet test tests/Recora.Core.Tests --filter FullyQualifiedName~RecordingCommandTests`
- FFmpeg: chỉ build trên CI (`tools/ffmpeg/build.sh`, chạy trong MSYS2 UCRT64). Có cache theo nội dung script.
- Chạy thử ghi thật (Windows, không giao diện): `Recora.exe --selftest <thư mục>`; chụp cửa sổ chính: `Recora.exe --ui-screenshot <file.png>`.
- CI: `.github/workflows/ci.yml` (build FFmpeg, build app, test, selftest, đóng gói). Phát hành: `.github/workflows/release.yml` chạy khi đẩy tag `recora-x.y.z`.

## Cấu trúc
- `src/Recora.Core/`: logic không đụng Windows (dựng lệnh FFmpeg, bộ mã hoá, chất lượng, trộn tiếng, phiên ghi, cài đặt, phím tắt). netstandard2.0.
- `src/Recora.App/`: app WinForms net48. `Native/` (P/Invoke, DXGI), `Audio/` (WASAPI qua NAudio, `AudioPump`), `Recording/` (tiến trình FFmpeg, `Recorder`), `UI/`.
- `tests/Recora.Core.Tests/`: xUnit cho `Recora.Core`.
- `tools/ffmpeg/`: script build FFmpeg tối giản. `installer/`: Inno Setup.
- `docs/plan/` PRD, kiến trúc, phase · `docs/domain/DOMAIN.md` thuật ngữ và quy tắc `BR-xx` · `docs/research/` · `docs/tasks/` task card · `docs/STATUS.md` tiến độ.

## Quy tắc bắt buộc
- `Recora.Core` không gọi API Windows (không WinForms, không P/Invoke) để test được trên Mac. Logic quyết định đặt ở Core, App chỉ nối vào Windows.
- Mọi đối số FFmpeg dựng trong Core qua `FfmpegArgs` và các builder; App không ghép chuỗi lệnh.
- Người dùng chỉ thấy câu tiếng Việt dễ hiểu. stderr của FFmpeg, message của exception, mã lỗi Windows chỉ vào nhật ký.
- Không phá các quy tắc trong `docs/domain/DOMAIN.md`, nhất là BR-01 (ghi từng đoạn `.mkv`, không mất video), BR-02 (GPU trước, tự lùi), BR-05 (cửa sổ Recora không lọt vào video).
- Tên class, file, biến viết tiếng Anh; chuỗi giao diện tiếng Việt; chú thích tiếng Anh, chỉ nói *tại sao*, không kể lại code, không ghi chú "đã sửa".
- Logic sắp xuất hiện lần thứ ba thì gom thành một hàm, trỏ mọi bản sao vào nó.
- Dùng thư viện có sẵn trước khi tự viết (JSON: `DataContractJsonSerializer`; resample tiếng: `WdlResampler` của NAudio). Thêm gói NuGet, gói MSYS2 hay thành phần FFmpeg phải kèm lý do trong commit.
- Test: không làm cho xanh bằng cách xoá, bỏ qua, nới lỏng test hay sửa giá trị mong đợi cho khớp kết quả mới.
- Git: commit mốc trước mỗi task. Hoàn tác bằng `git restore <file>` hoặc `git revert <sha>`. `git reset --hard`, `git clean`, force-push chỉ khi chủ sản phẩm đồng ý đúng lệnh đó.
- Release là việc công khai: chỉ đẩy tag `recora-*` khi chủ sản phẩm yêu cầu. Mỗi bản đã phát hành phải tăng `Version` trong `Directory.Build.props`.

## Quy ước
- Module mẫu để bắt chước: `src/Recora.Core/Ffmpeg/` (builder thuần, test đi kèm trong `tests/Recora.Core.Tests/Ffmpeg/`).
- Lỗi trong Core: `RecoraException` mang `Code` (chuỗi ngắn); App quy `Code` ra câu tiếng Việt ở `App/UI/FriendlyErrors.cs`.
- Test: tên `Method_WhenCondition_Expected`, dữ liệu dựng ngay trong test, không mock Windows (Core không đụng Windows).
- Commit: `<TASK-ID>: <mô tả tiếng Việt>`; việc ngoài task: `<Khu vực>: <mô tả>`.

## Ghi chú stack
- C# `LangVersion` 12 trên netstandard2.0/net48: `record` và `init` cần `IsExternalInit` (có sẵn trong `Core/Polyfills.cs`). Không dùng `Span<T>`, `System.Text.Json` (thêm DLL).
- FFmpeg 8.1.3: đường đi khung GPU, thang chất lượng từng bộ mã hoá, bẫy đồng bộ tiếng: `docs/research/capture-encode.md`.
- NAudio 2.2.1: `WasapiLoopbackCapture` không trả gói nào khi máy im lặng; `AudioPump` tự chèn im lặng theo đồng hồ.

## Quy trình
Dự án dùng bộ skill `vibe` (HoangThai18/vibe-code-pro; cài: `npx skills add HoangThai18/vibe-code-pro`): plan → tasks → build (một agent viết cả code lẫn test) → review → ship. Task đi `todo → doing → review` (code xong, giữ nhánh `task/<ID>`) `→ done` (merge vào `phase/Pn` sau khi review PASS). Chỉ phiên chính sửa task card và `docs/STATUS.md`; subagent (nếu có) chỉ báo kết quả.
