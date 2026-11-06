using System;
using System.Drawing;
using System.Windows.Forms;

namespace Greenshot.Editor.Controls
{
    /// <summary>
    /// 왕샷: a ruler (horizontal or vertical) along the canvas of the editor, in pixels of the image. Zero is the corner of the image,
    /// the scale follows the zoom, the part of the ruler over the image is lighter and the mouse has a red line.
    /// </summary>
    internal sealed class EditorRuler : Control
    {
        private static readonly int[] Steps = { 1, 2, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000, 2000, 5000, 10000, 20000, 50000 };
        private readonly bool _horizontal;
        private int _origin;
        private double _zoom = 1;
        private int _imageLength;
        private int _mark = -1;

        public EditorRuler(bool horizontal)
        {
            _horizontal = horizontal;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            TabStop = false;
        }

        /// <summary>
        /// Where the image starts on the ruler (client coordinates of the ruler), the zoom, the length of the image in pixels and where the mouse is
        /// </summary>
        public void Set(int origin, double zoom, int imageLength, int mark)
        {
            if (origin == _origin && Math.Abs(zoom - _zoom) < 0.0001 && imageLength == _imageLength && mark == _mark)
            {
                return;
            }

            _origin = origin;
            _zoom = zoom <= 0 ? 1 : zoom;
            _imageLength = imageLength;
            _mark = mark;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EditorTheme.Current;
            var graphics = e.Graphics;
            graphics.Clear(theme.Background);
            int length = _horizontal ? Width : Height;
            int thickness = _horizontal ? Height : Width;
            if (length <= 0 || thickness <= 0)
            {
                return;
            }

            // The part over the image is lighter
            double imageStart = _origin;
            double imageEnd = _origin + _imageLength * _zoom;
            using (var imageBrush = new SolidBrush(theme.Window))
            {
                int a = (int)Math.Max(0, Math.Min(length, imageStart));
                int b = (int)Math.Max(0, Math.Min(length, imageEnd));
                if (b > a)
                {
                    graphics.FillRectangle(imageBrush, _horizontal ? new Rectangle(a, 0, b - a, thickness) : new Rectangle(0, a, thickness, b - a));
                }
            }

            int major = Steps[Steps.Length - 1];
            foreach (int step in Steps)
            {
                if (step * _zoom >= LogicalToDeviceUnits(64))
                {
                    major = step;
                    break;
                }
            }

            int divisions = major >= 10 ? 10 : major >= 5 ? 5 : major;
            int minor = Math.Max(1, major / divisions);
            double first = -_origin / _zoom;
            double last = (length - _origin) / _zoom;
            long start = (long)Math.Floor(first / minor) * minor;
            using var tickPen = new Pen(theme.Muted);
            using var labelBrush = new SolidBrush(theme.Foreground);
            using var smallFont = new Font(Font.FontFamily, Math.Max(6.5f, Font.Size - 1.5f));
            for (long value = start; value <= last + minor; value += minor)
            {
                double position = _origin + value * _zoom;
                if (position < -1 || position > length + 1)
                {
                    continue;
                }

                // Very close ticks are left out
                if (minor * _zoom < 3)
                {
                    continue;
                }

                int p = (int)Math.Round(position);
                bool isMajor = value % major == 0;
                bool isHalf = !isMajor && major % 2 == 0 && value % (major / 2) == 0;
                int tick = isMajor ? thickness : isHalf ? thickness * 2 / 3 : thickness / 3;
                if (_horizontal)
                {
                    graphics.DrawLine(tickPen, p, thickness - tick, p, thickness);
                }
                else
                {
                    graphics.DrawLine(tickPen, thickness - tick, p, thickness, p);
                }

                if (isMajor)
                {
                    string text = value.ToString();
                    if (_horizontal)
                    {
                        graphics.DrawString(text, smallFont, labelBrush, p + 2, 1);
                    }
                    else
                    {
                        var state = graphics.Save();
                        graphics.TranslateTransform(0, p - 2);
                        graphics.RotateTransform(-90);
                        graphics.DrawString(text, smallFont, labelBrush, 0, 1);
                        graphics.Restore(state);
                    }
                }
            }

            using var borderPen = new Pen(theme.Border);
            if (_horizontal)
            {
                graphics.DrawLine(borderPen, 0, Height - 1, Width, Height - 1);
            }
            else
            {
                graphics.DrawLine(borderPen, Width - 1, 0, Width - 1, Height);
            }

            if (_mark >= 0 && _mark < length)
            {
                using var markPen = new Pen(Color.FromArgb(220, 40, 40));
                if (_horizontal)
                {
                    graphics.DrawLine(markPen, _mark, 0, _mark, Height);
                }
                else
                {
                    graphics.DrawLine(markPen, 0, _mark, Width, _mark);
                }
            }
        }
    }
}
