using System;
using System.Drawing;
using System.Runtime.ExceptionServices;
using System.Threading;
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
