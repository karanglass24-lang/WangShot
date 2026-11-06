using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

namespace Greenshot.Capturing
{
    /// <summary>
    /// 왕샷: puts the pictures of a page which is scrolled step by step together. For every new picture it looks for the distance the page moved
    /// (a strip of the new picture is searched in the old one), rows which don't move (a fixed header or footer) are taken once.
    /// </summary>
    internal sealed class ScrollStitcher
    {
        private const int StripHeight = 64;
        private readonly List<(Bitmap Image, int From, int To)> _segments = new();
        private Bitmap _first;
        private Bitmap _previous;
        private byte[] _previousGray;
        private int _width;
        private int _height;
        private int _lastDy;
        private int _lastFooter;
        private Bitmap _lastFooterImage;
        private bool _firstPairDone;

        /// <summary>
        /// The number of pictures which were added (the first one included)
        /// </summary>
        public int FrameCount { get; private set; }

        /// <summary>
        /// The height of the page so far
        /// </summary>
        public int TotalHeight { get; private set; }

        /// <summary>
        /// How far the page moved at the last step
        /// </summary>
        public int LastMove => _lastDy;

        /// <summary>
        /// The height of the pictures
        /// </summary>
        public int FrameHeight => _height;

        /// <summary>
        /// True when the picture looks like the last one which was added (the page didn't move)
        /// </summary>
        public bool LooksLikeLast(Bitmap candidate)
        {
            if (_previousGray == null || candidate.Width != _width || candidate.Height != _height)
            {
                return false;
            }

            var gray = ToGray(candidate);
            long sum = 0;
            long count = 0;
            for (int y = 0; y < _height; y += 3)
            {
                for (int x = 0; x < _width; x += 5)
                {
                    sum += Math.Abs(_previousGray[y * _width + x] - gray[y * _width + x]);
                    count++;
                }
            }

            return count > 0 && (double)sum / count < 0.6;
        }

        /// <summary>
        /// The first picture, the top of the page
        /// </summary>
        public void Start(Bitmap first)
        {
            _first = (Bitmap)first.Clone();
            _previous = _first;
            _width = first.Width;
            _height = first.Height;
            _previousGray = ToGray(first);
            FrameCount = 1;
            TotalHeight = _height;
        }

        /// <summary>
        /// Add the next picture (the page was scrolled down)
        /// </summary>
        /// <returns>false when the page didn't move or the pictures don't fit together: the page is at its end</returns>
        public bool TryAdd(Bitmap next)
        {
            if (next.Width != _width || next.Height != _height)
            {
                return false;
            }

            var gray = ToGray(next);
            int footer = FixedFooter(_previousGray, gray, _width, _height);
            if (_firstPairDone)
            {
                // A white gap above the footer looks like footer in one pair: the footer is the same in every pair, the smallest value counts
                footer = Math.Min(footer, _lastFooter);
            }
            int header = FixedHeader(_previousGray, gray, _width, _height);
            int dy = FindMove(_previousGray, gray, _width, _height, _lastDy, header, footer);
            if (dy <= 1)
            {
                return false;
            }

            if (!_firstPairDone)
            {
                // The first picture up to its footer
                _segments.Add((_first, 0, _height - footer));
                _firstPairDone = true;
                TotalHeight = _height - footer;
            }

            int from = Math.Max(0, _height - footer - dy);
            int to = _height - footer;
            // Only the new rows are kept (a long page would take too much memory otherwise)
            var copy = next.Clone(new Rectangle(0, from, _width, to - from), PixelFormat.Format24bppRgb);
            _segments.Add((copy, 0, to - from));
            TotalHeight += to - from;
            _lastFooterImage?.Dispose();
            _lastFooterImage = footer > 0 ? next.Clone(new Rectangle(0, _height - footer, _width, footer), PixelFormat.Format24bppRgb) : null;
            _previousGray = gray;
            _lastDy = dy;
            _lastFooter = footer;
            FrameCount++;
            return true;
        }

        /// <summary>
        /// The page as one picture
        /// </summary>
        public Bitmap Finish()
        {
            if (!_firstPairDone)
            {
                return (Bitmap)_first.Clone();
            }

            int total = TotalHeight + _lastFooter;
            var result = new Bitmap(_width, total, PixelFormat.Format24bppRgb);
            using var graphics = Graphics.FromImage(result);
            int y = 0;
            foreach (var (image, from, to) in _segments)
            {
                graphics.DrawImage(image, new Rectangle(0, y, _width, to - from), new Rectangle(0, from, _width, to - from), GraphicsUnit.Pixel);
                y += to - from;
            }

            if (_lastFooter > 0 && _lastFooterImage != null)
            {
                graphics.DrawImage(_lastFooterImage, new Rectangle(0, y, _width, _lastFooter), new Rectangle(0, 0, _width, _lastFooter), GraphicsUnit.Pixel);
            }

            return result;
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
                        // B, G, R
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

        private static int[] SampleColumns(int width)
        {
            var columns = new List<int>();
            for (int x = width / 8; x < width - width / 8; x += 3)
            {
                columns.Add(x);
            }

            return columns.ToArray();
        }

        private static long RowSad(byte[] a, int rowA, byte[] b, int rowB, int width, int[] columns, long limit)
        {
            long sad = 0;
            int offsetA = rowA * width;
            int offsetB = rowB * width;
            foreach (int x in columns)
            {
                sad += Math.Abs(a[offsetA + x] - b[offsetB + x]);
                if (sad > limit)
                {
                    break;
                }
            }

            return sad;
        }

        private static double Texture(byte[] gray, int width, int row, int strip, int[] columns)
        {
            long sum = 0;
            long count = 0;
            for (int r = 0; r < strip; r += 2)
            {
                foreach (int x in columns)
                {
                    sum += gray[(row + r) * width + x];
                    count++;
                }
            }

            double mean = (double)sum / count;
            double variance = 0;
            for (int r = 0; r < strip; r += 2)
            {
                foreach (int x in columns)
                {
                    variance += Math.Abs(gray[(row + r) * width + x] - mean);
                }
            }

            return variance / count;
        }

        /// <summary>
        /// How far the page moved up between the two pictures, 0 when the pictures don't fit together (or the page didn't move).
        /// A strip of the new picture is searched in the old one; strips with texture are tried one after the other, from the top
        /// (a strip near the top fits when the page moved a lot, one lower down when it moved a little).
        /// </summary>
        internal static int FindMove(byte[] before, byte[] after, int width, int height, int expected, int header = 0, int footer = 0)
        {
            var columns = SampleColumns(width);
            int strip = Math.Min(StripHeight, height / 4);
            if (columns.Length == 0 || strip < 8)
            {
                return 0;
            }

            long threshold = (long)strip * columns.Length * 4;
            // The first places are right under a fixed header: they fit when the page moved by almost the height of the picture
            foreach (double position in new[] { -1.0, -2.0, 0.14, 0.20, 0.27, 0.34, 0.42, 0.50, 0.58, 0.66 })
            {
                int bestRow = position == -1.0 ? header + 6 : position == -2.0 ? header + 20 : Math.Max((int)(height * position), header + 6);
                // The strip lies in the part of the picture which scrolls (not in a fixed header or footer)
                if (bestRow + strip > height - footer || Texture(after, width, bestRow, strip, columns) < 2.0)
                {
                    continue;
                }

                long minSad = long.MaxValue;
                var scores = new Dictionary<int, long>();
                for (int y = bestRow + 1; y <= height - footer - strip; y++)
                {
                    long sad = 0;
                    // Only fully computed values count: the sum is stopped when it is over the limit
                    long slack = minSad == long.MaxValue ? 0 : Math.Max(minSad / 4, (long)strip * columns.Length / 16);
                    long limit = minSad == long.MaxValue ? threshold * 4 : Math.Min(threshold * 4, minSad + slack);
                    for (int r = 0; r < strip && sad <= limit; r++)
                    {
                        sad += RowSad(before, y + r, after, bestRow + r, width, columns, limit - sad);
                    }

                    if (sad <= limit)
                    {
                        scores[y] = sad;
                        if (sad < minSad)
                        {
                            minSad = sad;
                        }
                    }
                }

                if (minSad > threshold)
                {
                    continue;
                }

                // Of the places which fit about equally well, the one which is nearest to the last move (the steps are all of the same size)
                int chosen = -1;
                // Only places which fit (almost) as well as the best one count
                long tolerance = Math.Max(minSad / 4, (long)strip * columns.Length / 16);
                foreach (var pair in scores)
                {
                    if (pair.Value > minSad + tolerance)
                    {
                        continue;
                    }

                    if (chosen < 0 || (expected > 0 ? Math.Abs(pair.Key - bestRow - expected) < Math.Abs(chosen - bestRow - expected) : pair.Value < scores[chosen]))
                    {
                        chosen = pair.Key;
                    }
                }

                if (chosen > 0)
                {
                    return chosen - bestRow;
                }
            }

            return 0;
        }
        /// <summary>
        /// The rows at the top which are the same in both pictures: a header which doesn't scroll
        /// </summary>
        internal static int FixedHeader(byte[] before, byte[] after, int width, int height)
        {
            var columns = SampleColumns(width);
            int limitRows = height / 3;
            int rows = 0;
            long tolerance = columns.Length * 2L;
            while (rows < limitRows && RowSad(before, rows, after, rows, width, columns, tolerance) <= tolerance)
            {
                rows++;
            }

            if (rows < 8)
            {
                return 0;
            }

            // Blank rows at the top aren't a header: the page may just start with white space
            long spread = 0;
            long count = 0;
            for (int r = 0; r < rows; r += 2)
            {
                foreach (int x in columns)
                {
                    spread += Math.Abs(after[r * width + x] - after[x]);
                    count++;
                }
            }

            return count > 0 && (double)spread / count < 1.5 ? 0 : rows;
        }
        /// <summary>
        /// The rows at the bottom which are the same in both pictures: a footer which doesn't scroll
        /// </summary>
        internal static int FixedFooter(byte[] before, byte[] after, int width, int height)
        {
            var columns = SampleColumns(width);
            int limitRows = height / 3;
            int rows = 0;
            long tolerance = columns.Length * 2L;
            while (rows < limitRows && RowSad(before, height - 1 - rows, after, height - 1 - rows, width, columns, tolerance) <= tolerance)
            {
                rows++;
            }

            if (rows < 8)
            {
                return 0;
            }

            // A footer of blank rows isn't worth keeping apart
            long spread = 0;
            long count = 0;
            for (int r = 0; r < rows; r += 2)
            {
                foreach (int x in columns)
                {
                    spread += Math.Abs(after[(height - 1 - r) * width + x] - after[(height - 1) * width + x]);
                    count++;
                }
            }

            return count > 0 && (double)spread / count < 1.5 ? 0 : rows;
        }
    }
}
