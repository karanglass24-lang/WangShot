using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Dapplo.Windows.Common.Structs;
using Greenshot.Base.Interfaces.Plugin;

namespace Greenshot.Editor.Helpers
{
    /// <summary>
    /// 왕샷: finds personal data (phone numbers, e-mail addresses, resident registration numbers, card numbers) in the OCR result
    /// </summary>
    public static class SensitiveInfoMatcher
    {
        private static readonly Regex[] Patterns =
        {
            // Mobile phone: 010-1234-5678, 010 1234 5678, 01012345678
            new Regex(@"(?<!\d)01[016789][-.\s]?\d{3,4}[-.\s]?\d{4}(?!\d)", RegexOptions.Compiled),
            // Landline: 02-123-4567, 031-1234-5678
            new Regex(@"(?<!\d)0\d{1,2}[-.\s]\d{3,4}[-.\s]\d{4}(?!\d)", RegexOptions.Compiled),
            // E-mail
            new Regex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.Compiled),
            // Resident registration number: 900101-1234567
            new Regex(@"(?<!\d)\d{2}(0[1-9]|1[0-2])(0[1-9]|[12]\d|3[01])[-\s]?[1-8]\d{6}(?!\d)", RegexOptions.Compiled),
            // Card number: 1234-5678-9012-3456
            new Regex(@"(?<!\d)\d{4}[-\s]\d{4}[-\s]\d{4}[-\s]\d{4}(?!\d)", RegexOptions.Compiled),
        };

        /// <summary>
        /// The kinds of personal data in a text, as character ranges
        /// </summary>
        public static IEnumerable<(int Start, int Length)> FindRanges(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield break;
            }

            foreach (var pattern in Patterns)
            {
                foreach (Match match in pattern.Matches(text))
                {
                    yield return (match.Index, match.Length);
                }
            }
        }

        /// <summary>
        /// The areas (with the height of their text line) of the words which belong to personal data. A match over several words gets one area.
        /// </summary>
        public static List<(NativeRect Bounds, int TextHeight)> FindAreas(IEnumerable<IOcrLineFeature> lines)
        {
            var areas = new List<(NativeRect Bounds, int TextHeight)>();
            foreach (var line in lines ?? Enumerable.Empty<IOcrLineFeature>())
            {
                var words = line.Words;
                if (words == null || words.Count == 0)
                {
                    continue;
                }

                // The words, one space between them: the position of every word in the text is known
                var text = new StringBuilder();
                var starts = new int[words.Count];
                for (int i = 0; i < words.Count; i++)
                {
                    if (i > 0)
                    {
                        text.Append(' ');
                    }

                    starts[i] = text.Length;
                    text.Append(words[i].Text);
                }

                var covered = new bool[words.Count];
                foreach (var (start, length) in FindRanges(text.ToString()).OrderBy(range => range.Start))
                {
                    int end = start + length;
                    var hit = Enumerable.Range(0, words.Count)
                        .Where(i => starts[i] < end && starts[i] + (words[i].Text?.Length ?? 0) > start)
                        .ToList();
                    if (hit.Count == 0 || hit.All(i => covered[i]))
                    {
                        continue;
                    }

                    int left = hit.Min(i => words[i].Bounds.Left);
                    int top = hit.Min(i => words[i].Bounds.Top);
                    int right = hit.Max(i => words[i].Bounds.Right);
                    int bottom = hit.Max(i => words[i].Bounds.Bottom);
                    foreach (int i in hit)
                    {
                        covered[i] = true;
                    }

                    areas.Add((new NativeRect(left, top, right - left, bottom - top), line.Bounds.Height));
                }
            }

            return areas;
        }
    }
}
