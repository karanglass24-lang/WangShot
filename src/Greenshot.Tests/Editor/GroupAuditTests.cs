using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using Greenshot.Base.Interfaces.Drawing;
using Greenshot.Editor.Drawing;
using Greenshot.Editor.Drawing.Fields;
using Xunit;

namespace Greenshot.Tests.Editor
{
    /// <summary>
    /// 왕캡: groups - flip, transform, bounds, validation of pasted groups, step labels, fields (second round of the audit)
    /// </summary>
    [Collection(TestCollections.InputState)]
    public class GroupAuditTests
    {
        public GroupAuditTests()
        {
            TestEnvironment.EnsureInitialized();
        }

        private static RectangleContainer AddRect(Surface surface, int left, int top, Color fill, int width = 40, int height = 30)
        {
            var rect = new RectangleContainer(surface) { Left = left, Top = top, Width = width, Height = height };
            rect.SetFieldValue(FieldType.FILL_COLOR, fill);
            rect.SetFieldValue(FieldType.LINE_THICKNESS, 0);
            rect.SetFieldValue(FieldType.SHADOW, false);
            surface.AddElement(rect, false, false);
            return rect;
        }

        private static GroupContainer GroupOf(Surface surface, params IDrawableContainer[] elements)
        {
            surface.DeselectAllElements();
            foreach (var element in elements)
            {
                surface.SelectElement(element, false, false);
            }

            surface.GroupSelected();
            return (GroupContainer)surface.Elements.Single(element => element is GroupContainer);
        }

        private static byte[] Serialize(params IDrawableContainer[] elements) => DrawableContainerClipboard.Serialize(new DrawableContainerList(elements));

        [Fact]
        public void Flip_OfAGroup_KeepsItsPlace_AndUndoRestoresThePositions()
        {
            using var surface = new Surface(new Bitmap(300, 300));
            var first = AddRect(surface, 50, 50, Color.Red);
            var second = AddRect(surface, 150, 150, Color.Blue);
            var group = GroupOf(surface, first, second);

            surface.FlipSelected(true);
            Assert.Equal(50, group.Left);
            Assert.Equal(50, group.Top);
            Assert.Equal(140, group.Width);

            surface.Undo();
            Assert.Equal(50, group.Left);
            Assert.Equal(50, group.Top);

            surface.Undo();
            Assert.Equal(new[] { (IDrawableContainer)first, second }, surface.Elements.ToArray());
            Assert.Equal((50, 50), (first.Left, first.Top));
            Assert.Equal((150, 150), (second.Left, second.Top));
        }

        [Fact]
        public void Flip_OfAGroup_MirrorsTheChildrenInTheBox()
        {
            using var surface = new Surface(new Bitmap(300, 300));
            var first = AddRect(surface, 50, 50, Color.Red);
            var second = AddRect(surface, 150, 150, Color.Blue);
            var group = GroupOf(surface, first, second);

            group.Flip(true);

            // The first rectangle is on the right side of the group now (local 140, mirrored: negative width), the second on the left
            Assert.Equal((140, -40), (first.Left, first.Width));
            Assert.Equal((40, -40), (second.Left - 0, second.Width));
        }

        [Fact]
        public void Group_SelfContaining_IsRejected_AndDoesNotCrash()
        {
            using var surface = new Surface(new Bitmap(100, 100));
            var group = new GroupContainer(surface);
            group.Children.Add(group);

            Assert.Null(DrawableContainerClipboard.Deserialize(Serialize(group)));

            // The reentrancy guard: no stack overflow when the parent is set
            group.Parent = null;
            group.Parent = surface;
        }

        [Fact]
        public void Group_WithTwoGroupsInACircle_OrTooDeep_OrWithAFilter_IsRejected()
        {
            using var surface = new Surface(new Bitmap(100, 100));
            var a = new GroupContainer(surface);
            var b = new GroupContainer(surface);
            a.Children.Add(b);
            b.Children.Add(a);
            Assert.Null(DrawableContainerClipboard.Deserialize(Serialize(a)));

            var outer = new GroupContainer(surface);
            var current = outer;
            for (int i = 0; i < 12; i++)
            {
                var inner = new GroupContainer(surface);
                current.Children.Add(inner);
                current = inner;
            }

            Assert.Null(DrawableContainerClipboard.Deserialize(Serialize(outer)));

            var withFilter = new GroupContainer(surface);
            withFilter.Children.Add(new ObfuscateContainer(surface));
            Assert.Null(DrawableContainerClipboard.Deserialize(Serialize(withFilter)));
        }

        [Fact]
        public void Group_Valid_IsAccepted_AndOneInvalidElementRejectsTheWholeList()
        {
            using var surface = new Surface(new Bitmap(100, 100));
            var rect = AddRect(surface, 10, 10, Color.Red);
            var other = AddRect(surface, 60, 10, Color.Blue);
            var group = GroupOf(surface, rect, other);
            var plain = new RectangleContainer(surface) { Left = 1, Top = 1, Width = 5, Height = 5 };
            Assert.Equal(2, DrawableContainerClipboard.Deserialize(Serialize(group, plain)).Count);

            var cyclic = new GroupContainer(surface);
            cyclic.Children.Add(cyclic);
            Assert.Null(DrawableContainerClipboard.Deserialize(Serialize(plain, group, cyclic)));
        }

        [Fact]
        public void Group_FromAFileWithACycle_IsNotLoaded()
        {
            using var surface = new Surface(new Bitmap(100, 100));
            var cyclic = new GroupContainer(surface);
            cyclic.Children.Add(cyclic);
            using var stream = new MemoryStream(Serialize(cyclic));

            surface.LoadElementsFromStream(stream);

            Assert.Empty(surface.Elements);
        }

        [Fact]
        public void Grouping_KeepsTheNumbersOfTheStepLabels()
        {
            using var surface = new Surface(new Bitmap(300, 300));
            var labels = new[] { 10, 60, 110 }.Select(left =>
            {
                var label = new StepLabelContainer(surface) { Left = left, Top = 10, Width = 36, Height = 36 };
                surface.AddElement(label, false, false);
                return label;
            }).ToArray();
            Assert.Equal(new[] { "1", "2", "3" }, labels.Select(label => label.NumberText).ToArray());

            GroupOf(surface, labels[0], labels[1]);

            Assert.Equal(new[] { "1", "2", "3" }, labels.Select(label => label.NumberText).ToArray());
        }

        [Fact]
        public void Grouping_SpeechBubble_OrTornEdge_IsRefused()
        {
            using var surface = new Surface(new Bitmap(300, 300));
            var rect = AddRect(surface, 10, 10, Color.Red);
            var bubble = new SpeechbubbleContainer(surface) { Left = 100, Top = 100, Width = 80, Height = 40 };
            bubble.SetTailLocation(150, 150);
            surface.AddElement(bubble, false, false);
            string message = null;
            surface.SurfaceMessage += (sender, args) => message = args.Message;
            surface.SelectElement(rect, false, false);
            surface.SelectElement(bubble, false, false);

            surface.GroupSelected();

            Assert.DoesNotContain(surface.Elements, element => element is GroupContainer);
            Assert.Contains("말풍선", message);

            Assert.False(GroupContainer.CanBeChild(new TornEdgeContainer(surface, new Greenshot.Base.Effects.TornEdgeEffect())));
        }

        [Fact]
        public void DrawingBounds_ContainTheShadowAndTheLineOfTheChildren_InSurfaceCoordinates()
        {
            using var surface = new Surface(new Bitmap(300, 300));
            var first = AddRect(surface, 50, 50, Color.Red);
            first.SetFieldValue(FieldType.LINE_THICKNESS, 20);
            first.SetFieldValue(FieldType.SHADOW, true);
            var second = AddRect(surface, 150, 150, Color.Blue);
            var expected = first.DrawingBounds;

            var group = GroupOf(surface, first, second);

            Assert.Equal(expected.Left, group.DrawingBounds.Left);
            Assert.Equal(expected.Top, group.DrawingBounds.Top);
            Assert.Equal(38, group.DrawingBounds.Left);
        }

        [Fact]
        public void Transform_ScalesTheChildrenInTheGroup()
        {
            using var surface = new Surface(new Bitmap(600, 600));
            var first = AddRect(surface, 100, 100, Color.Red);
            var second = AddRect(surface, 340, 330, Color.Blue);
            var group = GroupOf(surface, first, second);
            Assert.Equal(new Rectangle(100, 100, 280, 260), (Rectangle)group.Bounds);

            using var matrix = new Matrix();
            matrix.Scale(2, 2);
            group.Transform(matrix);

            Assert.Equal(new Rectangle(200, 200, 560, 520), (Rectangle)group.Bounds);
            Assert.Equal(new Rectangle(0, 0, 80, 60), (Rectangle)first.Bounds);
            Assert.Equal(new Rectangle(480, 460, 80, 60), (Rectangle)second.Bounds);
        }

        [Fact]
        public void Contains_UsesTheCoordinatesRelativeToTheGroup()
        {
            using var surface = new Surface(new Bitmap(300, 300));
            var first = AddRect(surface, 50, 50, Color.Red);
            var second = AddRect(surface, 150, 150, Color.Blue);
            var group = GroupOf(surface, first, second);

            Assert.True(group.Contains(60, 60));
            Assert.True(group.Contains(160, 160));
            Assert.False(group.Contains(100, 100));
        }

        [Fact]
        public void FormatPaste_OnNestedGroups_IsUndoneForAllChildren()
        {
            using var surface = new Surface(new Bitmap(400, 300));
            var a = AddRect(surface, 10, 10, Color.Red);
            var b = AddRect(surface, 70, 10, Color.Blue);
            var c = AddRect(surface, 130, 10, Color.Yellow);
            var source = AddRect(surface, 200, 100, Color.Green);
            var inner = GroupOf(surface, a, b);
            surface.DeselectAllElements();
            surface.SelectElement(inner, false, false);
            surface.SelectElement(c, false, false);
            surface.GroupSelected();
            surface.DeselectAllElements();
            surface.SelectElement(source, false, false);
            surface.CopyFormat();
            surface.DeselectAllElements();
            surface.SelectElement(surface.Elements.OfType<GroupContainer>().Single(), false, false);

            surface.PasteFormat();
            Assert.Equal(Color.Green.ToArgb(), a.GetFieldValueAsColor(FieldType.FILL_COLOR).ToArgb());
            Assert.Equal(Color.Green.ToArgb(), c.GetFieldValueAsColor(FieldType.FILL_COLOR).ToArgb());

            surface.Undo();
            Assert.Equal(Color.Red.ToArgb(), a.GetFieldValueAsColor(FieldType.FILL_COLOR).ToArgb());
            Assert.Equal(Color.Blue.ToArgb(), b.GetFieldValueAsColor(FieldType.FILL_COLOR).ToArgb());
            Assert.Equal(Color.Yellow.ToArgb(), c.GetFieldValueAsColor(FieldType.FILL_COLOR).ToArgb());
        }

        [Fact]
        public void Group_WithFreehand_HasTheBoundsOfTheLine_NotOfTheImage()
        {
            using var surface = new Surface(new Bitmap(400, 400));
            var rect = AddRect(surface, 100, 100, Color.Red);
            var freehand = new FreehandContainer(surface);
            freehand.HandleMouseDown(120, 120);
            freehand.HandleMouseMove(160, 150);
            freehand.HandleMouseMove(200, 190);
            freehand.HandleMouseUp(200, 190);
            surface.AddElement(freehand, false, false);

            var group = GroupOf(surface, rect, freehand);

            Assert.True(group.Width < 200, "width " + group.Width);
            Assert.True(group.Height < 200, "height " + group.Height);
            Assert.Equal(100, group.Left);
        }

        [Fact]
        public void Dispose_OfAGroup_DisposesItsChildren_ButNotAfterTheUngroup()
        {
            using var surface = new Surface(new Bitmap(300, 300));
            var imageA = new ImageContainer(surface) { Left = 10, Top = 10, Width = 20, Height = 20 };
            imageA.Image = new Bitmap(20, 20);
            var imageB = new ImageContainer(surface) { Left = 100, Top = 100, Width = 20, Height = 20 };
            imageB.Image = new Bitmap(20, 20);
            surface.AddElement(imageA, false, false);
            surface.AddElement(imageB, false, false);
            var group = GroupOf(surface, imageA, imageB);
            var bitmapA = imageA.Image;
            var bitmapB = imageB.Image;

            surface.UngroupSelected();
            group.Dispose();
            Assert.Equal(20, bitmapA.Width);

            var regrouped = GroupOf(surface, imageA, imageB);
            regrouped.Dispose();
            Assert.ThrowsAny<Exception>(() => bitmapB.Width);
        }

        [Fact]
        public void FieldChange_OfASelectedGroup_ReachesAllChildren()
        {
            using var surface = new Surface(new Bitmap(300, 300));
            var first = AddRect(surface, 50, 50, Color.Red);
            var second = AddRect(surface, 150, 150, Color.Blue);
            var group = GroupOf(surface, first, second);
            var list = new DrawableContainerList();
            list.Add(group);
            surface.FieldAggregator.BindElements(list);

            surface.FieldAggregator.GetField(FieldType.FILL_COLOR).Value = Color.Green;

            Assert.Equal(Color.Green.ToArgb(), first.GetFieldValueAsColor(FieldType.FILL_COLOR).ToArgb());
            Assert.Equal(Color.Green.ToArgb(), second.GetFieldValueAsColor(FieldType.FILL_COLOR).ToArgb());
        }
    }
}
