using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Forms;
using Greenshot.Editor.Drawing;
using Greenshot.Editor.Forms;
using Xunit;

namespace Greenshot.Tests.Editor
{
    /// <summary>
    /// 왕캡: the ribbon shows the state of the shown tab, not of the tab shown before. Run it alone (like EditorConstructionTests).
    /// </summary>
    public class EditorTabStateTests
    {
        [InteractiveDesktopFact]
        public void GridButton_FollowsTheShownTab()
        {
            TestEnvironment.EnsureInitialized();
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using var first = new Surface(new Bitmap(100, 100));
                    using var second = new Surface(new Bitmap(100, 100));
                    using var editor = new ImageEditorForm(first, true);
                    var secondTab = editor.AddTab(second, outputMade: true, fitWindow: false);
                    var firstTab = editor.Tabs[0];
                    var gridButton = (ToolStripButton)typeof(ImageEditorForm).GetField("_gridButton", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);

                    editor.SelectTab(firstTab);
                    gridButton.PerformClick();
                    Assert.True(first.ShowGrid);
                    Assert.True(gridButton.Checked);

                    editor.SelectTab(secondTab);
                    Assert.False(second.ShowGrid);
                    Assert.False(gridButton.Checked);

                    // A click on the second tab turns its grid on (it turned it on and showed the button unchecked before)
                    gridButton.PerformClick();
                    Assert.True(second.ShowGrid);
                    Assert.True(gridButton.Checked);

                    editor.SelectTab(firstTab);
                    Assert.True(gridButton.Checked);
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "The editor test timed out.");
            if (failure != null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }
    }
}
