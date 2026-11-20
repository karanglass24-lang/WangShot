using System;
using System.Collections.Generic;
using System.Globalization;
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
        // Cards and resident numbers have no dot as separator
        private const string CardSeparator = @"(?:\s?-\s?|\s)?";

        // A number which follows another number with a dot or a dash ("192.168.010.123 4567") is part of that number
        private const string NotAfterNumber = @"(?<!\d)(?<!\d[-.]\s?)";
        private const string NotBeforeNumber = @"(?!\d)(?![-.]\d)";

        // Area codes of Korea: Seoul, the provinces, internet phone and free of charge numbers
        private const string AreaCode = @"(?:02|03[1-3]|04[1-4]|05[1-5]|06[1-4]|070|080)";

        private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(500);

        // Longer lines are not OCR lines of a screenshot, they would only cost time
        private const int MaximumTextLength = 20000;

        private sealed class Pattern
        {
            internal Pattern(string expression, Func<string, bool> isValid = null, char? requiredCharacter = null)
            {
                Regex = new Regex(expression, RegexOptions.Compiled, MatchTimeout);
                IsValid = isValid;
                RequiredCharacter = requiredCharacter;
            }

            internal Regex Regex { get; }

            internal Func<string, bool> IsValid { get; }

            /// <summary>
            /// A character the text needs for a match (a cheap check before a pattern which is slow on long text)
            /// </summary>
            internal char? RequiredCharacter { get; }
        }

        private static readonly Pattern[] Patterns =
        {
            // Mobile phone: 010-1234-5678, 010 1234 5678, 01012345678, (010) 1234-5678
            new Pattern(NotAfterNumber + @"\(?01[016789]\)?" + OptionalSeparator + @"\d{3,4}" + OptionalSeparator + @"\d{4}" + NotBeforeNumber, IsNotADate),
            // Landline: 02-123-4567, 031-1234-5678 (a separator is needed, ten digits alone are an ordinary number)
            new Pattern(NotAfterNumber + AreaCode + Separator + @"\d{3,4}" + Separator + @"\d{4}" + NotBeforeNumber, IsNotADate),
            // Landline with the area code in brackets: (02) 123-4567, 02)123-4567
            new Pattern(NotAfterNumber + @"\(?" + AreaCode + @"\)\s?\d{3,4}" + Separator + @"\d{4}" + NotBeforeNumber, IsNotADate),
            // International: +82 10-1234-5678, +82-10-1234-5678, +82 2-123-4567
            new Pattern(@"(?<![\d+])\+82" + OptionalSeparator + @"\(?0?(?:1[016789]|2|3[1-3]|4[1-4]|5[1-5]|6[1-4]|70)\)?" + OptionalSeparator + @"\d{3,4}" + OptionalSeparator + @"\d{4}(?!\d)"),
            // E-mail (slow on long text: only when there is an @, and with a time limit)
            new Pattern(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", null, '@'),
            // Resident registration number: 900101-1234567 (the date has to exist; without a separator the check digit has to be right,
            // otherwise a bar code number of 13 digits would be one)
            new Pattern(@"(?<!\d)\d{2}(?:0[1-9]|1[0-2])(?:0[1-9]|[12]\d|3[01])" + CardSeparator + @"[1-8]\d{6}(?!\d)", IsResidentNumber),
            // Card number: 4111-1111-1111-1111, 4111111111111111 (the check digit and the first digits of a card are checked, dates and
            // other numbers are not cards)
            new Pattern(NotAfterNumber + @"\d{4}" + CardSeparator + @"\d{4}" + CardSeparator + @"\d{4}" + CardSeparator + @"\d{4}(?!\d)", IsCardNumber),
            // American Express: 3782 822463 10005
            new Pattern(NotAfterNumber + @"3[47]\d{2}" + CardSeparator + @"\d{6}" + CardSeparator + @"\d{5}(?!\d)", IsCardNumber),
        };

        private static string DigitsOf(string text) => new string(text.Where(char.IsDigit).ToArray());

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

        private static bool IsCardNumber(string text)
        {
            string digits = DigitsOf(text);
            if (!IsLuhnValid(digits))
            {
                return false;
            }

            // Cards start with 3 (Amex, JCB, Diners), 4 (Visa), 5 or 2221-2720 (Mastercard), 6 (Discover, UnionPay) or 9 (Korean local cards)
            if ("34569".IndexOf(digits[0]) >= 0)
            {
                return true;
            }

            return int.TryParse(digits.Substring(0, 4), out int prefix) && prefix >= 2221 && prefix <= 2720;
        }

        private static bool IsResidentNumber(string text)
        {
            string digits = DigitsOf(text);
            if (digits.Length != 13)
            {
                return false;
            }

            // The century comes from the 7th digit, the person has to be born already
            int century = digits[6] switch
            {
                '1' or '2' or '5' or '6' => 1900,
                '3' or '4' or '7' or '8' => 2000,
                _ => 0
            };
            if (century == 0)
            {
                return false;
            }

            int year = century + int.Parse(digits.Substring(0, 2), CultureInfo.InvariantCulture);
            int month = int.Parse(digits.Substring(2, 2), CultureInfo.InvariantCulture);
            int day = int.Parse(digits.Substring(4, 2), CultureInfo.InvariantCulture);
            if (year > DateTime.Now.Year || day > DateTime.DaysInMonth(year, month))
            {
                return false;
            }

            // Numbers with a separator are also accepted without check digit (the numbers since 2020 have none)
            if (text.Length != 13)
            {
                return true;
            }

            int[] weights = { 2, 3, 4, 5, 6, 7, 8, 9, 2, 3, 4, 5 };
            int sum = 0;
            for (int i = 0; i < 12; i++)
            {
                sum += (digits[i] - '0') * weights[i];
            }

            return (11 - sum % 11) % 10 == digits[12] - '0';
        }

        /// <summary>
        /// "02 2024 1120" and "02 - 2024 - 1231" are dates (month, year, day), not phone numbers
        /// </summary>
        private static bool IsNotADate(string text)
        {
            var groups = Regex.Matches(text, @"\d+").Cast<Match>().Select(match => match.Value).ToList();
            if (groups.Count != 3 || groups[1].Length != 4 || groups[2].Length != 4)
            {
                return true;
            }

            bool year = groups[1].StartsWith("19", StringComparison.Ordinal) || groups[1].StartsWith("20", StringComparison.Ordinal);
            int month = int.Parse(groups[2].Substring(0, 2), CultureInfo.InvariantCulture);
            int day = int.Parse(groups[2].Substring(2, 2), CultureInfo.InvariantCulture);
            return !(year && month >= 1 && month <= 12 && day >= 1 && day <= 31);
        }

        /// <summary>
        /// The kinds of personal data in a text, as character ranges
        /// </summary>
        public static IEnumerable<(int Start, int Length)> FindRanges(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length > MaximumTextLength)
            {
                return Enumerable.Empty<(int Start, int Length)>();
            }

            var ranges = new List<(int Start, int Length)>();
            foreach (var pattern in Patterns)
            {
                if (pattern.RequiredCharacter.HasValue && text.IndexOf(pattern.RequiredCharacter.Value) < 0)
                {
                    continue;
                }

                try
                {
                    foreach (Match match in pattern.Regex.Matches(text))
                    {
                        if (pattern.IsValid == null || pattern.IsValid(match.Value))
                        {
                            ranges.Add((match.Index, match.Length));
                        }
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    // A text which takes too long for one pattern is skipped for this pattern, the others still run
                }
            }

            return ranges;
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
