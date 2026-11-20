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

                if (File.Exists(target))
                {
                    File.Replace(temp, target, null);
                }
                else
                {
                    File.Move(temp, target);
                }
            }
            finally
            {
                TryDelete(temp);
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
