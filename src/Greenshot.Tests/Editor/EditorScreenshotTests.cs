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
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using Greenshot.Base.Core;
using Greenshot.Editor.Configuration;
using Greenshot.Editor.Drawing;
using Greenshot.Editor.Forms;
using Xunit;

namespace Greenshot.Tests.Editor
{
    public class EditorScreenshotTests
    {
        public EditorScreenshotTests()
        {
            TestEnvironment.EnsureInitialized();
        }

        [InteractiveDesktopFact]
        public void TakePictures()
        {
            var folder = @"D:\llm\capture\fx\captures";
            Directory.CreateDirectory(folder);
            foreach (var file in Directory.GetFiles(folder))
            {
                File.Delete(file);
            }

            using (var source = new Bitmap(900, 620, PixelFormat.Format32bppArgb))
            using (var graphics = Graphics.FromImage(source))
            {
                graphics.Clear(Color.White);
                graphics.FillRectangle(new SolidBrush(Color.FromArgb(240, 244, 250)), 0, 40, 900, 580);
                graphics.FillRectangle(new SolidBrush(Color.FromArgb(0, 120, 212)), 300, 200, 160, 40);
            }

            // Three captures for the panel
            for (int i = 0; i < 3; i++)
            {
                var file = Path.Combine(folder, $"0{i + 1}0.png");
                using var copy = new Bitmap(900, 620 + i * 120, PixelFormat.Format32bppArgb);
                using (var graphics = Graphics.FromImage(copy))
                {
                    graphics.Clear(Color.White);
                    graphics.FillRectangle(new SolidBrush(Color.FromArgb(200, 225, 245)), 30, 30 + i * 20, 800, 500);
                    graphics.DrawString($"cap {i + 1}", new Font("Segoe UI", 30), Brushes.Black, 60, 80 + i * 20);
                }

                copy.Save(file, ImageFormat.Png);
            }

            var core = IniConfigHelper.EnsureSection<ICoreConfiguration>(() => new CoreConfigurationImpl());
            core.OutputFilePath = folder;
            IniConfigHelper.EnsureSection<IEditorConfiguration>(() => new EditorConfigurationImpl()).RecentCapturesPanelShown = true;

            var image = new Bitmap(1280, 800, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(image))
            {
                graphics.Clear(Color.White);
                graphics.FillRectangle(new SolidBrush(Color.FromArgb(0, 120, 212)), 420, 300, 220, 50);
                graphics.DrawString("Grade report", new Font("Segoe UI", 22), Brushes.Black, 60, 60);
                graphics.DrawString("1. Load the list of students.", new Font("Segoe UI", 14), Brushes.Black, 60, 140);
                graphics.DrawString("2. Enter the grades and save.", new Font("Segoe UI", 14), Brushes.Black, 60, 190);
                graphics.DrawString("3. Print the result.", new Font("Segoe UI", 14), Brushes.Black, 60, 240);
            }

            var surface = new Surface(image);
            var editor = new ImageEditorForm(surface, true);
            bool painted = false;
            editor.Shown += (_, _) => editor.BeginInvoke(new MethodInvoker(() => painted = true));
            editor.Show();
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            while (timeout.ElapsedMilliseconds < 15000)
            {
                Application.DoEvents();
                System.Threading.Thread.Sleep(10);
                if (painted && timeout.ElapsedMilliseconds > 6000)
                {
                    break;
                }
            }

            editor.Update();
            using (var bitmap = new Bitmap(editor.Width, editor.Height))
            {
                editor.DrawToBitmap(bitmap, new Rectangle(0, 0, editor.Width, editor.Height));
                bitmap.Save(@"D:\llm\capture\fx\editor.png", ImageFormat.Png);
            }

            surface.Modified = false;
            editor.Hide();
            editor.Dispose();
            Application.DoEvents();
        }
    }
}
