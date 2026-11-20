using System;
using System.IO;

namespace Greenshot.Editor.Helpers
{
    /// <summary>
    /// 왕캡: writes a file so that a failed writer never leaves a partial or empty file at the target path.
    /// The file is written to a temporary file in the same folder and moved over the target only when it was written completely.
    /// </summary>
    internal static class SafeFileWriter
    {
        /// <summary>
        /// Write the target with <paramref name="primary"/>; when that fails, with <paramref name="fallback"/> (when it is not null).
        /// The writers get the path of a temporary file (with the extension of the target). They have to create a non empty file.
        /// </summary>
        /// <exception cref="Exception">the exception of the last writer, when no writer worked; the target is not touched then</exception>
        internal static void Write(string target, Action<string> primary, Action<string> fallback, Action<Exception> primaryFailed = null)
        {
            string folder = Path.GetDirectoryName(Path.GetFullPath(target));
            string temp = Path.Combine(folder, "~wangshot-" + Guid.NewGuid().ToString("N") + Path.GetExtension(target));
            bool keepTemp = false;
            try
            {
                try
                {
                    primary(temp);
                    EnsureWritten(temp);
                }
                catch (Exception exception) when (fallback != null)
                {
                    primaryFailed?.Invoke(exception);
                    TryDelete(temp);
                    fallback(temp);
                    EnsureWritten(temp);
                }

                if (!File.Exists(target))
                {
                    File.Move(temp, target);
                    return;
                }

                try
                {
                    File.Replace(temp, target, null);
                }
                catch (Exception replaceException)
                {
                    // File.Replace needs more than writing: it fails when the target is open in another program (which allows writing, but not
                    // deleting). Then the content is copied over the target, as writing the file directly did before.
                    keepTemp = true;
                    keepTemp = CopyOver(temp, target, replaceException);
                }
            }
            finally
            {
                if (!keepTemp)
                {
                    TryDelete(temp);
                }
            }
        }

        /// <summary>
        /// Can the target be written (not read-only, not locked against writing)? Nothing is changed.
        /// </summary>
        private static bool CanWrite(string target)
        {
            try
            {
                if ((File.GetAttributes(target) & FileAttributes.ReadOnly) != 0)
                {
                    return false;
                }

                using var stream = new FileStream(target, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static (long Length, DateTime LastWrite)? StateOf(string path)
        {
            try
            {
                var info = new FileInfo(path);
                return (info.Length, info.LastWriteTimeUtc);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Copy the new file over the target. The old target is saved first and put back when the copy fails.
        /// Nothing is left in the folder except, when the copy failed, the temporary file: it is then the only copy of the new content.
        /// </summary>
        /// <returns>true when the temporary file has to be kept (never: an exception is thrown then)</returns>
        /// <exception cref="IOException">when the target could not be replaced; the message (Korean, it is shown to the user) says what is where</exception>
        private static bool CopyOver(string temp, string target, Exception replaceException)
        {
            // A target which cannot be written (a viewer has it open, read-only): nothing is changed, no backup, no leftover
            if (!CanWrite(target))
            {
                TryDelete(temp);
                throw new IOException("다른 프로그램이 파일을 열고 있거나 읽기 전용이라 저장하지 못했어요. 원래 파일은 바뀌지 않았어요: " + target, replaceException);
            }

            string backup = target + ".~wangshot-" + Guid.NewGuid().ToString("N");
            bool hasBackup = false;
            try
            {
                File.Copy(target, backup, true);
                hasBackup = true;
            }
            catch (Exception)
            {
                // No backup possible: the copy is tried anyway
                TryDelete(backup);
            }

            var before = StateOf(target);
            try
            {
                File.Copy(temp, target, true);
            }
            catch (Exception copyException)
            {
                var error = new AggregateException(replaceException, copyException);
                bool unchanged = before.HasValue && StateOf(target) == before;
                bool restored = unchanged;
                if (!unchanged && hasBackup)
                {
                    try
                    {
                        File.Copy(backup, target, true);
                        restored = true;
                    }
                    catch (Exception)
                    {
                        // The old content stays in the backup file
                    }
                }

                if (restored)
                {
                    if (hasBackup)
                    {
                        TryDelete(backup);
                    }

                    throw new IOException("파일을 덮어쓰지 못했어요. 원래 파일은 그대로예요. 새 내용은 이 파일에 저장해 두었어요: " + temp, error);
                }

                string oldContent = hasBackup ? " 원래 내용은 이 파일에 있어요: " + backup + "." : string.Empty;
                throw new IOException("파일을 덮어쓰다 실패했고 원래 내용을 되돌리지 못했어요." + oldContent + " 새 내용은 이 파일에 있어요: " + temp, error);
            }

            if (hasBackup)
            {
                TryDelete(backup);
            }

            return false;
        }

        private static void EnsureWritten(string path)
        {
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
            {
                throw new IOException("The file was not written: " + path);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
                // A leftover temporary file is not worth a failure
            }
        }
    }
}
