using System.Drawing;
using System.Drawing.Drawing2D;
using Greenshot.Base.Interfaces.Drawing;
using Greenshot.Editor.Drawing;

namespace Greenshot.Editor.Memento
{
    /// <summary>Restores both the pixels and the element positions of a smart move.</summary>
    internal sealed class SmartMoveMemento : IMemento
    {
        private readonly Surface _surface;
        private readonly Rectangle _area;
        private readonly Bitmap _pixels;
        private readonly IMemento _bounds;

        internal SmartMoveMemento(Surface surface, Rectangle area, IMemento bounds)
        {
            _surface = surface;
            _area = Rectangle.Intersect(area, new Rectangle(Point.Empty, surface.Image.Size));
            _pixels = new Bitmap(_area.Width, _area.Height);
            using var g = Graphics.FromImage(_pixels);
            g.CompositingMode = CompositingMode.SourceCopy;
            g.DrawImage(surface.Image, new Rectangle(Point.Empty, _area.Size), _area, GraphicsUnit.Pixel);
            _bounds = bounds;
        }

        public IMemento Restore()
        {
            var inverse = new SmartMoveMemento(_surface, _area, _bounds.Restore());
            using (var g = Graphics.FromImage(_surface.Image))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImage(_pixels, _area, new Rectangle(Point.Empty, _area.Size), GraphicsUnit.Pixel);
            }
            _surface.Invalidate();
            return inverse;
        }

        public bool Merge(IMemento otherMemento) => false;

        public void Dispose()
        {
            _pixels.Dispose();
            _bounds.Dispose();
        }
    }
}
