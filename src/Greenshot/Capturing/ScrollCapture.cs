using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Greenshot.Base;
using Greenshot.Base.Core;
using Greenshot.Base.Interfaces;
using Greenshot.Base.Threading;
using Greenshot.Editor.Drawing;
using Greenshot.Editor.Forms;
using log4net;

namespace Greenshot.Capturing
{
    /// <summary>
    /// 왕샷: a screenshot of a whole page: the window under the mouse is scrolled down step by step, every step is captured and the pictures are
    /// put together (see <see cref="ScrollStitcher"/>). Esc stops it.
    /// </summary>
    internal static class ScrollCapture
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ScrollCapture));
        private const int MaximumFrames = 80;
        private const int MaximumHeight = 30000;
        private static bool _running;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT point);

        [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd, out RECT rect);

        [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hwnd, ref POINT point);

        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder text, int count);

        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);

        [DllImport("user32.dll")] private static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);

        private static void Notify(string message, int seconds = 4)
        {
            SimpleServiceProvider.Current?.GetInstance<INotificationService>(isOptional: true)?.ShowInfoMessage(message, TimeSpan.FromSeconds(seconds));
        }

        /// <summary>
        /// Start: after a few seconds the window under the mouse is scrolled and captured
        /// </summary>
        public static async Task RunAsync()
        {
            if (_running)
            {
                return;
            }

            _running = true;
            try
            {
                Notify("3초 뒤 마우스가 있는 창을 스크롤하면서 캡처해요. 창 맨 위에서 시작하고, Esc로 멈출 수 있어요.", 4);
                await Task.Delay(3500).ConfigureAwait(false);

                GetCursorPos(out var cursor);
                var window = GetAncestor(WindowFromPoint(cursor), 2 /* GA_ROOT */);
                if (window == IntPtr.Zero || IsForbiddenWindow(window))
                {
                    Notify("스크롤 캡처할 창 위에 마우스를 두고 다시 시도해 주세요.");
                    return;
                }

                var rect = ClientRectOnScreen(window);
                if (rect.Width < 100 || rect.Height < 100)
                {
                    Notify("이 창은 너무 작아서 스크롤 캡처할 수 없어요.");
                    return;
                }

                string title = TitleOf(window);
                SetForegroundWindow(window);
                await Task.Delay(400).ConfigureAwait(false);

                var (bitmap, frames, stoppedByUser) = await CaptureAllAsync(rect, cursor).ConfigureAwait(false);
                if (bitmap == null)
                {
                    Notify("스크롤 캡처에 실패했어요.");
                    return;
                }

                await UiDispatcher.Current.InvokeAsync(() =>
                {
                    var surface = new Surface(bitmap);
                    surface.CaptureDetails.Title = string.IsNullOrWhiteSpace(title) ? "스크롤 캡처" : title;
                    ImageEditorForm.Open(surface);
                    // Saved like every capture: in the folder with the next number
                    DestinationHelper.StartExport(WellKnownDestinations.FileNoDialog, surface);
                }).ConfigureAwait(false);

                Notify(frames < 2
                    ? "창이 스크롤되지 않아서 보이는 부분만 캡처했어요."
                    : $"스크롤 캡처 완료: {frames}장, 높이 {bitmap.Height}px{(stoppedByUser ? " (Esc로 멈췄어요)" : string.Empty)}");
            }
            catch (Exception ex)
            {
                Log.Warn("Scroll capture failed", ex);
                Notify("스크롤 캡처 중 오류가 났어요.");
            }
            finally
            {
                _running = false;
            }
        }

        private static bool IsForbiddenWindow(IntPtr window)
        {
            GetWindowThreadProcessId(window, out uint processId);
            using var current = Process.GetCurrentProcess();
            if (processId == current.Id)
            {
                return true;
            }

            var className = new StringBuilder(128);
            GetClassName(window, className, className.Capacity);
            string name = className.ToString();
            return name is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
        }

        private static string TitleOf(IntPtr window)
        {
            var text = new StringBuilder(256);
            GetWindowText(window, text, text.Capacity);
            return text.ToString();
        }

        private static Rectangle ClientRectOnScreen(IntPtr window)
        {
            GetClientRect(window, out var client);
            var origin = new POINT { X = 0, Y = 0 };
            ClientToScreen(window, ref origin);
            return new Rectangle(origin.X, origin.Y, client.Right - client.Left, client.Bottom - client.Top);
        }

        private static Bitmap Grab(Rectangle rect)
        {
            var bitmap = new Bitmap(rect.Width, rect.Height, PixelFormat.Format24bppRgb);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, rect.Size, CopyPixelOperation.SourceCopy);
            return bitmap;
        }

        private static bool Same(Bitmap a, Bitmap b)
        {
            // Compared by a coarse sample: the scrolling of the page has stopped when two pictures in a row look the same
            for (int y = 0; y < a.Height; y += Math.Max(1, a.Height / 40))
            {
                for (int x = 0; x < a.Width; x += Math.Max(1, a.Width / 40))
                {
                    var p = a.GetPixel(x, y);
                    var q = b.GetPixel(x, y);
                    if (Math.Abs(p.R - q.R) + Math.Abs(p.G - q.G) + Math.Abs(p.B - q.B) > 12)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static async Task<(Bitmap Result, int Frames, bool StoppedByUser)> CaptureAllAsync(Rectangle rect, POINT originalCursor)
        {
            var stitcher = new ScrollStitcher();
            bool stopped = false;
            try
            {
                // The mouse over the middle of the page: the wheel scrolls what is under it
                SetCursorPos(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);
                await Task.Delay(250).ConfigureAwait(false);
                using (var first = Grab(rect))
                {
                    stitcher.Start(first);
                }

                while (stitcher.FrameCount < MaximumFrames && stitcher.TotalHeight < MaximumHeight)
                {
                    if ((GetAsyncKeyState(0x1B) & 0x8000) != 0)
                    {
                        stopped = true;
                        break;
                    }

                    // Five notches of the wheel down
                    mouse_event(0x0800 /* MOUSEEVENTF_WHEEL */, 0, 0, unchecked((uint)(-120 * 5)), UIntPtr.Zero);
                    Bitmap current = null;
                    for (int attempt = 0; attempt < 14; attempt++)
                    {
                        await Task.Delay(attempt == 0 ? 350 : 140).ConfigureAwait(false);
                        var next = Grab(rect);
                        if (current != null && Same(current, next))
                        {
                            current.Dispose();
                            current = next;
                            break;
                        }

                        current?.Dispose();
                        current = next;
                    }

                    bool added;
                    using (current)
                    {
                        added = stitcher.TryAdd(current);
                    }

                    if (!added)
                    {
                        break;
                    }
                }

                return (stitcher.Finish(), stitcher.FrameCount, stopped);
            }
            catch (Exception ex)
            {
                Log.Warn("Capturing the scrolled page failed", ex);
                return (null, 0, false);
            }
            finally
            {
                SetCursorPos(originalCursor.X, originalCursor.Y);
            }
        }
    }
}
