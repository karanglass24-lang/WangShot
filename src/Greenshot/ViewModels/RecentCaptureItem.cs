/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2007-2026 Thomas Braun, Jens Klingen, Robin Krom
 *
 * For more information see: https://getgreenshot.org/
 * The Greenshot project is hosted on GitHub https://github.com/greenshot/greenshot
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Greenshot.ViewModels
{
    /// <summary>
    /// 왕샷: a capture of the output folder in the recent captures gallery: its file and, when it is loaded, its thumbnail.
    /// </summary>
    public sealed class RecentCaptureItem : INotifyPropertyChanged
    {
        private ImageSource _thumbnail;

        public RecentCaptureItem(string filePath, DateTime modified, long length)
        {
            FilePath = filePath;
            Modified = modified;
            Length = length;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string FilePath { get; }

        public string FileName => Path.GetFileName(FilePath);

        public DateTime Modified { get; }

        public long Length { get; }

        /// <summary>
        /// The date and the size, under the file name in the gallery
        /// </summary>
        public string DetailText => $"{Modified:yyyy-MM-dd HH:mm} · {FormatSize(Length)}";

        /// <summary>
        /// The thumbnail, null until it is loaded
        /// </summary>
        public ImageSource Thumbnail
        {
            get => _thumbnail;
            set
            {
                if (ReferenceEquals(_thumbnail, value))
                {
                    return;
                }

                _thumbnail = value;
                OnPropertyChanged();
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1024 * 1024)
            {
                return $"{bytes / 1024.0 / 1024.0:0.#}MB";
            }

            return $"{Math.Max(1, bytes / 1024)}KB";
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
