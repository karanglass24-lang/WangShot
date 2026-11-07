using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Greenshot.Editor.Controls
{
    /// <summary>
    /// 왕샷: the colours of the ribbon like in Office: two buttons, "도형 채우기" and "도형 윤곽선", each opens a colour grid
    /// (theme colours with their shades, standard colours, the last colours, no colour, more colours).
    /// </summary>
    internal sealed class ColorPaletteControl : Control
    {
        private readonly List<Color> _recent = new();
        private Color _line = Color.Red;
        private Color _fill = Color.Transparent;
        private int _hotRow = -1;
        private ToolStripDropDown _popup;

        /// <summary>
        /// A colour was chosen: the colour, and true for the fill colour, false for the line colour
        /// </summary>
        public event Action<Color, bool> ColorChosen;

        public event Action<int> ThicknessChanged;

        private readonly NumericUpDown _thickness = new() { Minimum = 0, Maximum = 100, TextAlign = HorizontalAlignment.Center, Enabled = false };
        private bool _syncingThickness;

        public ColorPaletteControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Size = new Size(LogicalToDeviceUnits(146), LogicalToDeviceUnits(80));
            _thickness.Size = new Size(LogicalToDeviceUnits(52), LogicalToDeviceUnits(21));
            _thickness.ValueChanged += (s, e) =>
            {
                if (!_syncingThickness)
                {
                    ThicknessChanged?.Invoke((int)_thickness.Value);
                }
            };
            Controls.Add(_thickness);
            PositionThickness();
        }

        /// <summary>
        /// The line thickness of the tool or the selected element, in the third row; without a line the field is disabled
        /// </summary>
        public void SetThickness(bool available, int value)
        {
            _syncingThickness = true;
            try
            {
                _thickness.Enabled = available;
                _thickness.Value = Math.Max(_thickness.Minimum, Math.Min(_thickness.Maximum, value));
            }
            finally
            {
                _syncingThickness = false;
            }

            Invalidate();
        }

        private void PositionThickness()
        {
            var row = RowBounds(2);
            _thickness.Location = new Point(row.Right - _thickness.Width - LogicalToDeviceUnits(4), row.Top + (row.Height - _thickness.Height) / 2);
        }

        public void SetColors(Color line, Color fill)
        {
            if (line == _line && fill == _fill)
            {
                return;
            }

            _line = line;
            _fill = fill;
            Invalidate();
        }

        private Rectangle RowBounds(int row) => new(LogicalToDeviceUnits(2), LogicalToDeviceUnits(3) + row * LogicalToDeviceUnits(25), Width - LogicalToDeviceUnits(4), LogicalToDeviceUnits(24));

        private int HitRow(Point location)
        {
            for (int row = 0; row < 2; row++)
            {
                if (RowBounds(row).Contains(location))
                {
                    return row;
                }
            }

            return -1;
        }

        internal static void DrawSwatch(Graphics graphics, Rectangle bounds, Color color)
        {
            if (color.A == 0)
            {
                // No colour: white with a red slash
                graphics.FillRectangle(Brushes.White, bounds);
                using var slash = new Pen(Color.FromArgb(220, 40, 40), 2);
                graphics.DrawLine(slash, bounds.Left + 2, bounds.Bottom - 2, bounds.Right - 2, bounds.Top + 2);
            }
            else
            {
                using var brush = new SolidBrush(color);
                graphics.FillRectangle(brush, bounds);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EditorTheme.Current;
            var graphics = e.Graphics;
            graphics.Clear(theme.Window);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var border = new Pen(theme.Border);
            var thicknessRow = RowBounds(2);
            TextRenderer.DrawText(graphics, "선 두께", Font, new Rectangle(thicknessRow.Left + LogicalToDeviceUnits(8), thicknessRow.Top, thicknessRow.Width - _thickness.Width - LogicalToDeviceUnits(12), thicknessRow.Height),
                _thickness.Enabled ? theme.WindowText : theme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            for (int row = 0; row < 2; row++)
            {
                var bounds = RowBounds(row);
                if (row == _hotRow)
                {
                    using var hotBrush = new SolidBrush(theme.HotTab);
                    graphics.FillRectangle(hotBrush, bounds);
                    using var hotPen = new Pen(theme.Highlight);
                    graphics.DrawRectangle(hotPen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
                }

                var swatch = new Rectangle(bounds.Left + LogicalToDeviceUnits(6), bounds.Top + (bounds.Height - LogicalToDeviceUnits(18)) / 2, LogicalToDeviceUnits(18), LogicalToDeviceUnits(18));
                DrawSwatch(graphics, swatch, row == 0 ? _fill : _line);
                graphics.DrawRectangle(border, swatch);
                TextRenderer.DrawText(graphics, row == 0 ? "채우기" : "윤곽선", Font,
                    new Rectangle(swatch.Right + LogicalToDeviceUnits(6), bounds.Top, bounds.Width - swatch.Width - LogicalToDeviceUnits(32), bounds.Height),
                    theme.WindowText, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                using var pen = new Pen(theme.WindowText, 1.5f);
                int cx = bounds.Right - LogicalToDeviceUnits(12);
                int cy = bounds.Top + bounds.Height / 2;
                graphics.DrawLines(pen, new[] { new Point(cx - 3, cy - 1), new Point(cx, cy + 2), new Point(cx + 3, cy - 1) });
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hot = HitRow(e.Location);
            if (hot != _hotRow)
            {
                _hotRow = hot;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hotRow != -1)
            {
                _hotRow = -1;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int row = HitRow(e.Location);
            if (row >= 0 && e.Button == MouseButtons.Left)
            {
                ShowPopup(row == 0);
            }
        }

        private void Choose(Color color, bool fill)
        {
            if (color.A != 0)
            {
                _recent.Remove(color);
                _recent.Insert(0, color);
                if (_recent.Count > 10)
                {
                    _recent.RemoveAt(10);
                }
            }

            ColorChosen?.Invoke(color, fill);
            Invalidate();
        }

        private void PickWithDialog(bool fill, Color current)
        {
            using var dialog = new ColorDialog { FullOpen = true, Color = current.A == 0 ? Color.White : current };
            if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
            {
                Choose(dialog.Color, fill);
            }
        }

        private void ShowPopup(bool fill)
        {
            _popup?.Dispose();
            var grid = new ColorGridPopup(fill, fill ? _fill : _line, _recent);
            var popup = new ToolStripDropDown { Padding = Padding.Empty, Margin = Padding.Empty, AutoSize = true, DropShadowEnabled = true };
            var host = new ToolStripControlHost(grid) { Padding = Padding.Empty, Margin = Padding.Empty, AutoSize = false, Size = grid.Size };
            popup.Items.Add(host);
            grid.Chosen += color =>
            {
                popup.Close();
                Choose(color, fill);
            };
            grid.MoreRequested += () =>
            {
                popup.Close();
                PickWithDialog(fill, fill ? _fill : _line);
            };
            _popup = popup;
            var row = RowBounds(fill ? 0 : 1);
            var location = PointToScreen(new Point(row.Left, row.Bottom));
            popup.Opacity = 0;
            popup.Show(location);
            ToolStripDropButton.Animate(popup, location);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _popup?.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// The colour grid of the drop-down: theme colours with shades, standard colours, last colours, no colour, more colours
    /// </summary>
    internal sealed class ColorGridPopup : Control
    {
        private static readonly Color[] ThemeColors =
        {
            Color.FromArgb(255, 255, 255), Color.FromArgb(0, 0, 0), Color.FromArgb(231, 230, 230), Color.FromArgb(68, 84, 106), Color.FromArgb(68, 114, 196),
            Color.FromArgb(237, 125, 49), Color.FromArgb(165, 165, 165), Color.FromArgb(255, 192, 0), Color.FromArgb(91, 155, 213), Color.FromArgb(112, 173, 71)
        };

        private static readonly Color[] StandardColors =
        {
            Color.FromArgb(192, 0, 0), Color.FromArgb(255, 0, 0), Color.FromArgb(255, 192, 0), Color.FromArgb(255, 255, 0), Color.FromArgb(146, 208, 80),
            Color.FromArgb(0, 176, 80), Color.FromArgb(0, 176, 240), Color.FromArgb(0, 112, 192), Color.FromArgb(0, 32, 96), Color.FromArgb(112, 48, 160)
        };

        private sealed class Item
        {
            public Rectangle Bounds;
            public Color? Color;
            public string Text;
            public bool More;
        }

        private readonly List<Item> _items = new();
        private readonly List<(string Text, int Y)> _headers = new();
        private readonly Color _current;
        private Item _hot;

        public event Action<Color> Chosen;

        public event Action MoreRequested;

        public ColorGridPopup(bool fill, Color current, IReadOnlyList<Color> recent)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Cursor = Cursors.Hand;
            _current = current;
            int cell = LogicalToDeviceUnits(20);
            int gap = LogicalToDeviceUnits(3);
            int pad = LogicalToDeviceUnits(10);
            int header = LogicalToDeviceUnits(24);
            int y = LogicalToDeviceUnits(4);
            int width = pad * 2 + 10 * cell + 9 * gap;

            _headers.Add(("테마 색", y));
            y += header;
            for (int column = 0; column < 10; column++)
            {
                _items.Add(new Item { Bounds = new Rectangle(pad + column * (cell + gap), y, cell, cell), Color = ThemeColors[column] });
            }

            y += cell + gap * 2;
            for (int row = 0; row < 5; row++)
            {
                for (int column = 0; column < 10; column++)
                {
                    _items.Add(new Item { Bounds = new Rectangle(pad + column * (cell + gap), y, cell, cell), Color = Shade(ThemeColors[column], row) });
                }

                y += cell + gap;
            }

            y += gap * 2;
            _headers.Add(("표준 색", y));
            y += header;
            for (int column = 0; column < 10; column++)
            {
                _items.Add(new Item { Bounds = new Rectangle(pad + column * (cell + gap), y, cell, cell), Color = StandardColors[column] });
            }

            y += cell + gap * 2;
            if (recent.Count > 0)
            {
                _headers.Add(("최근 색", y));
                y += header;
                for (int column = 0; column < Math.Min(10, recent.Count); column++)
                {
                    _items.Add(new Item { Bounds = new Rectangle(pad + column * (cell + gap), y, cell, cell), Color = recent[column] });
                }

                y += cell + gap * 2;
            }

            int rowHeight = LogicalToDeviceUnits(28);
            _items.Add(new Item { Bounds = new Rectangle(pad / 2, y, width - pad, rowHeight), Color = Color.Transparent, Text = fill ? "채우기 없음" : "윤곽선 없음" });
            y += rowHeight;
            _items.Add(new Item { Bounds = new Rectangle(pad / 2, y, width - pad, rowHeight), Text = "다른 색...", More = true });
            y += rowHeight + gap * 2;
            Size = new Size(width, y);
        }

        /// <summary>
        /// The shades of a theme colour like in Office: row 0..4 = lighter 80 %, 60 %, 40 %, darker 25 %, darker 50 %
        /// (white goes darker only, black lighter only)
        /// </summary>
        private static Color Shade(Color color, int row)
        {
            if (color.ToArgb() == Color.White.ToArgb())
            {
                return Mix(color, Color.Black, new[] { 0.05, 0.15, 0.25, 0.35, 0.50 }[row]);
            }

            if (color.ToArgb() == Color.Black.ToArgb())
            {
                return Mix(color, Color.White, new[] { 0.50, 0.35, 0.25, 0.15, 0.05 }[row]);
            }

            return row switch
            {
                0 => Mix(color, Color.White, 0.80),
                1 => Mix(color, Color.White, 0.60),
                2 => Mix(color, Color.White, 0.40),
                3 => Mix(color, Color.Black, 0.25),
                _ => Mix(color, Color.Black, 0.50)
            };
        }

        private static Color Mix(Color from, Color to, double amount) =>
            Color.FromArgb((int)(from.R + (to.R - from.R) * amount), (int)(from.G + (to.G - from.G) * amount), (int)(from.B + (to.B - from.B) * amount));

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EditorTheme.Current;
            var graphics = e.Graphics;
            graphics.Clear(theme.Window);
            using var border = new Pen(theme.Border);
            using var bold = new Font(Font, FontStyle.Bold);
            foreach (var (text, y) in _headers)
            {
                TextRenderer.DrawText(graphics, text, bold, new Rectangle(LogicalToDeviceUnits(10), y, Width, LogicalToDeviceUnits(22)), theme.WindowText,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }

            foreach (var item in _items)
            {
                if (item.Text != null)
                {
                    if (item == _hot)
                    {
                        using var hotBrush = new SolidBrush(theme.HotTab);
                        graphics.FillRectangle(hotBrush, item.Bounds);
                        using var hotPen = new Pen(theme.Highlight);
                        graphics.DrawRectangle(hotPen, item.Bounds.X, item.Bounds.Y, item.Bounds.Width - 1, item.Bounds.Height - 1);
                    }

                    var icon = new Rectangle(item.Bounds.Left + LogicalToDeviceUnits(6), item.Bounds.Top + (item.Bounds.Height - LogicalToDeviceUnits(18)) / 2, LogicalToDeviceUnits(18), LogicalToDeviceUnits(18));
                    if (item.More)
                    {
                        var dots = new[] { Color.FromArgb(237, 28, 36), Color.FromArgb(0, 162, 232), Color.FromArgb(34, 177, 76), Color.FromArgb(255, 127, 39) };
                        int half = icon.Width / 2;
                        for (int i = 0; i < 4; i++)
                        {
                            using var brush = new SolidBrush(dots[i]);
                            graphics.FillRectangle(brush, icon.Left + (i % 2) * half, icon.Top + (i / 2) * half, half - 1, half - 1);
                        }
                    }
                    else
                    {
                        ColorPaletteControl.DrawSwatch(graphics, icon, Color.Transparent);
                        graphics.DrawRectangle(border, icon);
                    }

                    TextRenderer.DrawText(graphics, item.Text, Font, new Rectangle(icon.Right + LogicalToDeviceUnits(8), item.Bounds.Top, item.Bounds.Width, item.Bounds.Height), theme.WindowText,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                    continue;
                }

                using (var brush = new SolidBrush(item.Color.Value))
                {
                    graphics.FillRectangle(brush, item.Bounds);
                }

                graphics.DrawRectangle(border, item.Bounds);
                bool selected = item.Color.Value.A != 0 && _current.A != 0 && item.Color.Value.ToArgb() == _current.ToArgb();
                if (item == _hot || selected)
                {
                    using var pen = new Pen(item == _hot ? theme.Highlight : theme.WindowText, 2);
                    graphics.DrawRectangle(pen, item.Bounds.Left - 1, item.Bounds.Top - 1, item.Bounds.Width + 1, item.Bounds.Height + 1);
                }
            }

            graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        }

        private Item HitItem(Point location)
        {
            foreach (var item in _items)
            {
                if (item.Bounds.Contains(location))
                {
                    return item;
                }
            }

            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var hot = HitItem(e.Location);
            if (hot != _hot)
            {
                _hot = hot;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hot != null)
            {
                _hot = null;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            var item = HitItem(e.Location);
            if (item == null || e.Button != MouseButtons.Left)
            {
                return;
            }

            if (item.More)
            {
                MoreRequested?.Invoke();
            }
            else if (item.Color != null)
            {
                Chosen?.Invoke(item.Color.Value);
            }
        }
    }
}
