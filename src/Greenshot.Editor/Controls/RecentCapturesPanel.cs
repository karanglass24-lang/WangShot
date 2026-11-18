/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2007-2026 Thomas Braun, Jens Klingen, Robin Krom
 *
 * For more information see: https://getgreenshot.org/
 * The Greenshot project is hosted on GitHub https://github.com/greenshot/greenshot
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Dapplo.Ini;
using Greenshot.Base.Core;
using Greenshot.Base.Interfaces;
using Greenshot.Base.Threading;
using Greenshot.Editor.Drawing;
using Greenshot.Editor.Forms;
using log4net;

namespace Greenshot.Editor.Controls
{
    /// <summary>
    /// 왕샷: the panel with the recent captures, on the left of the canvas. A thumbnail is dragged onto the canvas to put that
    /// capture into the image which is edited (e.g. a screenshot of an earlier step), a double click opens it in a new tab.
    /// </summary>
    public sealed class RecentCapturesPanel : UserControl
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(RecentCapturesPanel));

        /// <summary>
        /// The newest captures of the save folder, no more
        /// </summary>
        private const int MaximumCaptures = 30;

        private const int PanelWidth = 178;
        private const int ThumbnailWidth = 148;
        private const int ThumbnailHeight = 92;

        private static readonly HashSet<string> ImageExtensions = new HashSet<string>(new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff" }, StringComparer.OrdinalIgnoreCase);

        private readonly ImageEditorForm _editor;
        private readonly Panel _header;
        private readonly Label _title;
        private readonly Button _refreshButton;
        private readonly Label _emptyLabel;
        private readonly FlowLayoutPanel _list;
        private readonly ToolTip _toolTip = new ToolTip();

        private CancellationTokenSource _loadCancellation;

        public RecentCapturesPanel(ImageEditorForm editor)
        {
            _editor = editor ?? throw new ArgumentNullException(nameof(editor));
            SuspendLayout();
            Dock = DockStyle.Left;
            Width = PanelWidth;
            MinimumSize = new Size(PanelWidth, 0);
            Name = "recentCapturesPanel";
            DoubleBuffered = true;

            _header = new Panel { Dock = DockStyle.Top, Height = 32 };
            _title = new Label
            {
                Text = "최근 캡처",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                Font = new Font(Font, FontStyle.Bold)
            };
            _refreshButton = new Button
            {
                Text = "↻",
                Dock = DockStyle.Right,
                Width = 28,
                FlatStyle = FlatStyle.Flat,
                TabStop = false
            };
            _refreshButton.FlatAppearance.BorderSize = 0;
            _refreshButton.Click += (s, e) => Reload();
            _toolTip.SetToolTip(_refreshButton, "최근 캡처 목록을 새로 고침");
            _header.Controls.Add(_title);
            _header.Controls.Add(_refreshButton);
            _header.Paint += (s, e) =>
            {
                using var pen = new Pen(EditorTheme.Current.Border);
                e.Graphics.DrawLine(pen, 0, _header.Height - 1, _header.Width, _header.Height - 1);
            };

            _list = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(6, 6, 6, 6),
                AllowDrop = false
            };

            _emptyLabel = new Label
            {
                Text = "아직 캡처가 없어요.\n저장 폴더에 캡처가 쌓이면\n여기에 보여요.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };

            Controls.Add(_list);
            Controls.Add(_emptyLabel);
            Controls.Add(_header);
            ResumeLayout(true);

            ApplyTheme();
        }

        /// <summary>
        /// The panel is first shown: the captures of the save folder are read
        /// </summary>
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible && _list.Controls.Count == 0)
            {
                Reload();
            }
        }

        /// <summary>
        /// The colours of the editor, also when the theme changes
        /// </summary>
        public void ApplyTheme()
        {
            var theme = EditorTheme.Current;
            BackColor = theme.Background;
            ForeColor = theme.Foreground;
            _header.BackColor = theme.Background;
            _title.ForeColor = theme.Foreground;
            _refreshButton.BackColor = theme.Background;
            _refreshButton.ForeColor = theme.Foreground;
            _list.BackColor = theme.Background;
            _emptyLabel.ForeColor = theme.Muted;
            _emptyLabel.BackColor = theme.Background;
            foreach (Control control in _list.Controls)
            {
                control.BackColor = theme.Background;
            }

            Invalidate(true);
        }

        /// <summary>
        /// Read the save folder and show the newest captures
        /// </summary>
        public void Reload()
        {
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            var cancellation = _loadCancellation = new CancellationTokenSource();
            RefreshAsync(cancellation.Token).FireAndLog("Load the recent captures of the editor", Log);
        }

        private async Task RefreshAsync(CancellationToken cancellationToken)
        {
            string folder = null;
            try
            {
                var configuration = IniConfigRegistry.GetSection<ICoreConfiguration>();
                folder = configuration?.GetOutputDirectory(out _);
            }
            catch (Exception ex)
            {
                Log.Warn("Could not read the save folder of the captures", ex);
            }

            if (string.IsNullOrEmpty(folder))
            {
                ShowCaptures(new List<(string Path, DateTime Modified, Image Thumbnail)>());
                return;
            }

            // PARALLEL: the thumbnails are decoded next to the UI, a panel full of captures must not stall the editor
#pragma warning disable RS0030 // R10: documented parallel branch
            var captures = await Task.Run(() => LoadThumbnails(folder, cancellationToken), cancellationToken);
#pragma warning restore RS0030
            if (cancellationToken.IsCancellationRequested || IsDisposed)
            {
                return;
            }

            ShowCaptures(captures);
        }

        private static List<(string Path, DateTime Modified, Image Thumbnail)> LoadThumbnails(string folder, CancellationToken cancellationToken)
        {
            var captures = new List<(string, DateTime, Image)>();
            try
            {
                var files = new DirectoryInfo(folder).EnumerateFiles()
                    .Where(file => ImageExtensions.Contains(file.Extension))
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .Take(MaximumCaptures);
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    captures.Add((file.FullName, file.LastWriteTime, LoadThumbnail(file.FullName)));
                }
            }
            catch (DirectoryNotFoundException)
            {
                // The folder is gone: no captures
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Warn("Could not read the recent captures of " + folder, ex);
            }

            return captures;
        }

        /// <summary>
        /// The picture of a capture, small enough for the panel, also for a tall full page capture
        /// </summary>
        private static Image LoadThumbnail(string path)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var source = Image.FromStream(stream);
                var thumbnail = new Bitmap(ThumbnailWidth, ThumbnailHeight, PixelFormat.Format32bppArgb);
                using var graphics = Graphics.FromImage(thumbnail);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.Clear(Color.Transparent);
                double scale = Math.Min(ThumbnailWidth / (double)source.Width, ThumbnailHeight / (double)source.Height);
                int width = Math.Max(1, (int)Math.Round(source.Width * scale));
                int height = Math.Max(1, (int)Math.Round(source.Height * scale));
                graphics.DrawImage(source, (ThumbnailWidth - width) / 2, (ThumbnailHeight - height) / 2, width, height);
                return thumbnail;
            }
            catch (Exception ex)
            {
                Log.Debug("Could not read the thumbnail of " + path, ex);
                return null;
            }
        }

        private void ShowCaptures(List<(string Path, DateTime Modified, Image Thumbnail)> captures)
        {
            var theme = EditorTheme.Current;
            _list.SuspendLayout();
            foreach (Control control in _list.Controls.Cast<Control>().ToList())
            {
                control.Dispose();
            }

            _list.Controls.Clear();
            foreach (var (path, modified, thumbnail) in captures)
            {
                _list.Controls.Add(CreateCaptureBox(path, modified, thumbnail, theme));
            }

            _list.ResumeLayout(true);
            _emptyLabel.Visible = captures.Count == 0;
            _list.Visible = captures.Count > 0;
            if (_emptyLabel.Visible)
            {
                _emptyLabel.BringToFront();
            }
        }

        private Control CreateCaptureBox(string path, DateTime modified, Image thumbnail, EditorTheme theme)
        {
            var picture = new PictureBox
            {
                Image = thumbnail,
                Width = ThumbnailWidth,
                Height = ThumbnailHeight,
                SizeMode = PictureBoxSizeMode.Zoom,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 6),
                BackColor = theme.Hover,
                Tag = path
            };
            var name = Path.GetFileName(path);
            _toolTip.SetToolTip(picture, $"{name}\n{modified:yyyy-MM-dd HH:mm}\n캔버스로 끌어다 놓거나 두 번 클릭하면 새 탭에서 열려요.");

            picture.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Left || thumbnail == null)
                {
                    return;
                }

                var data = new DataObject();
                data.SetData(Surface.RecentCaptureFormat, path);
                DoDragDrop(data, DragDropEffects.Copy);
            };
            picture.DoubleClick += (s, e) => _editor.OpenRecentCaptureInNewTab(path);
            return picture;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _loadCancellation?.Cancel();
                _loadCancellation?.Dispose();
                _loadCancellation = null;
                _toolTip.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
