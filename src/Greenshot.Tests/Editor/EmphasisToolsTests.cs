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

using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Greenshot.Editor.Drawing;
using Greenshot.Editor.Drawing.Fields;
using Xunit;

namespace Greenshot.Tests.Editor
{
    /// <summary>
    /// 왕샷: the emphasis tools of the textbook editor, the spotlight, the magnifying glass and the click mark
    /// </summary>
    public class EmphasisToolsTests
    {
        public EmphasisToolsTests()
        {
            TestEnvironment.EnsureInitialized();
        }

        private static Bitmap CreateImage(int width, int height, Color color)
        {
            var image = new Bitmap(width, height);
            image.SetResolution(96, 96);
            using var graphics = Graphics.FromImage(image);
            graphics.Clear(color);
            return image;
        }

        /// <summary>
        /// Paint the surface like the editor does, and give back the picture
        /// </summary>
        private static Bitmap Paint(Surface surface)
        {
            var target = new Bitmap(surface.Width, surface.Height);
            using var graphics = Graphics.FromImage(target);
            var paint = typeof(Surface).GetMethod("SurfacePaint", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(paint);
            paint.Invoke(surface, new object[] { surface, new PaintEventArgs(graphics, new Rectangle(0, 0, surface.Width, surface.Height)) });
            return target;
        }

        [Fact]
        public void Spotlight_DarkensEverythingButTheSelectedArea()
        {
            using var surface = new Surface(CreateImage(400, 200, Color.White));
            var spotlight = new HighlightContainer(surface) { Left = 100, Top = 50, Width = 100, Height = 50 };
            spotlight.SetFieldValue(FieldType.PREPARED_FILTER_HIGHLIGHT, FilterContainer.PreparedFilter.SPOTLIGHT);
            surface.AddElement(spotlight, false, false);

            using var painted = Paint(surface);

            // The selected area is untouched, the rest is covered with the translucent black
            Assert.Equal(Color.White.ToArgb(), painted.GetPixel(150, 75).ToArgb());
            Color outside = painted.GetPixel(20, 20);
            Assert.True(outside.R < 140, $"The area outside the spotlight should be dark, but it is {outside}");
            Assert.True(painted.GetPixel(390, 190).R < 140, "The whole image outside the spotlight should be dark");
        }

        [Fact]
        public void MagnifyLens_ShowsTheMiddleOfTheLensEnlarged()
        {
            using var image = CreateImage(400, 200, Color.White);
            // A black square of 8 x 8 in the middle of the lens: with a factor of 2 it is 16 x 16 in the lens
            using (var graphics = Graphics.FromImage(image))
            {
                graphics.FillRectangle(Brushes.Black, 146, 96, 8, 8);
            }

            using var surface = new Surface(image);
            var lens = new HighlightContainer(surface) { Left = 100, Top = 50, Width = 100, Height = 100 };
            lens.SetFieldValue(FieldType.PREPARED_FILTER_HIGHLIGHT, FilterContainer.PreparedFilter.MAGNIFY_LENS);
            lens.SetFieldValue(FieldType.MAGNIFICATION_FACTOR, 2);
            surface.AddElement(lens, false, false);

            using var painted = Paint(surface);

            // 6 pixels from the middle the enlarged square is there, but the screenshot has nothing at that point
            Assert.True(painted.GetPixel(156, 106).R < 80, "The middle of the lens should be enlarged");
            Assert.Equal(Color.White.ToArgb(), painted.GetPixel(170, 108).ToArgb());
            // A corner of the lens is outside of the glass: the screenshot stays as it is
            Assert.Equal(Color.White.ToArgb(), painted.GetPixel(103, 53).ToArgb());
        }

        [Fact]
        public void ClickMark_DrawsTheDotInTheMiddleAndA_RingAroundIt()
        {
            using var surface = new Surface(CreateImage(200, 200, Color.White));
            var mark = new ClickMarkContainer(surface) { Left = 60, Top = 60, Width = 64, Height = 64 };
            surface.AddElement(mark, false, false);

            using var painted = Paint(surface);

            Color middle = painted.GetPixel(92, 92);
            Assert.True(middle.R > 200 && middle.G < 100, $"The middle of the click mark should be the red dot, but it is {middle}");
            // The ring around the dot, and the white between the dot and the ring
            Color ring = painted.GetPixel(92, 70);
            Assert.True(ring.G < 200, $"There should be a ring around the dot, but the pixel there is {ring}");
            Assert.True(painted.GetPixel(92, 76).G > 230, "Between the dot and the ring the screenshot stays visible");
            Assert.Equal(Color.White.ToArgb(), painted.GetPixel(10, 10).ToArgb());
        }
    }
}
