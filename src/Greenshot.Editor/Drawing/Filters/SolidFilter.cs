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
using Dapplo.Windows.Common.Structs;
using Greenshot.Base.Interfaces.Drawing;
using Greenshot.Editor.Drawing.Fields;

namespace Greenshot.Editor.Drawing.Filters
{
    /// <summary>
    /// 왕샷: covers an area with one colour. A solid block over a name or an account number looks tidier in a textbook than
    /// a mosaic, and nothing of the text behind it can show through.
    /// </summary>
    [Serializable]
    public class SolidFilter : AbstractFilter
    {
        public SolidFilter(DrawableContainer parent) : base(parent)
        {
            AddField(GetType(), FieldType.FILL_COLOR, Color.Black);
        }

        protected override void ApplyFilter(Graphics graphics, Bitmap applyBitmap, NativeRect applyRect, RenderMode renderMode)
        {
            Color color = GetFieldValueAsColor(FieldType.FILL_COLOR);
            if (color.A == 0)
            {
                return;
            }

            using var brush = new SolidBrush(color);
            graphics.FillRectangle(brush, applyRect);
        }
    }
}
