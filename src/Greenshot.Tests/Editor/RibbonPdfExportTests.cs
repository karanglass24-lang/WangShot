using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using Greenshot.Editor.Forms;
using Xunit;

namespace Greenshot.Tests.Editor
{
    public class RibbonPdfExportTests
    {
        [Fact]
        public void ExportUsesImageDpiAndLosslessRgb()
        {
            using var image = new Bitmap(64, 32);
            image.SetResolution(192, 192);
            image.SetPixel(0, 0, Color.Red);
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
            try
            {
                var type = typeof(ImageEditorForm).Assembly.GetType("Greenshot.Editor.Forms.PdfExport", true);
                type.GetMethod("Save", BindingFlags.Static | BindingFlags.Public).Invoke(null, new object[] { image, path, 92L });
                byte[] bytes = File.ReadAllBytes(path);
                string pdf = Encoding.GetEncoding("ISO-8859-1").GetString(bytes);
                Assert.Contains("/MediaBox [0 0 24 12]", pdf);
                Assert.DoesNotContain("/DCTDecode", pdf);
                int start = pdf.IndexOf("stream\n", pdf.IndexOf("/Subtype /Image", StringComparison.Ordinal), StringComparison.Ordinal) + 7;
                Assert.Equal(255, bytes[start]);
                Assert.Equal(0, bytes[start + 1]);
                Assert.Equal(0, bytes[start + 2]);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
