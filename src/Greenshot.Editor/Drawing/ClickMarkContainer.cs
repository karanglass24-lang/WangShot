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
using System.Runtime.Serialization;
using Dapplo.Windows.Common.Extensions;
using Dapplo.Windows.Common.Structs;
using Greenshot.Base.Interfaces;
using Greenshot.Base.Interfaces.Drawing;
using Greenshot.Editor.Drawing.Fields;

namespace Greenshot.Editor.Drawing
{
    /// <summary>
    /// 왕샷: the click mark. A filled dot with a ring around it, drawn where the user of the textbook has to click.
    /// It is placed with a click (or dragged to its size) and can be moved and scaled like any other element.
    /// </summary>
    [Serializable]
    public class ClickMarkContainer : DrawableContainer
    {
        public ClickMarkContainer(ISurface parent) : base(parent)
        {
            Init();
        }

        protected override void OnDeserialized(StreamingContext streamingContext)
        {
            base.OnDeserialized(streamingContext);
            Init();
        }

        private void Init()
        {
            CreateDefaultAdorners();
        }

        public override NativeSize DefaultSize => new NativeSize(64, 64);

        public override bool HasDefaultSize => true;

        protected override void InitializeFields()
        {
            AddField(GetType(), FieldType.LINE_COLOR, Color.FromArgb(0xE5, 0x39, 0x35));
            AddField(GetType(), FieldType.LINE_THICKNESS, 0);
            AddField(GetType(), FieldType.FILL_COLOR, Color.Transparent);
            AddField(GetType(), FieldType.SHADOW, false);
        }

        public override void Draw(Graphics graphics, RenderMode renderMode)
        {
            var rect = new NativeRect(Left, Top, Width, Height).Normalize();
            float radius = Math.Min(rect.Width, rect.Height) / 2f;
            if (radius < 1f)
            {
                return;
            }

            Color color = GetFieldValueAsColor(FieldType.LINE_COLOR);
            var center = new PointF(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);

            GraphicsState state = graphics.Save();
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                // The outer halo, then a firmer ring and the dot in the middle: a click which radiates
                DrawRing(graphics, center, radius * 0.92f, WithAlpha(color, 70), Math.Max(1.5f, radius * 0.12f));
                DrawRing(graphics, center, radius * 0.68f, WithAlpha(color, 150), Math.Max(1.5f, radius * 0.12f));

                float dotRadius = radius * 0.40f;
                using var dotBrush = new SolidBrush(WithAlpha(color, 235));
                graphics.FillEllipse(dotBrush, center.X - dotRadius, center.Y - dotRadius, dotRadius * 2, dotRadius * 2);
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private static void DrawRing(Graphics graphics, PointF center, float radius, Color color, float thickness)
        {
            using var pen = new Pen(color, thickness);
            float diameter = radius * 2 - thickness;
            if (diameter <= 0)
            {
                return;
            }

            graphics.DrawEllipse(pen, center.X - diameter / 2, center.Y - diameter / 2, diameter, diameter);
        }

        private static Color WithAlpha(Color color, int alpha) => Color.FromArgb(Math.Min(alpha, color.A), color.R, color.G, color.B);

        public override bool Contains(int x, int y) => EllipseContainer.EllipseContains(this, x, y);

        public override bool ClickableAt(int x, int y)
        {
            var rect = new NativeRect(Left, Top, Width, Height).Normalize();
            return EllipseContainer.EllipseClickableAt(rect, 10, Color.Red, x, y);
        }

        /// <summary>
        /// The mark is placed with its middle under the mouse
        /// </summary>
        public override bool HandleMouseDown(int mouseX, int mouseY)
        {
            return base.HandleMouseDown(mouseX - Width / 2, mouseY - Height / 2);
        }

        /// <summary>
        /// A click gives a mark of the default size, so dragging only moves it
        /// </summary>
        public override bool HandleMouseMove(int x, int y)
        {
            Invalidate();
            Left = x - Width / 2;
            Top = y - Height / 2;
            Invalidate();
            return true;
        }
    }
}
