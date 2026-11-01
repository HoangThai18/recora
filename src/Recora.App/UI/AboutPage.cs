using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Recora.App.Logging;
using Recora.App.UI.Controls;

namespace Recora.App.UI
{
    internal sealed class AboutPage : Page
    {
        public const string Homepage = "https://thaipro.store/phan-mem/recora";
        public const string SourceCode = "https://github.com/HoangThai18/recora";

        public AboutPage(string version, string? ffmpegVersion)
            : base("Giới thiệu")
        {
            var about = AddCard("recora");
            about.Body.Controls.Add(Caption("Recora " + version, Theme.Title, Theme.Accent, 4));
            about.Body.Controls.Add(Caption(
                "Quay màn hình miễn phí cho Windows: ghi bằng GPU nên không làm tụt FPS game, file nhỏ mà vẫn nét. "
                + "Không watermark, không giới hạn thời gian, không gửi dữ liệu đi đâu.",
                Theme.Body, Theme.Text, 14));
            about.Body.Controls.Add(Links(("Trang tải", Homepage), ("Mã nguồn", SourceCode)));
            _update = Caption(string.Empty, Theme.Small, Theme.Muted, 0);
            CheckUpdate = new FlatButton("Kiểm tra bản mới") { Size = new Size(170, 34), Margin = new Padding(0, 0, 10, 0) };
            UpdateNow = new FlatButton("Cập nhật ngay") { Primary = true, Size = new Size(210, 34), Margin = Padding.Empty, Visible = false };
            var updateRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Theme.Surface, Margin = new Padding(0, 12, 0, 4) };
            updateRow.Controls.Add(CheckUpdate);
            updateRow.Controls.Add(UpdateNow);
            about.Body.Controls.Add(updateRow);
            about.Body.Controls.Add(_update);

            var support = AddCard("khi gặp lỗi");
            support.Body.Controls.Add(Caption(
                "Recora ghi nhật ký trên máy để tìm lỗi. Gửi file nhật ký mới nhất khi báo lỗi; trong đó không có nội dung video.",
                Theme.Body, Theme.Text, 10));
            var logs = new FlatButton("Mở thư mục nhật ký") { Size = new Size(190, 34), Margin = Padding.Empty };
            logs.Click += (_, _) => OpenFolder(Log.DefaultDirectory);
            support.Body.Controls.Add(logs);

            var licenses = AddCard("giấy phép");
            licenses.Body.Controls.Add(Caption("Recora: giấy phép MIT.", Theme.Body, Theme.Text, 4));
            licenses.Body.Controls.Add(Caption(
                "Đi kèm FFmpeg" + (ffmpegVersion == null ? string.Empty : " (" + ffmpegVersion + ")")
                + " dựng với x264, giấy phép GPL v2 trở lên; mã nguồn đúng bản dựng có trong trang phát hành. "
                + "NAudio: giấy phép MIT. Danh sách đầy đủ trong THIRD-PARTY-NOTICES.txt cạnh Recora.exe.",
                Theme.Small, Theme.Muted, 4));
        }

        private readonly Label _update;

        public FlatButton CheckUpdate { get; }

        /// <summary>Shown once a newer release is known: downloads, installs and restarts Recora.</summary>
        public FlatButton UpdateNow { get; }

        public void OfferUpdate(Version version)
        {
            UpdateNow.Text = "Cập nhật lên " + version.ToString(3);
            UpdateNow.Visible = true;
            ShowUpdate($"Có bản {version.ToString(3)}. Bấm Cập nhật: Recora tự tải, cài rồi mở lại, cài đặt giữ nguyên.");
        }

        public void ShowUpdate(string text) => _update.Text = text;

        private static FlowLayoutPanel Links(params (string Text, string Url)[] links)
        {
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Theme.Surface, Margin = Padding.Empty };
            foreach (var link in links)
            {
                var button = new FlatButton(link.Text) { Size = new Size(130, 34), Margin = new Padding(0, 0, 10, 0) };
                button.Click += (_, _) => Process.Start(new ProcessStartInfo(link.Url) { UseShellExecute = true });
                row.Controls.Add(button);
            }

            return row;
        }

        private static void OpenFolder(string path)
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }
}
