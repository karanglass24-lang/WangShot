using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Greenshot.Editor.Controls
{
    /// <summary>
    /// 왕샷: the colours of the ribbon: the line colour (Color1) and the fill colour (Color2) as two large squares, and a palette. A click on a
    /// colour of the palette sets the line colour, a right click the fill colour. The last colours chosen are in the third row.
    /// </summary>
    internal sealed class ColorPaletteControl : Control
    {
        private static readonly Color[] Palette =
        {
            Color.FromArgb(0, 0, 0), Color.FromArgb(127, 127, 127), Color.FromArgb(136, 0, 21), Color.FromArgb(237, 28, 36), Color.FromArgb(255, 127, 39),
            Color.FromArgb(255, 242, 0), Color.FromArgb(34, 177, 76), Color.FromArgb(0, 162, 232), Color.FromArgb(63, 72, 204), Color.FromArgb(163, 73, 164),
            Color.FromArgb(255, 255, 255), Color.FromArgb(195, 195, 195), Color.FromArgb(185, 122, 87), Color.FromArgb(255, 174, 201), Color.FromArgb(255, 201, 14),
            Color.FromArgb(239, 228, 176), Color.FromArgb(181, 230, 29), Color.FromArgb(153, 217, 234), Color.FromArgb(112, 146, 190), Color.FromArgb(200, 191, 231)
        };

        private readonly List<Color> _recent = new();
        private Color _line = Color.Red;
        private Color _fill = Color.Transparent;
        private int _hotCell = -1;

        /// <summary>
        /// A colour was chosen: the colour, and true for the fill colour, false for the line colour
        /// </summary>
        public event Action<Color, bool> ColorChosen;

        public ColorPaletteControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Size = new Size(LogicalToDeviceUnits(362), LogicalToDeviceUnits(66));
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

        private int Cell => LogicalToDeviceUnits(18);

        private Rectangle LineSquare => new(LogicalToDeviceUnits(6), LogicalToDeviceUnits(4), LogicalToDeviceUnits(32), LogicalToDeviceUnits(32));

        private Rectangle FillSquare => new(LogicalToDeviceUnits(46), LogicalToDeviceUnits(4), LogicalToDeviceUnits(32), LogicalToDeviceUnits(32));

        private Point GridOrigin => new(LogicalToDeviceUnits(90), LogicalToDeviceUnits(4));

        private Rectangle MoreButton => new(LogicalToDeviceUnits(90) + 10 * (Cell + LogicalToDeviceUnits(2)) + LogicalToDeviceUnits(4), LogicalToDeviceUnits(4), LogicalToDeviceUnits(56), LogicalToDeviceUnits(46));

        private Rectangle CellBounds(int index)
        {
            int column = index % 10;
            int row = index / 10;
            return new Rectangle(GridOrigin.X + column * (Cell + LogicalToDeviceUnits(2)), GridOrigin.Y + row * (Cell + LogicalToDeviceUnits(2)), Cell, Cell);
        }

        private Color? CellColor(int index) => index < Palette.Length ? Palette[index] : index - Palette.Length < _recent.Count ? _recent[index - Palette.Length] : (Color?)null;

        private int HitCell(Point location)
        {
            for (int i = 0; i < 30; i++)
            {
                if (CellBounds(i).Contains(location))
                {
                    return i;
                }
            }

            return -1;
        }

        private static void DrawSwatch(Graphics graphics, Rectangle bounds, Color color)
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
            using var small = new Font(Font.FontFamily, Math.Max(7f, Font.Size - 1f));
            foreach (var (square, color, caption) in new[] { (LineSquare, _line, "선"), (FillSquare, _fill, "채움") })
            {
                DrawSwatch(graphics, square, color);
                graphics.DrawRectangle(border, square);
                TextRenderer.DrawText(graphics, caption, small, new Rectangle(square.Left - LogicalToDeviceUnits(6), square.Bottom + LogicalToDeviceUnits(2), square.Width + LogicalToDeviceUnits(12), LogicalToDeviceUnits(18)),
                    theme.WindowText, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPrefix);
            }

            for (int i = 0; i < 30; i++)
            {
                var cell = CellBounds(i);
                var color = CellColor(i);
                if (color == null)
                {
                    using var empty = new SolidBrush(theme.HotTab);
                    graphics.FillRectangle(empty, cell);
                    continue;
                }

                DrawSwatch(graphics, cell, color.Value);
                graphics.DrawRectangle(border, cell);
                if (i == _hotCell)
                {
                    using var hot = new Pen(theme.Highlight, 2);
                    graphics.DrawRectangle(hot, cell.Left - 1, cell.Top - 1, cell.Width + 1, cell.Height + 1);
                }
            }

            var more = MoreButton;
            if (_hotCell == 100)
            {
                using var hotBrush = new SolidBrush(theme.HotTab);
                graphics.FillRectangle(hotBrush, more);
            }

            // Four dots, like the "more colours" of Paint
            var dots = new[] { Color.FromArgb(237, 28, 36), Color.FromArgb(0, 162, 232), Color.FromArgb(34, 177, 76), Color.FromArgb(255, 127, 39) };
            int r = LogicalToDeviceUnits(5);
            int cx = more.Left + more.Width / 2;
            int cy = more.Top + LogicalToDeviceUnits(14);
            var offsets = new[] { new Point(-7, -4), new Point(5, -7), new Point(-1, 5), new Point(8, 3) };
            for (int i = 0; i < 4; i++)
            {
                using var brush = new SolidBrush(dots[i]);
                graphics.FillEllipse(brush, cx + LogicalToDeviceUnits(offsets[i].X) - r, cy + LogicalToDeviceUnits(offsets[i].Y) - r, r * 2, r * 2);
            }

            TextRenderer.DrawText(graphics, "더 보기", small, new Rectangle(more.Left, more.Top + LogicalToDeviceUnits(26), more.Width, LogicalToDeviceUnits(18)), theme.WindowText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPrefix);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hot = MoreButton.Contains(e.Location) ? 100 : HitCell(e.Location);
            if (hot != _hotCell)
            {
                _hotCell = hot;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hotCell != -1)
            {
                _hotCell = -1;
                Invalidate();
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

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (LineSquare.Contains(e.Location))
            {
                PickWithDialog(false, _line);
                return;
            }

            if (FillSquare.Contains(e.Location))
            {
                PickWithDialog(true, _fill);
                return;
            }

            if (MoreButton.Contains(e.Location))
            {
                PickWithDialog(e.Button == MouseButtons.Right, e.Button == MouseButtons.Right ? _fill : _line);
                return;
            }

            int cell = HitCell(e.Location);
            var color = cell >= 0 ? CellColor(cell) : null;
            if (color != null)
            {
                Choose(color.Value, e.Button == MouseButtons.Right);
            }
        }
    }
}
