using System.Collections.Generic;
using System.Linq;
using Dapplo.Windows.Common.Structs;
using Greenshot.Base.Core;
using Greenshot.Base.Interfaces.Plugin;
using Greenshot.Editor.Helpers;
using Xunit;

namespace Greenshot.Tests.Editor
{
    public class SensitiveInfoMatcherTests
    {
        [Theory]
        [InlineData("연락처 010-1234-5678 입니다", "010-1234-5678")]
        [InlineData("연락처 01012345678", "01012345678")]
        [InlineData("전화 02-123-4567", "02-123-4567")]
        [InlineData("mail: hong.gildong+x@example.co.kr 로", "hong.gildong+x@example.co.kr")]
        [InlineData("주민 900101-1234567", "900101-1234567")]
        [InlineData("카드 4111-1111-1111-1111", "4111-1111-1111-1111")]
        [InlineData("카드 5412 1999 2024 0001", "5412 1999 2024 0001")]
        [InlineData("카드 4000 1900 2000 1234", "4000 1900 2000 1234")]
        [InlineData("1.010-1234-5678", "010-1234-5678")]
        [InlineData("02 2072 0505", "02 2072 0505")]
        [InlineData("02 2019 1231", "02 2019 1231")]
        [InlineData("+82 507-1234-5678", "+82 507-1234-5678")]
        [InlineData("카드 4111-1111-1111-1112", "4111-1111-1111-1112")]
        [InlineData("카드 4111.1111.1111.1111", "4111.1111.1111.1111")]
        [InlineData("1. 010-1234-5678", "010-1234-5678")]
        [InlineData("2. 02-123-4567", "02-123-4567")]
        [InlineData("2026.11.20. 010-1234-5678", "010-1234-5678")]
        [InlineData("11.20. 010-1234-5678", "010-1234-5678")]
        [InlineData("1. 4111-1111-1111-1111", "4111-1111-1111-1111")]
        [InlineData("1. 3782 822463 10005", "3782 822463 10005")]
        [InlineData("010-2024-1120", "010-2024-1120")]
        [InlineData("010-1988-0815", "010-1988-0815")]
        [InlineData("010 1999 0505", "010 1999 0505")]
        [InlineData("02-2072-0505", "02-2072-0505")]
        [InlineData("02-2000-0101", "02-2000-0101")]
        [InlineData("031-2019-1231", "031-2019-1231")]
        [InlineData("050-1234-5678", "050-1234-5678")]
        [InlineData("카드 4111 1111 1111 1111", "4111 1111 1111 1111")]
        [InlineData("Amex 3782 822463 10005", "3782 822463 10005")]
        [InlineData("Amex 378282246310005", "378282246310005")]
        [InlineData("+82 10-1234-5678", "+82 10-1234-5678")]
        [InlineData("+82-10-1234-5678", "+82-10-1234-5678")]
        [InlineData("(02) 123-4567", "(02) 123-4567")]
        [InlineData("02)123-4567", "02)123-4567")]
        [InlineData("주민 9001011234568", "9001011234568")]
        [InlineData("010- 1234- 5678", "010- 1234- 5678")]
        [InlineData("010 -1234 -5678", "010 -1234 -5678")]
        [InlineData("전화 02- 123- 4567", "02- 123- 4567")]
        [InlineData("카드 4111111111111111", "4111111111111111")]
        public void FindRanges_FindsPersonalData(string text, string expected)
        {
            var found = SensitiveInfoMatcher.FindRanges(text).Select(range => text.Substring(range.Start, range.Length)).ToList();
            Assert.Contains(expected, found);
        }

        [Theory]
        [InlineData("2026년 11월 20일")]
        [InlineData("버전 1.5.7")]
        [InlineData("총 12345 건")]
        [InlineData("번호 1234567890123456")]
        [InlineData("2026. 11. 20")]
        [InlineData("2026-11-20 12:30")]
        [InlineData("버전 1.5.7.0")]
        [InlineData("0123456789")]
        [InlineData("1920 1080 2560 1440")]
        [InlineData("1920.1080.2560.1440")]
        [InlineData("1000 2000 3000 4000")]
        [InlineData("0001 0002 0003 0004")]
        [InlineData("2024-1120-1530-0045")]
        [InlineData("v2024.1120.1530.0045")]
        [InlineData("20241120-1530-0045-1234")]
        [InlineData("1234.5678 9012.3456")]
        [InlineData("1234-5678-9012-3456-7890")]
        [InlineData("ABCD-1234-5678-9012-3456")]
        [InlineData("1999-2000-1234-5678")]
        [InlineData("１２３４ ５６７８ ９０１２ ３４５６")]
        [InlineData("０１０-１２３４-５６７８")]
        [InlineData("٠١٠ ١٢٣٤ ٥٦٧٨")]
        [InlineData("05 - 2024 - 1231")]
        [InlineData("2023 2024 2025 2026")]
        [InlineData("02 2024 1120")]
        [InlineData("8801234567890")]
        [InlineData("192.168.010.123 4567")]
        [InlineData("")]
        [InlineData(null)]
        public void FindRanges_IgnoresOtherText(string text)
        {
            Assert.Empty(SensitiveInfoMatcher.FindRanges(text));
        }

        private static IOcrLineFeature Line(params (string Text, int Left)[] words)
        {
            var infos = words.Select(word => new OcrWordInfo { Text = word.Text, Bounds = new NativeRect(word.Left, 10, 40, 12) }).ToList();
            var bounds = new NativeRect(words[0].Left, 10, words[words.Length - 1].Left + 40 - words[0].Left, 12);
            return new DetectedOcrLine(bounds, string.Join(" ", words.Select(word => word.Text)), infos);
        }

        [Fact]
        public void FindAreas_MatchesSharingAWord_GiveOneMergedArea()
        {
            // The phone number (words 0-2) and the card number (words 1-4) share words: one box, not two overlapping ones
            var line = Line(("010", 0), ("4111", 50), ("1111", 100), ("1111", 150), ("1111", 200));

            var areas = SensitiveInfoMatcher.FindAreas(new[] { line });

            var area = Assert.Single(areas);
            Assert.Equal(0, area.Bounds.Left);
            Assert.Equal(240, area.Bounds.Right);
        }

        [Fact]
        public void FindAreas_SeparateMatches_GiveSeparateAreas()
        {
            var line = Line(("010-1234-5678", 0), ("그리고", 100), ("hong@example.com", 200));

            Assert.Equal(2, SensitiveInfoMatcher.FindAreas(new[] { line }).Count);
        }

        [Fact]
        public void FindAreas_NumberSplitAfterSeparators_IsFound()
        {
            var line = Line(("010-", 0), ("1234-", 50), ("5678", 100), ("끝", 150));

            var area = Assert.Single(SensitiveInfoMatcher.FindAreas(new[] { line }));
            Assert.Equal(0, area.Bounds.Left);
            Assert.Equal(140, area.Bounds.Right);
        }

        [Theory]
        [InlineData("4111111111111111", true)]
        [InlineData("4111111111111112", false)]
        [InlineData("", false)]
        public void IsLuhnValid_ChecksTheCheckDigit(string digits, bool expected)
        {
            Assert.Equal(expected, SensitiveInfoMatcher.IsLuhnValid(digits));
        }

        [Fact]
        public void FindRanges_LongLine_IsFast()
        {
            string digits = new string('1', 50000);
            string withAt = new string('a', 30000) + "@";

            var watch = System.Diagnostics.Stopwatch.StartNew();
            SensitiveInfoMatcher.FindRanges(digits).ToList();
            SensitiveInfoMatcher.FindRanges(withAt).ToList();
            SensitiveInfoMatcher.FindRanges(new string('1', 5000) + " hong@example.com").ToList();

            Assert.True(watch.ElapsedMilliseconds < 3000, "took " + watch.ElapsedMilliseconds + " ms");
        }

        [Fact]
        public void FindRanges_NormalLine_StillFindsTheEmail()
        {
            var found = SensitiveInfoMatcher.FindRanges("mail hong@example.com ok").ToList();

            Assert.Single(found);
        }
    }
}
