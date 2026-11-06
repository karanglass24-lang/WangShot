using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Greenshot.Editor.Controls;
using Greenshot.Editor.Tabs;

namespace Greenshot.Editor.Forms
{
    /// <summary>
    /// 왕샷: a button on the left of the tabs which shows all tabs with a picture of their image
    /// </summary>
    public partial class ImageEditorForm
    {
        private Control _tabOverviewButton;

        /// <summary>
        /// The button on the left of the tab strip, the strip keeps its own layout
        /// </summary>
        private void BuildTabOverview()
        {
            if (_tabOverviewButton != null || tabStrip.Parent == null)
            {
                return;
            }

            var parent = tabStrip.Parent;
            int index = parent.Controls.GetChildIndex(tabStrip);
            var row = new Panel { Dock = DockStyle.Top, Height = tabStrip.Height, Margin = Padding.Empty, Padding = Padding.Empty };
            parent.Controls.Remove(tabStrip);
            tabStrip.Dock = DockStyle.Fill;
            row.Controls.Add(tabStrip);
            _tabOverviewButton = new TabOverviewButton { Dock = DockStyle.Left, Width = LogicalToDeviceUnits(34) };
            _tabOverviewButton.Click += (s, e) => ShowAllTabs();
            row.Controls.Add(_tabOverviewButton);
            parent.Controls.Add(row);
            parent.Controls.SetChildIndex(row, index);
            new ToolTip().SetToolTip(_tabOverviewButton, "모든 탭 보기");
        }

        private void ShowAllTabs()
        {
            var tabs = Tabs.ToList();
            if (tabs.Count == 0)
            {
                return;
            }

            var theme = EditorTheme.Current;
            int cardWidth = LogicalToDeviceUnits(150);
            int cardHeight = LogicalToDeviceUnits(120);
            int columns = Math.Min(4, Math.Max(2, tabs.Count));
            var flow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                MaximumSize = new Size(columns * (cardWidth + LogicalToDeviceUnits(8)) + LogicalToDeviceUnits(8), 0),
                Padding = new Padding(LogicalToDeviceUnits(4)),
                BackColor = theme.Window
            };
            var dropDown = new ToolStripDropDown { Padding = Padding.Empty, BackColor = theme.Window, AutoClose = true };
            var images = new List<Image>();
            foreach (var tab in tabs)
            {
                var thumbnail = MakeThumbnail(tab, cardWidth - LogicalToDeviceUnits(12), cardHeight - LogicalToDeviceUnits(36));
                if (thumbnail != null)
                {
                    images.Add(thumbnail);
                }

                var card = new TabCard(tab.Title, thumbnail, tab == SelectedTab) { Size = new Size(cardWidth, cardHeight), Margin = new Padding(LogicalToDeviceUnits(4)) };
                var target = tab;
                card.Click += (s, e) =>
                {
                    dropDown.Close();
                    SelectTab(target);
                };
                flow.Controls.Add(card);
            }

            dropDown.Items.Add(new ToolStripControlHost(flow) { Margin = Padding.Empty, Padding = Padding.Empty });
            dropDown.Closed += (s, e) =>
            {
                foreach (var image in images)
                {
                    image.Dispose();
                }

                BeginInvoke(new MethodInvoker(() => dropDown.Dispose()));
            };
            dropDown.Show(_tabOverviewButton, new Point(0, _tabOverviewButton.Height));
        }

        private static Image MakeThumbnail(EditorTab tab, int maxWidth, int maxHeight)
        {
            try
            {
                var source = tab.Surface?.Image;
                if (source == null || source.Width <= 0 || source.Height <= 0)
                {
                    return null;
                }

                double scale = Math.Min((double)maxWidth / source.Width, (double)maxHeight / source.Height);
                int width = Math.Max(1, (int)(source.Width * Math.Min(1.0, scale)));
                int height = Math.Max(1, (int)(source.Height * Math.Min(1.0, scale)));
                var bitmap = new Bitmap(width, height);
                using var graphics = Graphics.FromImage(bitmap);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.Clear(Color.White);
                graphics.DrawImage(source, new Rectangle(0, 0, width, height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel);
                return bitmap;
            }
            catch (Exception ex)
            {
                Log.Debug("Could not make a thumbnail of a tab", ex);
                return null;
            }
        }

        /// <summary>
        /// The button with the grid icon
        /// </summary>
        private sealed class TabOverviewButton : Control
        {
            private bool _hot;

            public TabOverviewButton()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                Cursor = Cursors.Hand;
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                base.OnMouseEnter(e);
                _hot = true;
                Invalidate();
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                _hot = false;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var theme = EditorTheme.Current;
                var graphics = e.Graphics;
                graphics.Clear(theme.Background);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                int size = Math.Min(Width, Height) - LogicalToDeviceUnits(10);
                var area = new Rectangle((Width - size) / 2, (Height - size) / 2 + LogicalToDeviceUnits(1), size, size);
                if (_hot)
                {
                    using var hotBrush = new SolidBrush(theme.HotTab);
                    graphics.FillRectangle(hotBrush, new Rectangle(LogicalToDeviceUnits(3), LogicalToDeviceUnits(4), Width - LogicalToDeviceUnits(6), Height - LogicalToDeviceUnits(4)));
                }

                // Four small squares: the tabs
                using var pen = new Pen(theme.Foreground, Math.Max(1.5f, LogicalToDeviceUnits(1)));
                int cell = (size - LogicalToDeviceUnits(3)) / 2;
                for (int row = 0; row < 2; row++)
                {
                    for (int column = 0; column < 2; column++)
                    {
                        graphics.DrawRectangle(pen, area.Left + column * (cell + LogicalToDeviceUnits(3)), area.Top + row * (cell + LogicalToDeviceUnits(3)), cell, cell);
                    }
                }
            }
        }

        /// <summary>
        /// A tab in the list of all tabs: its picture and its name
        /// </summary>
        private sealed class TabCard : Control
        {
            private readonly string _title;
            private readonly Image _thumbnail;
            private readonly bool _selected;
            private bool _hot;

            public TabCard(string title, Image thumbnail, bool selected)
            {
                _title = title ?? string.Empty;
                _thumbnail = thumbnail;
                _selected = selected;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                Cursor = Cursors.Hand;
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                base.OnMouseEnter(e);
                _hot = true;
                Invalidate();
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                _hot = false;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var theme = EditorTheme.Current;
                var graphics = e.Graphics;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(theme.Window);
                var bounds = new Rectangle(1, 1, Width - 3, Height - 3);
                using (var path = RoundedRectangle(bounds, LogicalToDeviceUnits(6)))
                {
                    if (_hot)
                    {
                        using var hotBrush = new SolidBrush(theme.HotTab);
                        graphics.FillPath(hotBrush, path);
                    }

                    using var pen = new Pen(_selected ? theme.Highlight : theme.Border, _selected ? Math.Max(2, LogicalToDeviceUnits(2)) : 1);
                    graphics.DrawPath(pen, path);
                }

                int titleHeight = LogicalToDeviceUnits(24);
                var pictureArea = new Rectangle(LogicalToDeviceUnits(6), LogicalToDeviceUnits(6), Width - LogicalToDeviceUnits(12), Height - titleHeight - LogicalToDeviceUnits(10));
                if (_thumbnail != null)
                {
                    var picture = new Rectangle(pictureArea.Left + (pictureArea.Width - _thumbnail.Width) / 2, pictureArea.Top + (pictureArea.Height - _thumbnail.Height) / 2,
                        _thumbnail.Width, _thumbnail.Height);
                    using var shadowBrush = new SolidBrush(Color.FromArgb(40, 0, 0, 0));
                    graphics.FillRectangle(shadowBrush, new Rectangle(picture.Left + 2, picture.Top + 2, picture.Width, picture.Height));
                    graphics.DrawImage(_thumbnail, picture);
                    using var framePen = new Pen(theme.Border);
                    graphics.DrawRectangle(framePen, picture.Left, picture.Top, picture.Width - 1, picture.Height - 1);
                }

                TextRenderer.DrawText(graphics, _title, Font, new Rectangle(LogicalToDeviceUnits(6), Height - titleHeight - LogicalToDeviceUnits(2), Width - LogicalToDeviceUnits(12), titleHeight),
                    _selected ? theme.WindowText : theme.Foreground,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }

            private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
            {
                var path = new GraphicsPath();
                int d = radius * 2;
                path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
                path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
                path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
                path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                return path;
            }
        }
    }
}
