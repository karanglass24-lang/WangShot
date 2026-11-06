// Greenshot - a free and open source screenshot tool
// Copyright (C) 2007-2026 Thomas Braun, Jens Klingen, Robin Krom
//
// For more information see: https://getgreenshot.org/
// The Greenshot project is hosted on GitHub https://github.com/greenshot/greenshot
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using Greenshot.Base.Core;
using Dapplo.Ini;
using Greenshot.Base.Interfaces;
using Greenshot.Helpers.Entities;
using log4net;
using Greenshot.Base.Threading;
using Greenshot.Base.Languages;
using Greenshot.Base.Http;

namespace Greenshot.Helpers
{
    /// <summary>
    ///     This processes the information, if there are updates available.
    /// </summary>
    public class UpdateService : IDisposable
    {
        private bool _disposed;
        private readonly object _lifecycleLock = new object();
        private static readonly ILog Log = LogManager.GetLogger(typeof(UpdateService));
        private static readonly ICoreConfiguration CoreConfig = IniConfigRegistry.GetSection<ICoreConfiguration>();
        private static readonly Uri UpdateFeed = WangShotInfo.LatestReleaseApi;
        private static readonly Uri Downloads = WangShotInfo.ReleasesPage;
        // The longest single wait of the background loop, Task.Delay throws above about 24.8 days
        private static readonly TimeSpan MaxWait = TimeSpan.FromDays(1);
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        /// <summary>
        /// URI pointing to the Greenshot downloads webpage, for any other edition than Full with ?edition=light (etc.)
        /// so the page can offer that edition's download
        /// </summary>
        public static Uri DownloadsUri => EditionInfo.IsFull ? Downloads : new Uri($"{Downloads.AbsoluteUri}?edition={Uri.EscapeDataString(EditionInfo.Name.ToLowerInvariant())}");

        /// <summary>
        /// The downloads page for this edition: the one the update feed names for it, otherwise DownloadsUri
        /// </summary>
        public Uri DownloadsUrl => _editionDownloads ?? DownloadsUri;

        private Uri _editionDownloads;

        /// <summary>
        /// Provides the current version
        /// </summary>
        public Version CurrentVersion { get; }

        /// <summary>
        /// Provides the latest known version
        /// </summary>
        public Version LatestReleaseVersion { get; private set; }

        /// <summary>
        /// The latest beta version
        /// </summary>
        public Version LatestBetaVersion { get; private set; }

        /// <summary>
        /// Checks if there is an release update available
        /// </summary>
        public bool IsUpdateAvailable => LatestReleaseVersion != null && LatestReleaseVersion > CurrentVersion;

        /// <summary>
        /// Checks if there is an beta update available
        /// </summary>
        public bool IsBetaUpdateAvailable => LatestBetaVersion != null && LatestBetaVersion > CurrentVersion;

        /// <summary>
        /// Indicates whether the background update check task is currently running.
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Keep track of when the update was shown, so it won't be every few minutes
        /// </summary>
        public DateTimeOffset LastUpdateShown = DateTimeOffset.MinValue;

        /// <summary>
        /// Constructor with dependencies
        /// </summary>
        public UpdateService() : this(null)
        {
        }

        /// <summary>
        /// Constructor allowing explicit CurrentVersion (useful for unit testing and dependency injection)
        /// </summary>
        /// <param name="currentVersion">Current version override</param>
        public UpdateService(Version currentVersion)
        {
            if (currentVersion != null)
            {
                CurrentVersion = currentVersion;
            }
            else
            {
                // 왕샷: the version of 왕샷 itself, not the one of Greenshot
                CurrentVersion = WangShotInfo.CurrentVersion;
            }
        }

        /// <summary>
        /// Start the background task which checks for updates
        /// </summary>
        public void Startup()
        {
            lock (_lifecycleLock)
            {
                if (IsRunning)
                {
                    return;
                }

                if (_disposed)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    _disposed = false;
                }

                IsRunning = true;
                var interval = CoreConfig?.UpdateCheckInterval ?? 14;
                BackgroundTaskAsync(() => TimeSpan.FromDays(interval), ct => CheckForUpdatesAsync(true, ct), _cancellationTokenSource.Token).FireAndLog("Update check", Log);
            }
        }

        /// <summary>
        /// Cancels the background task and releases resources.
        /// </summary>
        public void Dispose()
        {
            lock (_lifecycleLock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
            }
        }

        /// <summary>
        /// This runs a periodic task in the background
        /// </summary>
        /// <param name="intervalFactory">Func which returns a TimeSpan</param>
        /// <param name="reoccurringTask">Func which returns a task</param>
        /// <param name="cancellationToken">CancellationToken</param>
        /// <returns>Task</returns>
        private async Task BackgroundTaskAsync(Func<TimeSpan> intervalFactory, Func<CancellationToken, Task> reoccurringTask, CancellationToken cancellationToken = default)
        {
            try
            {
                // Initial delay, to make sure this doesn't happen at the startup
                await Task.Delay(20000, cancellationToken).ConfigureAwait(false);
                Log.Info("Starting background task to check for updates");
                // Task.Delay with ConfigureAwait(false) continues on the thread pool
                {
                    try
                    {
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            var interval = intervalFactory();
                            var task = reoccurringTask;

                            // If the check is disabled, handle that here
                            var checkIsDisabled = TimeSpan.Zero == interval;
                            var nextCheckIsInTheFuture = CoreConfig != null && CoreConfig.LastUpdateCheck.Add(interval) > DateTime.Now;

                            // If we have an invalid interval
                            if (interval.TotalSeconds < 0)
                            {
                                // Just wait for longer time, maybe the configuration will change
                                interval = TimeSpan.FromDays(1);
                            }

                            if (checkIsDisabled || nextCheckIsInTheFuture)
                            {
                                // Just wait for 30 minutes, maybe the configuration will change
                                interval = TimeSpan.FromMinutes(30);
                                task = c => Task.FromResult(true);
                            }

                            try
                            {
                                await task(cancellationToken).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                Log.Error("Error occurred when trying to check for updates.", ex);
                            }

                            try
                            {
                                // Use duration to get an absolute time and can't be negative. At most a day: Task.Delay takes up to
                                // about 24.8 days, the interval can be 365 days; the next round checks again whether the check is due.
                                var wait = interval.Duration();
                                await Task.Delay(wait > MaxWait ? MaxWait : wait, cancellationToken).ConfigureAwait(false);
                            }
                            catch (TaskCanceledException)
                            {
                                // Ignore, this always happens
                            }
                            catch (Exception ex)
                            {
                                Log.Error("Error occurred await for the next background interval check.", ex);
                                // Safety pause, to avoid a potential tight loop if something is really wrong with the configuration or the update feed.
                                await Task.Delay(TimeSpan.FromDays(1), cancellationToken).ConfigureAwait(false);
                            }
                        }
                    }
                    finally
                    {
                        Log.Info("Stopping background task to check for updates");
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            finally
            {
                lock (_lifecycleLock)
                {
                    IsRunning = false;
                }
            }
        }

        /// <summary>
        /// Check for updates asynchronously from the Greenshot update feed.
        /// </summary>
        /// <param name="showNotification">Whether to show a toast notification if an update is found.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the update check completed successfully, false if an error occurred (e.g. offline).</returns>
        public async Task<bool> CheckForUpdatesAsync(bool showNotification = false, CancellationToken cancellationToken = default)
        {
            Log.InfoFormat("Checking for updates from {0}", UpdateFeed);

            try
            {
                if (CoreConfig != null)
                {
                    CoreConfig.LastUpdateCheck = DateTime.Now;
                }

                var release = await FetchLatestReleaseAsync(cancellationToken).ConfigureAwait(false);
                if (release == null)
                {
                    return false;
                }

                ProcessRelease(release);
                Log.InfoFormat("Latest release {0}, installed {1}, installer {2}", LatestReleaseVersion, CurrentVersion, _setupUrl);
            }
            catch (Exception ex)
            {
                Log.Warn("Error occurred when checking for updates.", ex);
                return false;
            }

            if (showNotification && DateTimeOffset.Now.AddDays(-1) > LastUpdateShown)
            {
                if (IsBetaUpdateAvailable)
                {
                    LastUpdateShown = DateTimeOffset.Now;
                    ShowUpdate(LatestBetaVersion);
                }
                else if (IsUpdateAvailable)
                {
                    LastUpdateShown = DateTimeOffset.Now;
                    ShowUpdate(LatestReleaseVersion);
                }
            }

            return true;
        }

        /// <summary>
        /// This takes care of creating the toast view model, publishing it, and disposing afterwards
        /// </summary>
        /// <param name="newVersion">Version</param>
        private void ShowUpdate(Version newVersion)
        {
            Log.InfoFormat("Asking about the update to {0}", newVersion);
            PromptAndInstallAsync(newVersion).FireAndLog("Show the update", Log);
        }

        // ---- 왕샷: update from the releases of the GitHub repository ------------------------------------------------

        private sealed class GitHubAsset
        {
            [JsonPropertyName("name")] public string Name { get; set; }

            [JsonPropertyName("browser_download_url")] public string Url { get; set; }

            [JsonPropertyName("size")] public long Size { get; set; }
        }

        private sealed class GitHubRelease
        {
            [JsonPropertyName("tag_name")] public string TagName { get; set; }

            [JsonPropertyName("body")] public string Body { get; set; }

            [JsonPropertyName("draft")] public bool Draft { get; set; }

            [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }

            [JsonPropertyName("assets")] public List<GitHubAsset> Assets { get; set; }
        }

        private string _releaseNotes;
        private Uri _setupUrl;

        private static async Task<GitHubRelease> FetchLatestReleaseAsync(CancellationToken cancellationToken)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WangShot-Updater");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            using var response = await client.GetAsync(UpdateFeed, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<GitHubRelease>(json);
        }

        /// <summary>
        /// The version, the notes and the installer of the newest release
        /// </summary>
        private void ProcessRelease(GitHubRelease release)
        {
            if (release.Draft || release.Prerelease || string.IsNullOrEmpty(release.TagName))
            {
                return;
            }

            if (!Version.TryParse(release.TagName.Trim().TrimStart('v', 'V'), out var version))
            {
                return;
            }

            LatestReleaseVersion = version;
            _releaseNotes = release.Body;
            _setupUrl = null;
            foreach (var asset in release.Assets ?? new List<GitHubAsset>())
            {
                // Only the installer of 왕샷, from GitHub over https
                if (asset.Name != null && Regex.IsMatch(asset.Name, @"^WangShot-[0-9.]+-setup\.exe$", RegexOptions.IgnoreCase)
                    && Uri.TryCreate(asset.Url, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps
                    && url.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
                {
                    _setupUrl = url;
                    break;
                }
            }
        }

        /// <summary>
        /// True when this copy was installed with the setup (it has an uninstaller), a copy from a zip can't be updated by the setup
        /// </summary>
        private static bool IsInstalledCopy() => File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "unins000.exe"));

        /// <summary>
        /// Check for an update when the user asks for it, and tell the result
        /// </summary>
        public async Task CheckNowAsync()
        {
            bool ok = await CheckForUpdatesAsync(false).ConfigureAwait(false);
            if (!ok)
            {
                await ShowMessageAsync("업데이트를 확인하지 못했어요. 인터넷 연결을 확인해 주세요.", MessageBoxIcon.Warning).ConfigureAwait(false);
            }
            else if (IsUpdateAvailable)
            {
                await PromptAndInstallAsync(LatestReleaseVersion).ConfigureAwait(false);
            }
            else
            {
                await ShowMessageAsync($"이미 최신 버전이에요. (왕샷 {CurrentVersion})", MessageBoxIcon.Information).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// The question or message of the update, in the style of 왕샷's windows, on top of the other windows
        /// </summary>
        /// <returns>the index of the clicked button, -1 when the dialog was closed</returns>
        private static Task<int> ShowChoiceAsync(string text, MessageBoxIcon icon, params string[] buttons)
        {
            var image = icon == MessageBoxIcon.Warning ? System.Windows.MessageBoxImage.Warning : System.Windows.MessageBoxImage.Information;
            return UiDispatcher.Current.InvokeAsync(() => Greenshot.Base.Wpf.ThemedMessageBox.ShowChoice(null, "왕샷 업데이트", text, image, buttons, defaultIndex: 0, cancelIndex: -1, onTop: true));
        }

        private static Task ShowMessageAsync(string text, MessageBoxIcon icon) => ShowChoiceAsync(text, icon, "확인");

        /// <summary>
        /// Ask whether to update, then download the installer and run it: it closes 왕샷, updates it and starts it again
        /// </summary>
        private async Task PromptAndInstallAsync(Version newVersion)
        {
            string notes = (_releaseNotes ?? string.Empty).Trim();
            if (notes.Length > 600)
            {
                notes = notes.Substring(0, 600) + "…";
            }

            bool canInstall = _setupUrl != null && IsInstalledCopy();
            string question = canInstall ? "지금 업데이트할까요?" : "다운로드 페이지를 열까요?";
            int answer = await ShowChoiceAsync($"왕샷 {newVersion} 버전이 나왔어요. (현재 {CurrentVersion})\n\n{notes}\n\n{question}", MessageBoxIcon.Information, "예", "나중에").ConfigureAwait(false);
            Log.InfoFormat("The answer to the update question: {0}", answer);
            if (answer != 0)
            {
                return;
            }

            if (!canInstall)
            {
                Process.Start(new ProcessStartInfo(DownloadsUrl.AbsoluteUri) { UseShellExecute = true })?.Dispose();
                return;
            }

            try
            {
                string folder = Path.Combine(Path.GetTempPath(), "WangShot-update");
                Directory.CreateDirectory(folder);
                string setupPath = Path.Combine(folder, Path.GetFileName(_setupUrl.LocalPath));
                using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("WangShot-Updater");
                    using var response = await client.GetAsync(_setupUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    using var file = File.Create(setupPath);
                    await response.Content.CopyToAsync(file).ConfigureAwait(false);
                }

                if (new FileInfo(setupPath).Length < 1024 * 1024)
                {
                    throw new InvalidDataException("The downloaded installer is too small.");
                }

                Log.InfoFormat("Starting the installer {0}", setupPath);
                Process.Start(new ProcessStartInfo(setupPath, "/SILENT /NORESTART /CLOSEAPPLICATIONS /relaunch=1") { UseShellExecute = true })?.Dispose();
                // The installer closes 왕샷 too, this makes sure the files are free
                SimpleServiceProvider.Current?.GetInstance<IGreenshotShell>(isOptional: true)?.Exit();
            }
            catch (Exception ex)
            {
                Log.Warn("The update failed", ex);
                await ShowMessageAsync("업데이트하지 못했어요. 다운로드 페이지에서 직접 받아 주세요.", MessageBoxIcon.Warning).ConfigureAwait(false);
                Process.Start(new ProcessStartInfo(DownloadsUrl.AbsoluteUri) { UseShellExecute = true })?.Dispose();
            }
        }

        /// <summary>
        /// Process the update feed to get the latest version
        /// </summary>
        /// <param name="updateFeed">Update feed entity</param>
        public void ProcessFeed(UpdateFeed updateFeed)
        {
            if (updateFeed == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(updateFeed.CurrentReleaseVersion))
            {
                var latestReleaseString = Regex.Replace(updateFeed.CurrentReleaseVersion, "[a-zA-Z\\-]*", "");
                if (Version.TryParse(latestReleaseString, out var latestReleaseVersion))
                {
                    LatestReleaseVersion = latestReleaseVersion;
                }
            }

            if (updateFeed.Downloads != null && !EditionInfo.IsFull)
            {
                var edition = EditionInfo.Name.ToLowerInvariant();
                foreach (var download in updateFeed.Downloads)
                {
                    if (string.Equals(download.Key, edition, StringComparison.OrdinalIgnoreCase)
                        && Uri.TryCreate(download.Value, UriKind.Absolute, out var downloadsUri) && downloadsUri.Scheme == Uri.UriSchemeHttps)
                    {
                        _editionDownloads = downloadsUri;
                    }
                }
            }

            if (!string.IsNullOrEmpty(updateFeed.CurrentBetaVersion))
            {
                var latestBetaString = Regex.Replace(updateFeed.CurrentBetaVersion, "[a-zA-Z\\-]*", "");
                if (Version.TryParse(latestBetaString, out var latestBetaVersion))
                {
                    LatestBetaVersion = latestBetaVersion;
                }
            }
        }
    }
}