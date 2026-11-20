using System.Linq;
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
        public void FindRanges_FindsPersonalData(string text, string expected)
        {
            var found = SensitiveInfoMatcher.FindRanges(text).Select(range => text.Substring(range.Start, range.Length)).ToList();
            Assert.Contains(expected, found);
        }

        [Theory]
        [InlineData("2026년 11월 20일")]
        [InlineData("버전 1.5.7")]
        [InlineData("총 12345 건")]
        [InlineData("")]
        [InlineData(null)]
        public void FindRanges_IgnoresOtherText(string text)
        {
            Assert.Empty(SensitiveInfoMatcher.FindRanges(text));
        }
    }
}
