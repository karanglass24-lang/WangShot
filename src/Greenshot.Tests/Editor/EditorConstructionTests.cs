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
