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
using Dapplo.Windows.Common.Extensions;
using Dapplo.Windows.Common.Structs;
using Greenshot.Base.Interfaces.Drawing;
using Greenshot.Editor.Drawing.Fields;
using Greenshot.Editor.Helpers;

namespace Greenshot.Editor.Drawing.Filters
{
    /// <summary>
    /// 왕샷: the spotlight. Everything but the selected area is covered with a translucent colour, the area itself stays
    /// as it is: "look here". The corners of the hole are rounded a little, like a callout.
    /// </summary>
    [Serializable]
    public class SpotlightFilter : AbstractFilter
    {
        /// <summary>
        /// How round the corners of the hole are, at most
        /// </summary>
        private const float CornerRadius = 12f;

        public SpotlightFilter(DrawableContainer parent) : base(parent)
        {
            // A soft black: a screenshot under it is dark but still visible
            AddField(GetType(), FieldType.FILL_COLOR, Color.FromArgb(150, 0, 0, 0));
        }

        /// <summary>
        /// The whole image is covered, the selected area is cut out of it
        /// </summary>
        public override void Apply(Graphics graphics, Bitmap applyBitmap, NativeRect rect, RenderMode renderMode)
        {
            if (graphics == null || applyBitmap == null)
            {
                return;
            }

            var bounds = new NativeRect(0, 0, applyBitmap.Width, applyBitmap.Height);
            var hole = rect.Normalize().Intersect(bounds);
            Color dimColor = GetFieldValueAsColor(FieldType.FILL_COLOR);
            if (!Colors.IsVisible(dimColor))
            {
                return;
            }

            GraphicsState state = graphics.Save();
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                // The area behind the hole is dimmed, only the part which is repainted (the graphics is clipped already)
                using var path = new GraphicsPath(FillMode.Alternate);
                path.AddRectangle(bounds);
                if (hole.Width > 0 && hole.Height > 0)
                {
                    AddRoundedRectangle(path, hole, Math.Min(CornerRadius, Math.Min(hole.Width, hole.Height) / 4f));
                }

                using var brush = new SolidBrush(dimColor);
                graphics.FillPath(brush, path);
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private static void AddRoundedRectangle(GraphicsPath path, NativeRect rect, float radius)
        {
            if (radius <= 0.5f)
            {
                path.AddRectangle(rect);
                return;
            }

            float diameter = radius * 2;
            var arc = new RectangleF(rect.X, rect.Y, diameter, diameter);
            // Top left
            path.AddArc(arc, 180, 90);
            // Top right
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);
            // Bottom right
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            // Bottom left
            arc.X = rect.X;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
        }
    }
}
