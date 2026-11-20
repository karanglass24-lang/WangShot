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
        // 왕캡: OCR often splits a number after its separator ("010-", "1234-", "5678"), so a separator may have a space on both sides.
        // Words are joined with one space, so "010- 1234- 5678" and "010 -1234 -5678" are the same number as "010-1234-5678".
        private const string Separator = @"(?:\s?[-.]\s?|\s)";
        private const string OptionalSeparator = @"(?:\s?[-.]\s?|\s)?";

        private static readonly (Regex Pattern, Func<string, bool> IsValid)[] Patterns =
        {
            // Mobile phone: 010-1234-5678, 010 1234 5678, 01012345678
            (new Regex(@"(?<!\d)01[016789]" + OptionalSeparator + @"\d{3,4}" + OptionalSeparator + @"\d{4}(?!\d)", RegexOptions.Compiled), null),
            // Landline: 02-123-4567, 031-1234-5678 (a separator is needed, ten digits alone are an ordinary number)
            (new Regex(@"(?<!\d)0\d{1,2}" + Separator + @"\d{3,4}" + Separator + @"\d{4}(?!\d)", RegexOptions.Compiled), null),
            // E-mail
            (new Regex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.Compiled), null),
            // Resident registration number: 900101-1234567
            (new Regex(@"(?<!\d)\d{2}(0[1-9]|1[0-2])(0[1-9]|[12]\d|3[01])(?:\s?-\s?|\s)?[1-8]\d{6}(?!\d)", RegexOptions.Compiled), null),
            // Card number with separators: 1234-5678-9012-3456
            (new Regex(@"(?<!\d)\d{4}" + Separator + @"\d{4}" + Separator + @"\d{4}" + Separator + @"\d{4}(?!\d)", RegexOptions.Compiled), null),
            // Card number without separators: 16 digits, the check digit (Luhn) keeps ordinary 16 digit numbers out
            (new Regex(@"(?<!\d)\d{16}(?!\d)", RegexOptions.Compiled), IsLuhnValid),
        };

        /// <summary>
        /// Luhn check of a number
        /// </summary>
        internal static bool IsLuhnValid(string digits)
        {
            int sum = 0;
            bool doubleIt = false;
            for (int i = digits.Length - 1; i >= 0; i--)
            {
                int digit = digits[i] - '0';
                if (digit < 0 || digit > 9)
                {
                    return false;
                }

                if (doubleIt)
                {
                    digit *= 2;
                    if (digit > 9)
                    {
                        digit -= 9;
                    }
                }

                sum += digit;
                doubleIt = !doubleIt;
            }

            return digits.Length > 0 && sum % 10 == 0;
        }

        /// <summary>
        /// The kinds of personal data in a text, as character ranges
        /// </summary>
        public static IEnumerable<(int Start, int Length)> FindRanges(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield break;
            }

            foreach (var (pattern, isValid) in Patterns)
            {
                foreach (Match match in pattern.Matches(text))
                {
                    if (isValid == null || isValid(match.Value))
                    {
                        yield return (match.Index, match.Length);
                    }
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

                // Matches which share a word (e.g. a phone number inside a longer digit group) become one cluster of words, so one box
                var clusters = new List<SortedSet<int>>();
                foreach (var (start, length) in FindRanges(text.ToString()).OrderBy(range => range.Start))
                {
                    int end = start + length;
                    var hit = new SortedSet<int>(Enumerable.Range(0, words.Count)
                        .Where(i => starts[i] < end && starts[i] + (words[i].Text?.Length ?? 0) > start));
                    if (hit.Count == 0)
                    {
                        continue;
                    }

                    foreach (var cluster in clusters.Where(cluster => cluster.Overlaps(hit)).ToList())
                    {
                        hit.UnionWith(cluster);
                        clusters.Remove(cluster);
                    }

                    clusters.Add(hit);
                }

                foreach (var hit in clusters.OrderBy(cluster => cluster.Min))
                {
                    int left = hit.Min(i => words[i].Bounds.Left);
                    int top = hit.Min(i => words[i].Bounds.Top);
                    int right = hit.Max(i => words[i].Bounds.Right);
                    int bottom = hit.Max(i => words[i].Bounds.Bottom);
                    areas.Add((new NativeRect(left, top, right - left, bottom - top), line.Bounds.Height));
                }
            }

            return areas;
        }
    }
}
