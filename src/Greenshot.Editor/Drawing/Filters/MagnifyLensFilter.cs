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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Dapplo.Windows.Common.Extensions;
using Dapplo.Windows.Common.Structs;
using Greenshot.Base.Core;
using Greenshot.Base.Interfaces.Drawing;
using Greenshot.Editor.Drawing.Fields;

namespace Greenshot.Editor.Drawing.Filters
{
    /// <summary>
    /// 왕샷: the magnifying glass. The middle of the selected area is shown enlarged in a round lens with a white rim,
    /// so a small button or a line of text of a screenshot can be read in a textbook.
    /// </summary>
    [Serializable]
    public class MagnifyLensFilter : AbstractFilter
    {
        private const int BorderThickness = 3;
        private const int MaximumFactor = 8;

        public MagnifyLensFilter(DrawableContainer parent) : base(parent)
        {
            AddField(GetType(), FieldType.MAGNIFICATION_FACTOR, 2);
        }

        protected override void ApplyFilter(Graphics graphics, Bitmap applyBitmap, NativeRect applyRect, RenderMode renderMode)
        {
            if (parent == null)
            {
                return;
            }

            var lens = parent.Bounds.Normalize().Intersect(new NativeRect(0, 0, applyBitmap.Width, applyBitmap.Height));
            if (lens.Width < 4 || lens.Height < 4)
            {
                return;
            }

            int factor = GetFieldValueAsInt(FieldType.MAGNIFICATION_FACTOR);
            factor = Math.Max(2, Math.Min(MaximumFactor, factor));

            // The part in the middle of the lens, enlarged to the lens
            int sourceWidth = Math.Max(1, lens.Width / factor);
            int sourceHeight = Math.Max(1, lens.Height / factor);
            var source = new NativeRect(lens.X + (lens.Width - sourceWidth) / 2, lens.Y + (lens.Height - sourceHeight) / 2, sourceWidth, sourceHeight);
            using Bitmap enlarged = ImageHelper.CloneArea(applyBitmap, source, PixelFormat.Format32bppArgb);
            if (enlarged == null)
            {
                return;
            }

            GraphicsState state = graphics.Save();
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                using (var lensPath = new GraphicsPath())
                {
                    lensPath.AddEllipse(lens);
                    graphics.SetClip(lensPath, CombineMode.Intersect);
                    graphics.DrawImage(enlarged, lens, new Rectangle(0, 0, enlarged.Width, enlarged.Height), GraphicsUnit.Pixel);
                }
            }
            finally
            {
                graphics.Restore(state);
            }

            // The rim of the glass, and a soft dark edge so it stands out on a light screenshot
            using var rim = new Pen(Color.White, BorderThickness);
            using var edge = new Pen(Color.FromArgb(60, 0, 0, 0), BorderThickness + 2);
            var rimRect = new NativeRect(lens.X, lens.Y, Math.Max(1, lens.Width - BorderThickness), Math.Max(1, lens.Height - BorderThickness));
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.DrawEllipse(edge, new NativeRect(rimRect.X, rimRect.Y, rimRect.Width + BorderThickness, rimRect.Height + BorderThickness));
            graphics.DrawEllipse(rim, rimRect);
        }
    }
}
