using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualBasic.FileIO;
using Recora.App.Logging;
using Recora.App.UI.Controls;
using Recora.Core.Ffmpeg;
using Recora.Core.Library;

namespace Recora.App.UI
{
    /// <summary>Thư viện: what Recora saved in the output folder, newest first, with open, show in folder and delete.</summary>
    internal sealed class LibraryPage : Page
    {
        private readonly Func<IReadOnlyList<LibraryItem>> _load;
        private readonly ListView _list;
        private readonly Label _summary;
        private readonly Card _card;
        private readonly FlatButton _open;
        private readonly FlatButton _trim;
        private readonly FlatButton _reveal;
        private readonly FlatButton _delete;

        public LibraryPage(Func<IReadOnlyList<LibraryItem>> load)
            : base("Thư viện")
        {
            _load = load;
            _card = AddCard("bản ghi", 470);
            _summary = Caption(string.Empty, Theme.Small, Theme.Muted, 8);

            _list = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                MultiSelect = false,
                BorderStyle = BorderStyle.FixedSingle,
                Font = Theme.Body,
                ForeColor = Theme.Text,
                Height = 330,
                Margin = new Padding(0, 0, 0, 12),
            };
            _list.Columns.Add("Tên", 300);
            _list.Columns.Add("Thời lượng", 90, HorizontalAlignment.Right);
            _list.Columns.Add("Dung lượng", 90, HorizontalAlignment.Right);
            _list.Columns.Add("Ngày ghi", 130);
            _list.DoubleClick += (_, _) => OpenSelected();
            _list.SelectedIndexChanged += (_, _) => UpdateButtons();
            _list.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Delete)
                {
                    DeleteSelected();
                }
                else if (e.KeyCode == Keys.Enter)
                {
                    OpenSelected();
                }
            };

            var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Theme.Surface, Margin = Padding.Empty };
            _open = new FlatButton("Mở") { Primary = true, Size = new Size(90, 34), Margin = new Padding(0, 0, 10, 0) };
            _trim = new FlatButton("Cắt") { Size = new Size(90, 34), Margin = new Padding(0, 0, 10, 0) };
            _reveal = new FlatButton("Xem trong thư mục") { Size = new Size(170, 34), Margin = new Padding(0, 0, 10, 0) };
            _delete = new FlatButton("Xoá") { Size = new Size(90, 34), Margin = new Padding(0, 0, 10, 0) };
            var refresh = new FlatButton("Làm mới") { Size = new Size(110, 34), Margin = Padding.Empty };
            _open.Click += (_, _) => OpenSelected();
            _trim.Click += (_, _) =>
            {
                if (Selected is LibraryItem item)
                {
                    TrimRequested?.Invoke(this, item);
                }
            };
            _reveal.Click += (_, _) => RevealSelected();
            _delete.Click += (_, _) => DeleteSelected();
            refresh.Click += (_, _) => Reload();
            buttons.Controls.AddRange(new Control[] { _open, _trim, _reveal, _delete, refresh });

            _card.Body.Controls.Add(_summary);
            _card.Body.Controls.Add(_list);
            _card.Body.Controls.Add(buttons);
            _card.Resize += (_, _) => FitList();
            VisibleChanged += (_, _) =>
            {
                if (Visible)
                {
                    Reload();
                }
            };
        }

        /// <summary>"Cắt" on a video or a sound file; the controller opens the cut dialog.</summary>
        public event EventHandler<LibraryItem>? TrimRequested;

        /// <summary>Selects a file (a fresh cut) and scrolls to it.</summary>
        public void Select(string path)
        {
            foreach (ListViewItem row in _list.Items)
            {
                if (row.Tag is LibraryItem item && string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase))
                {
                    row.Selected = true;
                    row.Focused = true;
                    row.EnsureVisible();
                    _list.Focus();
                    return;
                }
            }
        }

        public void Reload()
        {
            IReadOnlyList<LibraryItem> items;
            try
            {
                items = _load();
            }
            catch (IOException error)
            {
                Log.Warn("library scan failed: " + error.Message);
                items = new LibraryItem[0];
            }
            catch (UnauthorizedAccessException error)
            {
                Log.Warn("library scan failed: " + error.Message);
                items = new LibraryItem[0];
            }

            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var item in items)
            {
                var duration = item.IsImage ? "ảnh" : item.Duration.HasValue ? LibraryIndex.FormatDuration(item.Duration.Value) : "—";
                var row = new ListViewItem(new[] { item.Name, duration, LibraryIndex.FormatBytes(item.Bytes), item.Modified.ToString("dd/MM/yyyy HH:mm") }) { Tag = item };
                _list.Items.Add(row);
            }

            _list.EndUpdate();
            var videos = items.Count(i => !i.IsImage);
            var total = items.Sum(i => i.Bytes);
            _summary.Text = items.Count == 0
                ? "Chưa có bản ghi nào. Bấm nút đỏ hoặc F12 để ghi."
                : $"{videos} video · {items.Count - videos} ảnh · tổng {LibraryIndex.FormatBytes(total)}";
            UpdateButtons();
        }

        private LibraryItem? Selected => _list.SelectedItems.Count > 0 ? _list.SelectedItems[0].Tag as LibraryItem : null;

        private void FitList()
        {
            var width = _card.Body.ClientSize.Width;
            if (width <= 0)
            {
                return;
            }

            _list.Width = width;
            var dpi = DeviceDpi / 96f * Theme.UiScale;
            var fixedColumns = (int)((90 + 90 + 130) * dpi);
            _list.Columns[1].Width = (int)(90 * dpi);
            _list.Columns[2].Width = (int)(90 * dpi);
            _list.Columns[3].Width = (int)(130 * dpi);
            _list.Columns[0].Width = Math.Max((int)(160 * dpi), width - fixedColumns - SystemInformation.VerticalScrollBarWidth - (int)(10 * dpi));
        }

        private void UpdateButtons()
        {
            var any = Selected != null;
            _open.Enabled = any;
            _trim.Enabled = Selected is LibraryItem item && !item.IsImage && TrimCommand.CanTrim(item.Path);
            _reveal.Enabled = any;
            _delete.Enabled = any;
        }

        private void OpenSelected()
        {
            var item = Selected;
            if (item == null || !File.Exists(item.Path))
            {
                Reload();
                return;
            }

            Process.Start(new ProcessStartInfo(item.Path) { UseShellExecute = true });
        }

        private void RevealSelected()
        {
            var item = Selected;
            if (item != null)
            {
                Process.Start("explorer.exe", $"/select,\"{item.Path}\"");
            }
        }

        private void DeleteSelected()
        {
            var item = Selected;
            if (item == null)
            {
                return;
            }

            var answer = MessageBox.Show(FindForm(), $"Chuyển \"{item.Name}\" vào Thùng rác?", "Xoá bản ghi", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (answer != DialogResult.OK)
            {
                return;
            }

            try
            {
                FileSystem.DeleteFile(item.Path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                Log.Info("deleted to recycle bin: " + item.Path);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is OperationCanceledException)
            {
                Log.Warn("delete failed: " + error.Message);
            }

            Reload();
        }
    }
}
