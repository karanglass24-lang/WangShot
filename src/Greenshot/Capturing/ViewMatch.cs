using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace Greenshot.Capturing
{
    /// <summary>
    /// 왕캡: pure helpers of the scroll capture which can be tested without a screen
    /// </summary>
    internal static class ViewMatch
    {
        /// <summary>
        /// True when two views of a page are the same view: a caret, an animation or a header which appears may differ,
        /// but the content (everything which is not background) has to be in the same place. A page with a lot of white space
        /// is not "the same" just because most points are white: only points where one of the views has content count.
        /// </summary>
        internal static bool IsSameView(byte[] a, byte[] b, int width, int height)
        {
            if (a == null || b == null || a.Length < width * height || b.Length < width * height || width <= 0 || height <= 0)
            {
                return false;
            }

            // The background: the most common gray of the first picture
            var histogram = new int[256];
            for (int y = 0; y < height; y += 3)
            {
                for (int x = 0; x < width; x += 5)
                {
                    histogram[a[y * width + x]]++;
                }
            }

            int background = 0;
            for (int i = 1; i < 256; i++)
            {
                if (histogram[i] > histogram[background])
                {
                    background = i;
                }
            }

            long samples = 0;
            long different = 0;
            long informative = 0;
            long informativeDifferent = 0;
            for (int y = 0; y < height; y += 3)
            {
                for (int x = 0; x < width; x += 5)
                {
                    int grayA = a[y * width + x];
                    int grayB = b[y * width + x];
                    bool differs = Math.Abs(grayA - grayB) > 12;
                    samples++;
                    if (differs)
                    {
                        different++;
                    }

                    if (Math.Abs(grayA - background) > 24 || Math.Abs(grayB - background) > 24)
                    {
                        informative++;
                        if (differs)
                        {
                            informativeDifferent++;
                        }
                    }
                }
            }

            if (samples == 0 || different > samples * 0.04)
            {
                return false;
            }

            return informative == 0 || informativeDifferent <= informative * 0.15;
        }

        internal static bool IsSameView(Bitmap a, Bitmap b)
        {
            if (a == null || b == null || a.Size != b.Size)
            {
                return false;
            }

            return IsSameView(ToGray(a), ToGray(b), a.Width, a.Height);
        }

        private static unsafe byte[] ToGray(Bitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            var gray = new byte[width * height];
            var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                for (int y = 0; y < height; y++)
                {
                    byte* row = (byte*)data.Scan0 + y * data.Stride;
                    int offset = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        gray[offset + x] = (byte)((row[x * 3] + 2 * row[x * 3 + 1] + row[x * 3 + 2]) >> 2);
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            return gray;
        }
    }

    /// <summary>
    /// 왕캡: how far the wheel was turned during the restore of a view, so that a failed restore can go back to where it started
    /// </summary>
    internal sealed class WheelTracker
    {
        /// <summary>
        /// Units turned up (positive) minus units turned down
        /// </summary>
        internal int Net { get; private set; }

        internal void Up(int units) => Net += units;

        internal void Down(int units)
        {
            Net -= units;
            DownUnits += units;
        }

        /// <summary>
        /// The sum of the units turned down
        /// </summary>
        internal int DownUnits { get; private set; }

        /// <summary>
        /// The units to turn up (positive) or down (negative) to be where the tracking started
        /// </summary>
        internal int ReturnUnits => -Net;
    }
}
