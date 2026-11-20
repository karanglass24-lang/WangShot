using System;
using System.Drawing;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Greenshot.Base.Interfaces;
using Greenshot.Editor.Drawing;
using Greenshot.Editor.Forms;
using Xunit;

namespace Greenshot.Tests.Editor
{
    public class EditorConstructionTests
    {
        [InteractiveDesktopFact]
        public void EditorCanBeConstructedWithAnInitialSurface()
        {
            TestEnvironment.EnsureInitialized();
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using var surface = new Surface(new Bitmap(100, 100));
                    using var editor = new ImageEditorForm(surface, true);
                    Assert.NotNull(editor);
                    var ribbon = typeof(ImageEditorForm).GetField("_ribbon", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
                    var strips = (IReadOnlyList<ToolStrip>)ribbon.GetType().GetProperty("Strips").GetValue(ribbon);
                    Assert.Contains(strips, strip => strip.Items.Cast<ToolStripItem>().Any(item => item.Text == "스크롤 캡처"));
                    Assert.Contains(strips, strip => strip.Items.Cast<ToolStripItem>().Any(item => item.Text == "개체 복사") &&
                                                     strip.Items.Cast<ToolStripItem>().Any(item => item.Text == "서식 적용"));
                    Assert.Contains(strips, strip => strip.Items.Cast<ToolStripItem>().Any(item => item.Text == "여백 추가") &&
                                                     strip.Items.Cast<ToolStripItem>().Any(item => item.Text == "우회전"));
                    Assert.Contains(strips, strip => strip.Items.Cast<ToolStripItem>().Any(item => item.Text == "전체 이미지 복사") &&
                                                     strip.Items.Cast<ToolStripItem>().Any(item => item.Text == "텍스트 복사"));
                    int checkedMenus = 0;
                    foreach (var item in strips.SelectMany(strip => strip.Items.Cast<ToolStripItem>())
                                 .Where(item => item.GetType().Name == "ToolStripDropButton"))
                    {
                        var type = item.GetType();
                        var members = (System.Collections.ICollection)type.GetField("_members", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(item);
                        if (members.Count == 0) continue;
                        var menu = (ToolStripDropDownMenu)type.GetProperty("DropDown").GetValue(item);
                        type.GetMethod("RebuildMenu", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(item, new object[] { menu });
                        Assert.All(menu.Items.Cast<ToolStripItem>(), choice => Assert.False(string.IsNullOrWhiteSpace(choice.Text)));
                        checkedMenus++;
                    }
                    Assert.True(checkedMenus >= 8);
                    var toggleFullscreen = typeof(ImageEditorForm).GetMethod("ToggleFullscreen", BindingFlags.Instance | BindingFlags.NonPublic);
                    var fullscreenButton = (ToolStripButton)typeof(ImageEditorForm).GetField("_fullscreenButton", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
                    toggleFullscreen.Invoke(editor, null);
                    Assert.Equal("전체 화면 종료", fullscreenButton.Text);
                    toggleFullscreen.Invoke(editor, null);
                    Assert.Equal("전체 화면", fullscreenButton.Text);
                    // Selecting the text tool binds an integer line thickness to the format tab.
                    surface.DrawingMode = DrawingModes.Text;
                    Assert.Equal(DrawingModes.Text, surface.DrawingMode);
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "Editor construction timed out.");
            if (failure != null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }
    }
}
