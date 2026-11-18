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
using System.Linq;
using System.Windows.Forms;
using Greenshot.Base.Core;
using Greenshot.Editor.Drawing;
using Greenshot.Editor.Drawing.Fields;
using Xunit;

namespace Greenshot.Tests.Editor
{
    /// <summary>
    /// 왕샷: dragging elements, the guide lines it snaps to and the copy an Alt+drag makes
    /// </summary>
    [Collection(TestCollections.InputState)]
    public class SurfaceDragTests
    {
        public SurfaceDragTests()
        {
            TestEnvironment.EnsureInitialized();
        }

        private static RectangleContainer AddRectangle(Surface surface, int left, int top)
        {
            var rectangle = new RectangleContainer(surface) { Left = left, Top = top, Width = 40, Height = 30 };
            // Filled, so the middle of it can be clicked
            rectangle.SetFieldValue(FieldType.FILL_COLOR, Color.LightBlue);
            surface.AddElement(rectangle, false, false);
            return rectangle;
        }

        private static void Drag(Surface surface, int fromX, int fromY, int toX, int toY)
        {
            surface.ForwardMouseDown(new MouseEventArgs(MouseButtons.Left, 1, fromX, fromY, 0));
            surface.ForwardMouseMove(new MouseEventArgs(MouseButtons.Left, 0, toX, toY, 0));
            surface.ForwardMouseUp(new MouseEventArgs(MouseButtons.Left, 1, toX, toY, 0));
        }

        [Fact]
        public void Drag_SnapsToTheRightEdgeOfAnotherElement()
        {
            using var surface = new Surface(new Bitmap(400, 300));
            AddRectangle(surface, 50, 50);
            var dragged = AddRectangle(surface, 200, 50);
            surface.SelectElement(dragged, false, false);

            // 5 pixels short of the right edge of the first rectangle: it snaps to it
            Drag(surface, 210, 60, 105, 60);

            Assert.Equal(90, dragged.Left);
        }

        [Fact]
        public void Drag_KeepsThePositionWhenNoGuideIsClose()
        {
            using var surface = new Surface(new Bitmap(400, 300));
            AddRectangle(surface, 50, 50);
            var dragged = AddRectangle(surface, 200, 120);
            surface.SelectElement(dragged, false, false);

            Drag(surface, 210, 130, 160, 145);

            Assert.Equal(150, dragged.Left);
            Assert.Equal(135, dragged.Top);
        }

        [Fact]
        public void AltDrag_CopiesTheSelectionAndMovesTheCopy()
        {
            InputState.ModifierKeysSource = () => Keys.Alt;
            try
            {
                using var surface = new Surface(new Bitmap(400, 300));
                var rectangle = AddRectangle(surface, 50, 50);
                surface.SelectElement(rectangle, false, false);

                Drag(surface, 60, 60, 160, 160);

                Assert.Equal(2, surface.Elements.Count);
                // The original stays, the copy is the one at the mouse
                Assert.Equal(50, rectangle.Left);
                var copy = Assert.Single(surface.Elements.OfType<RectangleContainer>(), element => !ReferenceEquals(element, rectangle));
                Assert.Equal(150, copy.Left);
            }
            finally
            {
                InputState.ModifierKeysSource = () => Keys.None;
            }
        }

        [Fact]
        public void AltClick_WithoutAMove_CopiesNothing()
        {
            InputState.ModifierKeysSource = () => Keys.Alt;
            try
            {
                using var surface = new Surface(new Bitmap(400, 300));
                var rectangle = AddRectangle(surface, 50, 50);
                surface.SelectElement(rectangle, false, false);

                Drag(surface, 60, 60, 60, 60);

                Assert.Single(surface.Elements.OfType<RectangleContainer>());
            }
            finally
            {
                InputState.ModifierKeysSource = () => Keys.None;
            }
        }
    }
}
