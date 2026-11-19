using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Greenshot.Editor.Forms
{
    /// <summary>
    /// Saves an image as a lossless RGB PDF page using its resolution.
    /// </summary>
    internal static class PdfExport
    {
        public static void Save(Image image, string path, long jpegQuality = 92)
        {
            int width = image.Width;
            int height = image.Height;
            byte[] rgb = new byte[checked(width * height * 3)];
            using (var flat = new Bitmap(width, height, PixelFormat.Format24bppRgb))
            {
                using (var graphics = Graphics.FromImage(flat))
                {
                    graphics.Clear(Color.White);
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(image, 0, 0, width, height);
                }

                var bits = flat.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                try
                {
                    var row = new byte[Math.Abs(bits.Stride)];
                    for (int y = 0; y < height; y++)
                    {
                        System.Runtime.InteropServices.Marshal.Copy(IntPtr.Add(bits.Scan0, y * bits.Stride), row, 0, row.Length);
                        for (int x = 0; x < width; x++)
                        {
                            int source = x * 3, dest = (y * width + x) * 3;
                            rgb[dest] = row[source + 2];
                            rgb[dest + 1] = row[source + 1];
                            rgb[dest + 2] = row[source];
                        }
                    }
                }
                finally { flat.UnlockBits(bits); }
            }

            double dpiX = image.HorizontalResolution > 0 ? image.HorizontalResolution : 96;
            double dpiY = image.VerticalResolution > 0 ? image.VerticalResolution : 96;
            double pageWidth = width * 72 / dpiX;
            double pageHeight = height * 72 / dpiY;
            double limit = Math.Max(pageWidth, pageHeight);
            if (limit > 14400)
            {
                double shrink = 14400 / limit;
                pageWidth *= shrink;
                pageHeight *= shrink;
            }

            string w = pageWidth.ToString("0.##", CultureInfo.InvariantCulture);
            string h = pageHeight.ToString("0.##", CultureInfo.InvariantCulture);
            var offsets = new long[6];
            using var output = new MemoryStream();

            void Write(string text)
            {
                var bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(text);
                output.Write(bytes, 0, bytes.Length);
            }

            Write("%PDF-1.4\n%\u00e2\u00e3\u00cf\u00d3\n");
            offsets[1] = output.Position;
            Write("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
            offsets[2] = output.Position;
            Write("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");
            offsets[3] = output.Position;
            Write($"3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {w} {h}] /Resources << /XObject << /Im0 5 0 R >> >> /Contents 4 0 R >>\nendobj\n");
            string content = $"q {w} 0 0 {h} 0 0 cm /Im0 Do Q\n";
            offsets[4] = output.Position;
            Write($"4 0 obj\n<< /Length {content.Length} >>\nstream\n{content}endstream\nendobj\n");
            offsets[5] = output.Position;
            Write($"5 0 obj\n<< /Type /XObject /Subtype /Image /Width {width} /Height {height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Length {rgb.Length} >>\nstream\n");
            output.Write(rgb, 0, rgb.Length);
            Write("\nendstream\nendobj\n");
            long xref = output.Position;
            Write("xref\n0 6\n0000000000 65535 f \n");
            for (int i = 1; i <= 5; i++)
            {
                Write(offsets[i].ToString("0000000000", CultureInfo.InvariantCulture) + " 00000 n \n");
            }

            Write($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
            File.WriteAllBytes(path, output.ToArray());
        }
    }
}
