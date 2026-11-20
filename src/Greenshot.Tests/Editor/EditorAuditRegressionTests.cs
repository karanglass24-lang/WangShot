using System.Drawing;
using System.Linq;
using Greenshot.Base.Interfaces.Drawing;
using Greenshot.Editor.Drawing;
using Greenshot.Editor.Drawing.Fields;
using Xunit;

namespace Greenshot.Tests.Editor
{
    /// <summary>
    /// 왕캡: regressions of the object commands of the ribbon (flip, format painter, group) found in the editor audit
    /// </summary>
    [Collection(TestCollections.InputState)]
    public class EditorAuditRegressionTests
    {
        public EditorAuditRegressionTests()
        {
            TestEnvironment.EnsureInitialized();
        }

        private static RectangleContainer AddRect(Surface surface, int left, int top, Color fill)
        {
            var rect = new RectangleContainer(surface) { Left = left, Top = top, Width = 40, Height = 30 };
            rect.SetFieldValue(FieldType.FILL_COLOR, fill);
            rect.SetFieldValue(FieldType.LINE_THICKNESS, 0);
            rect.SetFieldValue(FieldType.SHADOW, false);
            surface.AddElement(rect, false, false);
            return rect;
        }

        private static Color PixelOfExport(Surface surface, int x, int y)
        {
            using var image = (Bitmap)surface.GetImageForExport();
            return image.GetPixel(x, y);
        }

        [Fact]
        public void Flip_MirrorsTheElementInPlace()
        {
            using var surface = new Surface(new Bitmap(200, 200));
            var line = new LineContainer(surface) { Left = 10, Top = 20, Width = 40, Height = 30 };
            surface.AddElement(line, false, false);
            surface.SelectElement(line, false, false);

            surface.FlipSelected(true);

            // The line now goes from the top right to the bottom left of the same box
            Assert.Equal(new Rectangle(10, 20, 40, 30), (Rectangle)line.Bounds);
            Assert.Equal(50, line.Left);
            Assert.Equal(-40, line.Width);

            surface.FlipSelected(false);
            Assert.Equal(new Rectangle(10, 20, 40, 30), (Rectangle)line.Bounds);
            Assert.Equal(50, line.Top);
            Assert.Equal(-30, line.Height);
        }

        [Fact]
        public void Flip_CanBeUndone()
        {
            using var surface = new Surface(new Bitmap(200, 200));
            var line = new LineContainer(surface) { Left = 10, Top = 20, Width = 40, Height = 30 };
            surface.AddElement(line, false, false);
            surface.SelectElement(line, false, false);

            surface.FlipSelected(true);
            Assert.True(surface.CanUndo);
            surface.Undo();

            Assert.Equal(10, line.Left);
            Assert.Equal(40, line.Width);
        }

        [Fact]
        public void PasteFormat_FromTextToText_KeepsTheAlignmentType()
        {
            using var surface = new Surface(new Bitmap(200, 200));
            var source = new TextContainer(surface) { Left = 10, Top = 10, Width = 80, Height = 30, Text = "a" };
            source.SetFieldValue(FieldType.TEXT_HORIZONTAL_ALIGNMENT, StringAlignment.Far);
            var target = new TextContainer(surface) { Left = 10, Top = 100, Width = 80, Height = 30, Text = "b" };
            surface.AddElement(source, false, false);
            surface.AddElement(target, false, false);

            surface.SelectElement(source, false, false);
            surface.CopyFormat();
            surface.DeselectAllElements();
            surface.SelectElement(target, false, false);
            surface.PasteFormat();

            Assert.Equal(StringAlignment.Far, target.GetFieldValue(FieldType.TEXT_HORIZONTAL_ALIGNMENT));
            // Drawing the text casts the field to StringAlignment
            using var image = surface.GetImageForExport();
        }

        [Fact]
        public void PasteFormat_CanBeUndone()
        {
            using var surface = new Surface(new Bitmap(200, 200));
            var source = AddRect(surface, 10, 10, Color.Red);
            var target = AddRect(surface, 100, 10, Color.Blue);

            surface.SelectElement(source, false, false);
            surface.CopyFormat();
            surface.DeselectAllElements();
            surface.SelectElement(target, false, false);
            surface.PasteFormat();
            Assert.Equal(Color.Red.ToArgb(), target.GetFieldValueAsColor(FieldType.FILL_COLOR).ToArgb());

            Assert.True(surface.CanUndo);
            surface.Undo();
            Assert.Equal(Color.Blue.ToArgb(), target.GetFieldValueAsColor(FieldType.FILL_COLOR).ToArgb());
        }

        [Fact]
        public void Group_StillDrawsItsChildren()
        {
            using var surface = new Surface(new Bitmap(200, 200));
            AddRect(surface, 50, 50, Color.Red);
            AddRect(surface, 120, 50, Color.Blue);
            Assert.True(Color.Red.ToArgb() == PixelOfExport(surface, 70, 65).ToArgb(), "The rectangle is drawn before it is grouped");

            foreach (var element in surface.Elements.ToList())
            {
                surface.SelectElement(element, false, false);
            }

            surface.GroupSelected();

            Assert.Equal(Color.Red.ToArgb(), PixelOfExport(surface, 70, 65).ToArgb());
            Assert.Equal(Color.Blue.ToArgb(), PixelOfExport(surface, 140, 65).ToArgb());
        }

        [Fact]
        public void Group_IsOneUndoStep_AndRestoresThePositions()
        {
            using var surface = new Surface(new Bitmap(200, 200));
            var red = AddRect(surface, 50, 50, Color.Red);
            var blue = AddRect(surface, 120, 60, Color.Blue);
            surface.SelectElement(red, false, false);
            surface.SelectElement(blue, false, false);

            surface.GroupSelected();
            Assert.IsType<GroupContainer>(Assert.Single(surface.Elements));

            surface.Undo();
            Assert.False(surface.CanUndo);
            Assert.Equal(2, surface.Elements.Count);
            Assert.DoesNotContain(surface.Elements, element => element is GroupContainer);
            Assert.Equal(50, red.Left);
            Assert.Equal(50, red.Top);
            Assert.Equal(120, blue.Left);
            Assert.Equal(60, blue.Top);

            surface.Redo();
            var group = Assert.IsType<GroupContainer>(Assert.Single(surface.Elements));
            Assert.Equal(2, group.Children.Count);
            Assert.Equal(new Rectangle(50, 50, 110, 40), (Rectangle)group.Bounds);
        }

        [Fact]
        public void Ungroup_IsOneUndoStep_AndRestoresTheGroup()
        {
            using var surface = new Surface(new Bitmap(200, 200));
            var red = AddRect(surface, 50, 50, Color.Red);
            var blue = AddRect(surface, 120, 60, Color.Blue);
            surface.SelectElement(red, false, false);
            surface.SelectElement(blue, false, false);
            surface.GroupSelected();

            surface.UngroupSelected();
            Assert.Equal(2, surface.Elements.Count);
            Assert.Equal(50, red.Left);
            Assert.Equal(120, blue.Left);

            surface.Undo();
            var group = Assert.IsType<GroupContainer>(Assert.Single(surface.Elements));
            Assert.Equal(2, group.Children.Count);
            Assert.Equal(new Rectangle(50, 50, 110, 40), (Rectangle)group.Bounds);
            Assert.Equal(Color.Blue.ToArgb(), PixelOfExport(surface, 140, 75).ToArgb());
        }

        [Fact]
        public void Group_KeepsTheZOrder_AndUndoRestoresIt()
        {
            using var surface = new Surface(new Bitmap(300, 200));
            var below = AddRect(surface, 10, 10, Color.Green);
            var first = AddRect(surface, 60, 50, Color.Red);
            var middle = AddRect(surface, 110, 10, Color.Yellow);
            var second = AddRect(surface, 160, 60, Color.Blue);
            var above = AddRect(surface, 210, 10, Color.Black);
            surface.SelectElement(first, false, false);
            surface.SelectElement(second, false, false);

            surface.GroupSelected();
            // The group takes the place of its topmost child
            Assert.IsType<GroupContainer>(surface.Elements[2]);
            Assert.Same(above, surface.Elements[3]);

            surface.Undo();
            Assert.Equal(new IDrawableContainer[] { below, first, middle, second, above }, surface.Elements.ToArray());

            surface.Redo();
            Assert.IsType<GroupContainer>(surface.Elements[2]);
            surface.Undo();
            Assert.Equal(new IDrawableContainer[] { below, first, middle, second, above }, surface.Elements.ToArray());
        }

        [Fact]
        public void Ungroup_Undo_RestoresTheGroupAtItsPlace()
        {
            using var surface = new Surface(new Bitmap(300, 200));
            var below = AddRect(surface, 10, 10, Color.Green);
            var first = AddRect(surface, 60, 50, Color.Red);
            var second = AddRect(surface, 160, 60, Color.Blue);
            var above = AddRect(surface, 210, 10, Color.Black);
            surface.SelectElement(first, false, false);
            surface.SelectElement(second, false, false);
            surface.GroupSelected();
            var group = surface.Elements[1];

            surface.UngroupSelected();
            Assert.Equal(new IDrawableContainer[] { below, first, second, above }, surface.Elements.ToArray());

            surface.Undo();
            Assert.Equal(new[] { below, group, above }, surface.Elements.ToArray());
        }

        [Fact]
        public void GroupChildren_HaveTheSurfaceAsParent_AlsoAfterUndoRedoAndPaste()
        {
            using var surface = new Surface(new Bitmap(300, 200));
            var red = AddRect(surface, 50, 50, Color.Red);
            var blue = AddRect(surface, 120, 60, Color.Blue);
            surface.SelectElement(red, false, false);
            surface.SelectElement(blue, false, false);

            surface.GroupSelected();
            Assert.Same(surface, red.Parent);
            Assert.Same(surface, blue.Parent);

            // A field change of a child inside the group is undoable (it needs the parent)
            surface.Undo();
            Assert.False(surface.CanUndo);
            surface.Redo();
            red.BeforeFieldChange(red.GetField(FieldType.FILL_COLOR), Color.Green);
            Assert.True(surface.CanUndo, "field change of a child");
            Assert.True(surface.CanUndo);
            surface.Undo();

            var group = (GroupContainer)Assert.Single(surface.Elements);
            Assert.All(group.Children.OfType<IDrawableContainer>(), child => Assert.Same(surface, child.Parent));

            // Pasted: the group comes from the clipboard bytes without parents and events
            var copy = DrawableContainerClipboard.Deserialize(DrawableContainerClipboard.Serialize(new DrawableContainerList(new IDrawableContainer[] { group })));
            var pasted = (GroupContainer)Assert.Single(copy);
            Assert.All(pasted.Children.OfType<IDrawableContainer>(), child => Assert.Null(child.Parent));
            surface.AddElement(pasted, false, false);
            Assert.All(pasted.Children.OfType<IDrawableContainer>(), child => Assert.Same(surface, child.Parent));
        }

        [Fact]
        public void PasteFormat_FromAnObjectWithoutFill_KeepsTheFillOfTheTarget()
        {
            using var surface = new Surface(new Bitmap(200, 200));
            var source = new LineContainer(surface) { Left = 10, Top = 10, Width = 40, Height = 30 };
            source.SetFieldValue(FieldType.LINE_COLOR, Color.Red);
            surface.AddElement(source, false, false);
            var target = AddRect(surface, 100, 10, Color.Blue);
            Assert.False(source.HasField(FieldType.FILL_COLOR));

            surface.SelectElement(source, false, false);
            surface.CopyFormat();
            surface.DeselectAllElements();
            surface.SelectElement(target, false, false);
            surface.PasteFormat();

            Assert.Equal(Color.Blue.ToArgb(), target.GetFieldValueAsColor(FieldType.FILL_COLOR).ToArgb());
            Assert.Equal(Color.Red.ToArgb(), target.GetFieldValueAsColor(FieldType.LINE_COLOR).ToArgb());
        }

        [Fact]
        public void ApplyFormat_UnsetFields_ChangeNothing()
        {
            using var surface = new Surface(new Bitmap(200, 200));
            var rect = AddRect(surface, 10, 10, Color.Blue);
            rect.SetFieldValue(FieldType.SHADOW, true);

            rect.ApplyFormat(new FormatSnapshot());

            Assert.Equal(Color.Blue.ToArgb(), rect.GetFieldValueAsColor(FieldType.FILL_COLOR).ToArgb());
            Assert.True(rect.GetFieldValueAsBool(FieldType.SHADOW));
        }

        [Fact]
        public void Group_WithAFilter_IsRefusedWithAMessage()
        {
            using var surface = new Surface(new Bitmap(200, 200));
            var rect = AddRect(surface, 10, 10, Color.Red);
            var pixelate = new ObfuscateContainer(surface) { Left = 60, Top = 10, Width = 40, Height = 30 };
            surface.AddElement(pixelate, false, false);
            string message = null;
            surface.SurfaceMessage += (sender, args) => message = args.Message;
            surface.SelectElement(rect, false, false);
            surface.SelectElement(pixelate, false, false);

            surface.GroupSelected();

            Assert.Equal(2, surface.Elements.Count);
            Assert.DoesNotContain(surface.Elements, element => element is GroupContainer);
            Assert.False(surface.CanUndo);
            Assert.Contains("효과", message);
        }
    }
}
