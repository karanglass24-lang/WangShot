using System;
using System.Drawing;
using System.IO;
using System.Linq;
using Greenshot.Base.Interfaces.Drawing;
using Greenshot.Capturing;
using Greenshot.Editor.Drawing;
using Greenshot.Editor.Drawing.Fields;
using Xunit;

namespace Greenshot.Tests.Editor
{
    /// <summary>
    /// 왕캡: third round of the audit: group flip with freehand, delete/undo of groups, depth of groups, view matching of the scroll capture
    /// </summary>
    [Collection(TestCollections.InputState)]
    public class AuditRoundThreeTests
    {
        public AuditRoundThreeTests()
        {
            TestEnvironment.EnsureInitialized();
        }

        private static RectangleContainer AddRect(Surface surface, int left, int top)
        {
            var rect = new RectangleContainer(surface) { Left = left, Top = top, Width = 40, Height = 30 };
            rect.SetFieldValue(FieldType.FILL_COLOR, Color.Red);
            surface.AddElement(rect, false, false);
            return rect;
        }

        private static FreehandContainer AddFreehand(Surface surface)
        {
            var freehand = new FreehandContainer(surface);
            freehand.HandleMouseDown(120, 120);
            freehand.HandleMouseMove(160, 150);
            freehand.HandleMouseMove(200, 190);
            freehand.HandleMouseUp(200, 190);
            surface.AddElement(freehand, false, false);
            return freehand;
        }

        private static GroupContainer GroupOf(Surface surface, params IDrawableContainer[] elements)
        {
            surface.DeselectAllElements();
            foreach (var element in elements)
            {
                surface.SelectElement(element, false, false);
            }

            surface.GroupSelected();
            return (GroupContainer)surface.Elements.OfType<GroupContainer>().Last();
        }

        [Fact]
        public void Flip_OfAGroupWithFreehand_KeepsTheBox_AndUndoRestoresExactly()
        {
            using var surface = new Surface(new Bitmap(300, 300));
            var rect = AddRect(surface, 60, 50);
            var freehand = AddFreehand(surface);
            var pathBefore = freehand.PathBounds;
            var group = GroupOf(surface, rect, freehand);
            var boxBefore = (group.Left, group.Top, group.Width, group.Height);

            surface.FlipSelected(true);
            Assert.Equal(boxBefore, (group.Left, group.Top, group.Width, group.Height));

            surface.Undo();
            Assert.Equal(boxBefore, (group.Left, group.Top, group.Width, group.Height));

            surface.Undo();
            Assert.Equal((60, 50), (rect.Left, rect.Top));
            Assert.Equal(pathBefore, freehand.PathBounds);
        }

        [Fact]
        public void Flip_OfALoneFreehand_MirrorsThePathInItsBox_AndUndoRestores()
        {
            using var surface = new Surface(new Bitmap(300, 300));
            var freehand = AddFreehand(surface);
            var before = freehand.PathBounds;
            surface.SelectElement(freehand, false, false);

            surface.FlipSelected(true);
            var after = freehand.PathBounds;
            Assert.InRange(Math.Abs(after.Left - before.Left), 0, 2);
            Assert.InRange(Math.Abs(after.Width - before.Width), 0, 2);

            surface.Undo();
            Assert.Equal(before, freehand.PathBounds);
        }

        [Fact]
        public void DeleteOfAGroup_Undo_ThenANewAction_DoesNotDisposeTheChildren()
        {
            using var surface = new Surface(new Bitmap(300, 300));
            var rect = AddRect(surface, 10, 10);
            var image = new ImageContainer(surface) { Left = 100, Top = 100, Width = 20, Height = 20 };
            image.Image = new Bitmap(20, 20);
            surface.AddElement(image, false, false);
            var freehand = AddFreehand(surface);
            var group = GroupOf(surface, rect, image, freehand);

            surface.RemoveElements(new DrawableContainerList(new IDrawableContainer[] { group }), true);
            surface.Undo();
            surface.AddElement(AddRect(surface, 200, 200), true);

            Assert.Equal(20, image.Image.Width);
            Assert.NotNull(freehand.FreehandPath);
            using var export = surface.GetImageForExport();
        }

        [Fact]
        public void Group_NestedTooDeep_IsRefused_ByTheCommand()
        {
            using var surface = new Surface(new Bitmap(600, 300));
            var group = GroupOf(surface, AddRect(surface, 10, 10), AddRect(surface, 60, 10));
            for (int i = 2; i <= GroupContainer.MaximumDepth; i++)
            {
                group = GroupOf(surface, group, AddRect(surface, 10 + i * 50, 100));
            }

            Assert.Equal(GroupContainer.MaximumDepth, GroupContainer.NestingOf(group));
            string message = null;
            surface.SurfaceMessage += (sender, args) => message = args.Message;

            var extra = AddRect(surface, 10, 200);
            surface.DeselectAllElements();
            surface.SelectElement(group, false, false);
            surface.SelectElement(extra, false, false);
            surface.GroupSelected();

            Assert.Same(group, surface.Elements.OfType<GroupContainer>().Single());
            Assert.Contains("단계", message);
        }

        [Fact]
        public void Clone_OfADeepGroupTree_KeepsTheElements()
        {
            using var surface = new Surface(new Bitmap(100, 100));
            var outer = new GroupContainer(surface);
            var current = outer;
            for (int i = 0; i < 12; i++)
            {
                var inner = new GroupContainer(surface);
                current.AddChildToGroup(inner);
                current = inner;
            }

            current.AddChildToGroup(new RectangleContainer(surface) { Width = 5, Height = 5 });
            surface.AddElement(outer, false, false);

            using var clone = (Surface)surface.Clone();

            Assert.Single(clone.Elements);
            // From a file the same tree is refused
            using var stream = new MemoryStream();
            surface.SaveElementsToStream(stream);
            stream.Position = 0;
            using var other = new Surface(new Bitmap(100, 100));
            Assert.Throws<InvalidDataException>(() => other.LoadElementsFromStream(stream));
        }

        private static byte[] Page(int width, int height, int blockTop, bool caret = false)
        {
            var gray = new byte[width * height];
            for (int i = 0; i < gray.Length; i++)
            {
                gray[i] = 255;
            }

            // some text lines
            for (int line = 0; line < 6; line++)
            {
                for (int y = blockTop + line * 14; y < blockTop + line * 14 + 6; y++)
                {
                    for (int x = 20; x < width - 40 - line * 20; x++)
                    {
                        gray[y * width + x] = 40;
                    }
                }
            }

            if (caret)
            {
                for (int y = 10; y < 22; y++)
                {
                    gray[y * width + 5] = 0;
                }
            }

            return gray;
        }

        [Fact]
        public void IsSameView_AcceptsACaret_ButNotAShiftedPage()
        {
            var page = Page(400, 300, 100);

            Assert.True(ViewMatch.IsSameView(page, Page(400, 300, 100), 400, 300));
            Assert.True(ViewMatch.IsSameView(page, Page(400, 300, 100, caret: true), 400, 300));
            Assert.False(ViewMatch.IsSameView(page, Page(400, 300, 160), 400, 300));
            Assert.False(ViewMatch.IsSameView(page, Page(400, 300, 108), 400, 300));
        }

        [Fact]
        public void IsSameView_BlankPages_AreTheSame()
        {
            var blank = Enumerable.Repeat((byte)255, 10000).ToArray();

            Assert.True(ViewMatch.IsSameView(blank, Enumerable.Repeat((byte)255, 10000).ToArray(), 100, 100));
            Assert.False(ViewMatch.IsSameView(null, blank, 100, 100));
        }

        [Fact]
        public void WheelTracker_ReturnsExactlyWhatWasTurned()
        {
            var wheel = new WheelTracker();
            wheel.Up(60);
            wheel.Down(20);
            wheel.Down(20);
            Assert.Equal(20, wheel.Net);
            Assert.Equal(-20, wheel.ReturnUnits);

            wheel.Down(20);
            Assert.Equal(0, wheel.ReturnUnits);
        }

        [Fact]
        public void FieldChange_OfAGroup_IsOneUndoStep()
        {
            using var surface = new Surface(new Bitmap(300, 300));
            var first = AddRect(surface, 50, 50);
            var second = AddRect(surface, 150, 150);
            var group = GroupOf(surface, first, second);
            var list = new DrawableContainerList();
            list.Add(group);
            surface.FieldAggregator.BindElements(list);

            surface.FieldAggregator.GetField(FieldType.FILL_COLOR).Value = Color.Green;
            surface.FieldAggregator.GetField(FieldType.FILL_COLOR).Value = Color.Blue;
            Assert.Equal(Color.Blue.ToArgb(), second.GetFieldValueAsColor(FieldType.FILL_COLOR).ToArgb());

            surface.Undo();
            Assert.Equal(Color.Red.ToArgb(), first.GetFieldValueAsColor(FieldType.FILL_COLOR).ToArgb());
            Assert.Equal(Color.Red.ToArgb(), second.GetFieldValueAsColor(FieldType.FILL_COLOR).ToArgb());
            // The next undo is the grouping, not another color
            Assert.Single(surface.Elements);
        }

        [Fact]
        public void IsValidTree_RejectsAChildWhichTwoGroupsShare()
        {
            using var surface = new Surface(new Bitmap(100, 100));
            var shared = AddRect(surface, 10, 10);
            var first = new GroupContainer(surface);
            var second = new GroupContainer(surface);
            first.AddChildToGroup(shared);
            second.AddChildToGroup(shared);

            Assert.False(GroupContainer.IsValidTree(new IDrawableContainer[] { first, second }));
            Assert.True(GroupContainer.IsValidTree(new IDrawableContainer[] { first }));
        }

        [Fact]
        public void FindRanges_LongText_IsSearchedInChunks_AndFindsTheNumberOnTheBorder()
        {
            string text = new string('a', 4990) + " 010-1234-5678 " + new string('b', 20000) + " hong@example.com";

            var found = Greenshot.Editor.Helpers.SensitiveInfoMatcher.FindRanges(text).Select(range => text.Substring(range.Start, range.Length)).ToList();

            Assert.Single(found, "010-1234-5678");
            Assert.Contains("hong@example.com", found);
        }

        [Fact]
        public void ChildMoved_UpdatesTheBoxOfTheGroup()
        {
            using var surface = new Surface(new Bitmap(300, 300));
            var first = AddRect(surface, 50, 50);
            var second = AddRect(surface, 150, 150);
            var group = GroupOf(surface, first, second);

            second.Left = 200;
            Assert.Equal(240, group.Width);

            first.Left = -10;
            Assert.Equal(40, group.Left);
            Assert.Equal(0, first.Left);
            Assert.Equal(50, group.Left + first.Left + 10);
        }

        [Fact]
        public void FormatUndo_AlsoRestoresTheOwnFieldsOfAGroup()
        {
            using var surface = new Surface(new Bitmap(300, 300));
            var first = AddRect(surface, 50, 50);
            var second = AddRect(surface, 150, 150);
            var group = GroupOf(surface, first, second);
            group.SetFieldValue(FieldType.SHADOW, false);
            var memento = new Greenshot.Editor.Memento.FieldValuesMemento(new[] { group });

            group.SetFieldValue(FieldType.SHADOW, true);
            memento.Restore();

            Assert.False(group.GetFieldValueAsBool(FieldType.SHADOW));
        }
    }
}
