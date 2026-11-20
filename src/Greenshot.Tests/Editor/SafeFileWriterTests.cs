using System;
using System.IO;
using System.Linq;
using Greenshot.Editor.Helpers;
using Xunit;

namespace Greenshot.Tests.Editor
{
    /// <summary>
    /// 왕캡: the PDF save must not leave a partial or empty file when the plugin fails after it created the file
    /// </summary>
    public class SafeFileWriterTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "wangshot-safewriter-" + Guid.NewGuid().ToString("N"));

        public SafeFileWriterTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            Directory.Delete(_folder, true);
        }

        [Fact]
        public void PrimaryFails_AfterCreatingTheFile_FallbackResultIsTheTarget_NoTempFileLeft()
        {
            string target = Path.Combine(_folder, "out.pdf");

            SafeFileWriter.Write(target,
                temp =>
                {
                    File.WriteAllText(temp, "partial");
                    throw new InvalidOperationException("plugin failed");
                },
                temp => File.WriteAllText(temp, "built in"));

            Assert.Equal("built in", File.ReadAllText(target));
            Assert.Equal(new[] { target }, Directory.GetFiles(_folder));
        }

        [Fact]
        public void PrimaryWorks_ReplacesAnExistingTarget()
        {
            string target = Path.Combine(_folder, "out.pdf");
            File.WriteAllText(target, "old");

            SafeFileWriter.Write(target, temp => File.WriteAllText(temp, "new"), temp => throw new InvalidOperationException("not used"));

            Assert.Equal("new", File.ReadAllText(target));
            Assert.Single(Directory.GetFiles(_folder));
        }

        [Fact]
        public void BothFail_LeavesNoFile_AndKeepsAnExistingTarget()
        {
            string target = Path.Combine(_folder, "out.pdf");
            File.WriteAllText(target, "old");

            Assert.Throws<InvalidOperationException>(() => SafeFileWriter.Write(target,
                temp =>
                {
                    File.WriteAllText(temp, "partial");
                    throw new InvalidOperationException("plugin failed");
                },
                temp =>
                {
                    File.WriteAllText(temp, "partial");
                    throw new InvalidOperationException("export failed");
                }));

            Assert.Equal("old", File.ReadAllText(target));
            Assert.Single(Directory.GetFiles(_folder));
        }

        [Fact]
        public void PrimaryWritesAnEmptyFile_FallbackIsUsed()
        {
            string target = Path.Combine(_folder, "out.pdf");

            SafeFileWriter.Write(target, temp => File.WriteAllBytes(temp, new byte[0]), temp => File.WriteAllText(temp, "built in"));

            Assert.Equal("built in", File.ReadAllText(target));
        }

        [Fact]
        public void NoTarget_WhenNothingWasWritten()
        {
            string target = Path.Combine(_folder, "out.pdf");

            Assert.ThrowsAny<Exception>(() => SafeFileWriter.Write(target, temp => throw new InvalidOperationException("x"), null));

            Assert.False(Directory.GetFiles(_folder).Any());
        }

        [Fact]
        public void TargetOpenInAnotherProgram_WhichAllowsWriting_IsStillReplaced()
        {
            string target = Path.Combine(_folder, "out.pdf");
            File.WriteAllText(target, "old");
            using (new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                SafeFileWriter.Write(target, temp => File.WriteAllText(temp, "new"), null);
            }

            Assert.Equal("new", File.ReadAllText(target));
            Assert.Single(Directory.GetFiles(_folder));
        }

        [Theory]
        [InlineData(FileShare.None)]
        [InlineData(FileShare.Read)]
        public void TargetLockedAgainstWriting_FailsAtOnce_NothingIsLeftOver_AndTheOriginalStays(FileShare share)
        {
            string target = Path.Combine(_folder, "out.pdf");
            File.WriteAllText(target, "old");
            IOException error;
            using (new FileStream(target, FileMode.Open, FileAccess.Read, share))
            {
                error = Assert.Throws<IOException>(() => SafeFileWriter.Write(target, temp => File.WriteAllText(temp, "new"), null));
            }

            Assert.Equal("old", File.ReadAllText(target));
            Assert.Equal(new[] { target }, Directory.GetFiles(_folder));
            Assert.Contains("저장하지 못했어요", error.Message);
            Assert.Contains("바뀌지 않았어요", error.Message);
        }

        [Fact]
        public void ReadOnlyTarget_FailsAtOnce_NothingIsLeftOver()
        {
            string target = Path.Combine(_folder, "out.pdf");
            File.WriteAllText(target, "old");
            File.SetAttributes(target, FileAttributes.ReadOnly);
            try
            {
                var error = Assert.Throws<IOException>(() => SafeFileWriter.Write(target, temp => File.WriteAllText(temp, "new"), null));

                Assert.Contains("읽기 전용", error.Message);
                Assert.Equal("old", File.ReadAllText(target));
                Assert.Equal(new[] { target }, Directory.GetFiles(_folder));
            }
            finally
            {
                File.SetAttributes(target, FileAttributes.Normal);
            }
        }
    }
}
