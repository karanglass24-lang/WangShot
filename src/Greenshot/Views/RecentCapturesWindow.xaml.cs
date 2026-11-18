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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dapplo.Ini;
using Greenshot.Base.Core;
using Greenshot.Base.Core.Enums;
using Greenshot.Base.Interfaces;
using Greenshot.Base.Threading;
using Greenshot.Base.Wpf;
using Greenshot.Editor.Drawing;
using Greenshot.Editor.Forms;
using Greenshot.ViewModels;
using log4net;

namespace Greenshot.Views
{
    /// <summary>
    /// 왕샷: the recent captures gallery. It shows the newest captures of the output folder as thumbnails, so a capture from
    /// earlier can be opened in the editor again, copied or deleted without looking for the file in the Explorer.
    /// </summary>
    public partial class RecentCapturesWindow : Window, INotifyPropertyChanged
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(RecentCapturesWindow));

        /// <summary>
        /// The gallery shows at most this many captures, the newest first
        /// </summary>
        private const int MaximumCaptures = 60;

        /// <summary>
        /// The thumbnail is decoded this big: enough for the card on a display with 200% scaling
        /// </summary>
        private const int ThumbnailWidth = 384;
        private const int ThumbnailHeight = 240;

        private static readonly HashSet<string> ImageExtensions = new HashSet<string>(new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff" }, StringComparer.OrdinalIgnoreCase);

        private static RecentCapturesWindow _current;

        private readonly ObservableCollection<RecentCaptureItem> _captures = new ObservableCollection<RecentCaptureItem>();
        private CancellationTokenSource _loadCancellation;
        private bool _isLoading;
        private string _folder;

        public RecentCapturesWindow()
        {
            InitializeComponent();
            DataContext = this;

            WpfThemeHelper.ThemeChanged += OnThemeChanged;
            Loaded += OnWindowLoaded;
            Closed += OnWindowClosed;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// The captures of the output folder, the newest first
        /// </summary>
        public ObservableCollection<RecentCaptureItem> Captures => _captures;

        // Theme brushes bound to the XAML
        public SolidColorBrush WindowBackgroundBrush => WpfThemeHelper.WindowBackground;
        public SolidColorBrush CardBackgroundBrush => WpfThemeHelper.CardBackground;
        public SolidColorBrush CardBorderBrush => WpfThemeHelper.CardBorder;
        public SolidColorBrush TextPrimaryBrush => WpfThemeHelper.TextPrimary;
        public SolidColorBrush TextSecondaryBrush => WpfThemeHelper.TextSecondary;
        public SolidColorBrush BadgeBackgroundBrush => WpfThemeHelper.BadgeBackground;
        public SolidColorBrush AccentBrush => WpfThemeHelper.Accent;

        private static ICoreConfiguration CoreConfiguration => IniConfigRegistry.GetSection<ICoreConfiguration>();

        /// <summary>
        /// Show the gallery, or bring the open one to the front
        /// </summary>
        /// <param name="ownerHandle">The hidden window of the shell, so the gallery stays out of the taskbar on its own</param>
        public static void ShowWindow(IntPtr ownerHandle)
        {
            if (_current != null)
            {
                if (_current.WindowState == WindowState.Minimized)
                {
                    _current.WindowState = WindowState.Normal;
                }

                _current.Activate();
                _current.ReloadAsync().FireAndLog("Reload the recent captures", Log);
                return;
            }

            var window = new RecentCapturesWindow();
            if (ownerHandle != IntPtr.Zero)
            {
                _ = new WindowInteropHelper(window) { Owner = ownerHandle };
            }

            window.Closed += (sender, args) =>
            {
                if (ReferenceEquals(_current, window))
                {
                    _current = null;
                }
            };
            _current = window;
            window.Show();
            window.Activate();
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            ReloadAsync().FireAndLog("Load the recent captures", Log);
        }

        private void OnWindowClosed(object sender, EventArgs e)
        {
            WpfThemeHelper.ThemeChanged -= OnThemeChanged;
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            _loadCancellation = null;
            _captures.Clear();
        }

        /// <summary>
        /// Read the output folder and show the thumbnails. A newer reload cancels the one which is running.
        /// </summary>
        private async Task ReloadAsync()
        {
            if (_isLoading)
            {
                return;
            }

            _isLoading = true;
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            var cancellation = _loadCancellation = new CancellationTokenSource();
            var cancellationToken = cancellation.Token;

            RefreshButton.IsEnabled = false;
            StatusText.Text = "불러오는 중…";
            try
            {
                string folder = null;
                string problem = null;
                var configuration = IniConfigRegistry.GetSection<ICoreConfiguration>();
                if (configuration != null)
                {
                    folder = configuration.GetOutputDirectory(out problem);
                }
                if (string.IsNullOrEmpty(folder))
                {
                    SubtitleText.Text = "저장 폴더를 찾지 못했어요.";
                    StatusText.Text = "저장 폴더를 찾지 못했어요.";
                    return;
                }

                _folder = folder;
                SubtitleText.Text = problem == null ? folder : $"{folder} — {problem}";

                // PARALLEL: the folder is scanned next to the UI, a folder full of captures would stall the gallery
#pragma warning disable RS0030 // R10: documented parallel branch
                var items = await Task.Run(() => FindCaptures(folder, cancellationToken), cancellationToken);
#pragma warning restore RS0030
                if (cancellationToken.IsCancellationRequested || !IsLoaded)
                {
                    return;
                }

                // The thumbnails of the previous list can go before the new ones are decoded
                foreach (var old in _captures)
                {
                    old.Thumbnail = null;
                }

                _captures.Clear();
                foreach (var item in items)
                {
                    _captures.Add(item);
                }

                UpdateEmptyState();
                StatusText.Text = items.Count == 0
                    ? "저장 폴더에 캡처가 없어요."
                    : $"{items.Count}개 · 두 번 클릭하면 편집기에서 열려요.";

                // PARALLEL: the thumbnails are decoded next to the UI, a page full of captures would stall the gallery
#pragma warning disable RS0030 // R10: documented parallel branch
                var thumbnails = await Task.Run(() => items.Select(item => (Item: item, Image: DecodeThumbnail(item.FilePath))).ToList(), cancellationToken);
#pragma warning restore RS0030
                if (cancellationToken.IsCancellationRequested || !IsLoaded)
                {
                    return;
                }

                foreach (var (item, image) in thumbnails)
                {
                    item.Thumbnail = image;
                }
            }
            catch (OperationCanceledException)
            {
                // A newer reload took over
            }
            catch (Exception ex)
            {
                Log.Warn("Could not list the recent captures", ex);
                StatusText.Text = "최근 캡처를 불러오지 못했어요.";
            }
            finally
            {
                if (ReferenceEquals(_loadCancellation, cancellation))
                {
                    _isLoading = false;
                    RefreshButton.IsEnabled = true;
                    UpdateButtons();
                }
            }
        }

        /// <summary>
        /// The image files of the folder, the newest first
        /// </summary>
        private static List<RecentCaptureItem> FindCaptures(string folder, CancellationToken cancellationToken)
        {
            var captures = new List<RecentCaptureItem>();
            try
            {
                foreach (var file in new DirectoryInfo(folder).EnumerateFiles()
                             .Where(file => ImageExtensions.Contains(file.Extension))
                             .OrderByDescending(file => file.LastWriteTimeUtc)
                             .Take(MaximumCaptures))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    captures.Add(new RecentCaptureItem(file.FullName, file.LastWriteTime, file.Length));
                }
            }
            catch (DirectoryNotFoundException)
            {
                // The folder went away while the gallery was open: no captures
            }

            return captures;
        }

        /// <summary>
        /// Decode a thumbnail which fits in the card, also for a tall full page capture: first the size, then a smaller decode
        /// </summary>
        private static ImageSource DecodeThumbnail(string path)
        {
            try
            {
                int width;
                int height;
                using (var stream = OpenRead(path))
                {
                    var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                    var frame = decoder.Frames[0];
                    width = frame.PixelWidth;
                    height = frame.PixelHeight;
                }

                if (width <= 0 || height <= 0)
                {
                    return null;
                }

                double scale = Math.Min(1.0, Math.Min((double)ThumbnailWidth / width, (double)ThumbnailHeight / height));
                int decodedWidth = Math.Max(1, (int)Math.Round(width * scale));
                int decodedHeight = Math.Max(1, (int)Math.Round(height * scale));

                var bitmap = new BitmapImage();
                using (var stream = OpenRead(path))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = decodedWidth;
                    bitmap.DecodePixelHeight = decodedHeight;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }

                // Frozen: the background thread may hand it to the UI thread
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                Log.Debug("Could not read the thumbnail of " + path, ex);
                return null;
            }
        }

        /// <summary>
        /// A capture which is being written must not be locked by the gallery
        /// </summary>
        private static FileStream OpenRead(string path) =>
            new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        private void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            ReloadAsync().FireAndLog("Reload the recent captures", Log);
        }

        private void OnOpenFolderClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_folder))
            {
                return;
            }

            try
            {
                ExplorerHelper.OpenInExplorer(_folder);
            }
            catch (Exception ex)
            {
                Log.Warn("Could not open the folder " + _folder, ex);
                StatusText.Text = "저장 폴더를 열지 못했어요.";
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            bool hasSelection = CapturesList.SelectedItem is RecentCaptureItem;
            OpenButton.IsEnabled = hasSelection;
            CopyButton.IsEnabled = hasSelection;
            RevealButton.IsEnabled = hasSelection;
            DeleteButton.IsEnabled = hasSelection;
        }

        private void UpdateEmptyState()
        {
            EmptyText.Visibility = _captures.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnListMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ItemsControl.ContainerFromElement(CapturesList, e.OriginalSource as DependencyObject) is not ListBoxItem item)
            {
                return;
            }

            CapturesList.SelectedItem = item.DataContext;
            OpenSelectedCapture();
        }

        private void OnListKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                OpenSelectedCapture();
            }
            else if (e.Key == Key.Delete)
            {
                e.Handled = true;
                DeleteSelectedCapture();
            }
            else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                AsyncCommand.Run(CopySelectedCaptureAsync, "Copy the capture to the clipboard");
            }
        }

        private void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }

            if (e.Key == Key.F5)
            {
                e.Handled = true;
                ReloadAsync().FireAndLog("Reload the recent captures", Log);
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        }

        private void OnOpenClick(object sender, RoutedEventArgs e)
        {
            OpenSelectedCapture();
        }

        private void OnCopyClick(object sender, RoutedEventArgs e)
        {
            AsyncCommand.Run(CopySelectedCaptureAsync, "Copy the capture to the clipboard");
        }

        private void OnRevealClick(object sender, RoutedEventArgs e)
        {
            if (CapturesList.SelectedItem is not RecentCaptureItem item)
            {
                return;
            }

            try
            {
                ExplorerHelper.OpenInExplorer(item.FilePath);
            }
            catch (Exception ex)
            {
                Log.Warn("Could not show the capture in the Explorer: " + item.FilePath, ex);
                StatusText.Text = "탐색기에서 파일을 보여주지 못했어요.";
            }
        }

        private void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            DeleteSelectedCapture();
        }

        /// <summary>
        /// Open the capture in the editor, like the "열기" of the editor does
        /// </summary>
        private void OpenSelectedCapture()
        {
            if (CapturesList.SelectedItem is not RecentCaptureItem item)
            {
                return;
            }

            System.Drawing.Image image = null;
            try
            {
                image = ImageIO.LoadImage(item.FilePath);
                if (image == null)
                {
                    ThemedMessageBox.Show(this, "이 파일은 이미지로 열 수 없어요.", "최근 캡처", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var surface = new Surface(image);
                surface.CaptureDetails.Title = Path.GetFileNameWithoutExtension(item.FilePath);
                ImageEditorForm.Open(surface);
            }
            catch (Exception ex)
            {
                image?.Dispose();
                Log.Warn("Could not open the capture in the editor: " + item.FilePath, ex);
                ThemedMessageBox.Show(this, "이미지를 편집기에서 열지 못했어요.", "최근 캡처", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Put the capture on the clipboard as image, like the clipboard destination of the editor
        /// </summary>
        private async Task CopySelectedCaptureAsync()
        {
            if (CapturesList.SelectedItem is not RecentCaptureItem item)
            {
                return;
            }

            try
            {
                using var image = ImageIO.LoadImage(item.FilePath);
                if (image == null)
                {
                    ThemedMessageBox.Show(this, "이 파일은 이미지로 열 수 없어요.", "최근 캡처", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await ClipboardService.Current.SetImageAsync(image, new[] { ClipboardFormat.PNG, ClipboardFormat.DIB });
                StatusText.Text = $"'{item.FileName}'을(를) 클립보드에 복사했어요.";
            }
            catch (Exception ex)
            {
                Log.Warn("Could not copy the capture to the clipboard: " + item.FilePath, ex);
                StatusText.Text = "클립보드에 복사하지 못했어요.";
            }
        }

        /// <summary>
        /// Send the capture to the recycle bin, so a mistake can be undone there
        /// </summary>
        private void DeleteSelectedCapture()
        {
            if (CapturesList.SelectedItem is not RecentCaptureItem item)
            {
                return;
            }

            var answer = ThemedMessageBox.Show(this, $"'{item.FileName}'을(를) 휴지통으로 보낼까요?", "최근 캡처", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                    item.FilePath,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                _captures.Remove(item);
                UpdateEmptyState();
                UpdateButtons();
                StatusText.Text = $"'{item.FileName}'을(를) 휴지통으로 보냈어요.";
            }
            catch (Exception ex)
            {
                Log.Warn("Could not delete the capture: " + item.FilePath, ex);
                StatusText.Text = "캡처를 삭제하지 못했어요.";
            }
        }

        private void OnThemeChanged()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.InvokeAsync(NotifyThemeChanged).Task.FireAndLog("Refresh the theme of the recent captures", Log);
                return;
            }

            NotifyThemeChanged();
        }

        private void NotifyThemeChanged()
        {
            OnPropertyChanged(nameof(WindowBackgroundBrush));
            OnPropertyChanged(nameof(CardBackgroundBrush));
            OnPropertyChanged(nameof(CardBorderBrush));
            OnPropertyChanged(nameof(TextPrimaryBrush));
            OnPropertyChanged(nameof(TextSecondaryBrush));
            OnPropertyChanged(nameof(BadgeBackgroundBrush));
            OnPropertyChanged(nameof(AccentBrush));
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
