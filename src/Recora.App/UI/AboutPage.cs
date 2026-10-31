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
