/*
 * 왕샷 (WangShot) - based on Greenshot, GPL-3.0-or-later
 */

using System.Drawing;
using System.Windows.Forms;
using Greenshot.Base.Interfaces.Drawing;

namespace Greenshot.Editor.Drawing.Adorners
{
    /// <summary>
    /// The handle of the crop rectangle like in PicPick/PowerPoint: a black corner bracket or edge bar, exactly on the border of the crop area.
    /// </summary>
    public class CropAdorner : ResizeAdorner
    {
        public CropAdorner(IDrawableContainer owner, Positions position) : base(owner, position)
        {
            // A bigger grab area than the 6 pixels of the plain handles
            Size = new Dapplo.Windows.Common.Structs.NativeSize(Size.Width * 3, Size.Height * 3);
        }

        public override void Paint(PaintEventArgs paintEventArgs)
        {
            var g = paintEventArgs.Graphics;
            var b = BoundsOnSurface;
            int cx = b.Left + b.Width / 2;
            int cy = b.Top + b.Height / 2;
            // Thickness and length of the bracket, relative to the handle size
            int t = System.Math.Max(3, b.Width / 4);
            int a = System.Math.Max(10, b.Width);
            int sx = Position == Positions.TopLeft || Position == Positions.BottomLeft || Position == Positions.MiddleLeft ? 1 : -1;
            int sy = Position == Positions.TopLeft || Position == Positions.TopRight || Position == Positions.TopCenter ? 1 : -1;

            Rectangle R(int x1, int y1, int x2, int y2) => Rectangle.FromLTRB(System.Math.Min(x1, x2), System.Math.Min(y1, y2), System.Math.Max(x1, x2), System.Math.Max(y1, y2));

            Rectangle[] parts;
            switch (Position)
            {
                case Positions.TopCenter:
                case Positions.BottomCenter:
                    parts = new[] { R(cx - a, cy, cx + a, cy + sy * t) };
                    break;
                case Positions.MiddleLeft:
                case Positions.MiddleRight:
                    parts = new[] { R(cx, cy - a, cx + sx * t, cy + a) };
                    break;
                default:
                    parts = new[] { R(cx, cy, cx + sx * a, cy + sy * t), R(cx, cy, cx + sx * t, cy + sy * a) };
                    break;
            }

            var state = g.Save();
            try
            {
                using var fill = new SolidBrush(Color.Black);
                using var outline = new Pen(Color.White);
                foreach (var part in parts)
                {
                    g.FillRectangle(fill, part);
                    g.DrawRectangle(outline, part.X, part.Y, part.Width - 1, part.Height - 1);
                }
            }
            finally
            {
                g.Restore(state);
            }
        }
    }
}
