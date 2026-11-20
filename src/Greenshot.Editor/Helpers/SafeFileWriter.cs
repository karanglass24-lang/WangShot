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
                    CopyOver(temp, target, replaceException);
                    keepTemp = false;
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
        /// Copy the new file over the target. The old target is saved first and put back when the copy fails.
        /// </summary>
        /// <exception cref="IOException">when the copy failed: the message has the path of the temporary file, which is kept</exception>
        private static void CopyOver(string temp, string target, Exception replaceException)
        {
            string backup = target + ".~wangshot-" + Guid.NewGuid().ToString("N");
            bool hasBackup = false;
            try
            {
                File.Copy(target, backup, true);
                hasBackup = true;
            }
            catch (Exception)
            {
                // No backup possible (e.g. the target cannot be read): the copy is tried anyway
            }

            try
            {
                File.Copy(temp, target, true);
            }
            catch (Exception copyException)
            {
                var error = new AggregateException(replaceException, copyException);
                if (!hasBackup)
                {
                    throw new IOException("The file could not be replaced, the new file is " + temp, error);
                }

                try
                {
                    File.Copy(backup, target, true);
                }
                catch (Exception)
                {
                    // The old content stays in the backup file
                    throw new IOException("The file could not be replaced, the new file is " + temp + ", the old content is in " + backup, error);
                }

                TryDelete(backup);
                throw new IOException("The file could not be replaced, the new file is " + temp, error);
            }

            if (hasBackup)
            {
                TryDelete(backup);
            }
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
