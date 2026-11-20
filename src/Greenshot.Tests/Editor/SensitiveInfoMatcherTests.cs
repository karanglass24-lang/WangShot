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
        [InlineData("카드 1234-5678-9012-3456", "1234-5678-9012-3456")]
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
            var line = Line(("010", 0), ("1234", 50), ("5678", 100), ("9012", 150), ("3456", 200));

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
    }
}
