/*
 * Copyright (C) 2026 Ardysa
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
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.DependencyInjection;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Services.Localization;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Cache;
using ArdysaModsTools.Core.Services.Config;
using ArdysaModsTools.Models;
using ArdysaModsTools.UI.Helpers;
using ArdysaModsTools.UI.Interfaces;
using ArdysaModsTools.UI.Presenters;

namespace ArdysaModsTools.UI.Forms
{
    public partial class HeroGalleryForm : Form, IHeroGalleryView
    {
        private WebView2? _webView;
        private bool _initialized;
        private TaskCompletionSource<bool>? _alertDismissed;
        private TaskCompletionSource<bool>? _confirmBaseNoSet;

        private int _generationLogViews;

        private static readonly TimeSpan DialogCallbackTimeout = TimeSpan.FromSeconds(60);

        private List<HeroModel> _heroes = new();
        private Dictionary<string, HeroSelectionState> _selections = new();
        private HashSet<string> _favorites = new(StringComparer.OrdinalIgnoreCase);
        private readonly HeroService _heroService;
        private readonly IConfigService _configService;
        private readonly HeroGalleryPresenter _presenter;

        public ModGenerationResult? GenerationResult { get; private set; }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public HeroGalleryForm(IConfigService configService)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            
            InitializeComponent();
            SetupForm();

            var baseFolder = AppDomain.CurrentDomain.BaseDirectory;
            _heroService = new HeroService(baseFolder);

            _presenter = new HeroGalleryPresenter(new HeroGenerationService(), _configService);
            _presenter.SetView(this);

            var loadedFav = FavoritesStore.Load();
            _favorites = loadedFav ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            this.Shown += async (s, e) => await InitializeAsync();
            Helpers.DpiLayout.AttachClamp(this);
            this.FormClosing += (s, e) => FavoritesStore.Save(_favorites);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.None;
            this.ClientSize = new Size(1200, 800);
            this.Name = "HeroGalleryForm";
            this.Text = "Select Hero Sets - AMT 2.0";
            this.ResumeLayout(false);
        }

        private void SetupForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.MinimumSize = new Size(800, 600);
            this.BackColor = Theme.Canvas;
            this.StartPosition = FormStartPosition.Manual;

            AppIconHelper.Apply(this);

            this.Load += (s, e) =>
            {
                if (this.Owner != null)
                {
                    this.Left = this.Owner.Left + (this.Owner.Width - this.Width) / 2;
                    this.Top = this.Owner.Top + (this.Owner.Height - this.Height) / 2;
                }
                else
                {
                    var screen = Screen.FromControl(this);
                    this.Left = screen.WorkingArea.Left + (screen.WorkingArea.Width - this.Width) / 2;
                    this.Top = screen.WorkingArea.Top + (screen.WorkingArea.Height - this.Height) / 2;
                }
            };

            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = Theme.Canvas
            };
            this.Controls.Add(_webView);

            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    Close();
                    e.Handled = true;
                }
            };
        }

        private async Task InitializeAsync()
        {
            if (_initialized) return;

            try
            {
                var env = await WebView2EnvironmentHelper.CreateEnvironmentAsync();

                await _webView!.EnsureCoreWebView2Async(env);
                Helpers.DpiLayout.PinTo100(this, _webView!);
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled =
                    System.Diagnostics.Debugger.IsAttached;

                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                WebViewAssetInterceptor.Attach(_webView.CoreWebView2, env, EnvironmentConfig.ContentBase);

                string html = GetGalleryHtml();
                _webView.CoreWebView2.NavigateToString(Helpers.WebViewTheming.Apply(html));

                var tcs = new TaskCompletionSource<bool>();
                void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
                {
                    _webView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
                    tcs.TrySetResult(e.IsSuccess);
                }
                _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;

                var timeoutTask = Task.Delay(15000);
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
                if (completedTask == timeoutTask)
                {
                    throw new TimeoutException("WebView2 navigation timeout");
                }

                await Task.Delay(200);
                _initialized = true;

                if (Loc.Service != null)
                    await _webView.CoreWebView2.ExecuteScriptAsync(WebViewLocalizer.BuildBootstrapScript(Loc.Service));

                var version = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion ?? "unknown";
                await _webView.CoreWebView2.ExecuteScriptAsync($"setVersion('{version}')");

                await LoadHeroDataAsync();
                await RestoreSelectionsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebView2 init failed: {ex.Message}");
                this.DialogResult = DialogResult.Abort;
                this.Close();
            }
        }

        private async Task LoadHeroDataAsync()
        {
            try
            {
                await UpdateStatusAsync("Loading heroes...");

                var heroSummaries = await _heroService.LoadHeroesAsync();
                if (heroSummaries == null || heroSummaries.Count == 0)
                {
                    await UpdateStatusAsync("No heroes found");
                    return;
                }

                _heroes = HeroModelMapper.MapFromSummaries(heroSummaries);

                var jsHeroes = _heroes.Select(h => new
                {
                    id = h.Id,
                    name = h.Name,
                    displayName = h.DisplayName,
                    attribute = h.PrimaryAttribute ?? "universal",
                    thumbnail = GetHeroThumbnail(h),
                    sets = h.Sets?.Select((kvp, idx) => new
                    {
                        name = kvp.Key,
                        index = idx,
                        isCustom = HeroModelMapper.IsCustomSet(kvp.Value),
                        category = HeroModelMapper.ClassifySet(kvp.Value).ToString().ToLowerInvariant(),
                        tag = HeroModelMapper.ExtractItemTag(kvp.Value),
                        styleGroup = h.SetStyles != null && h.SetStyles.TryGetValue(kvp.Key, out var si) ? si.Group : null,
                        styleLabel = h.SetStyles != null && h.SetStyles.TryGetValue(kvp.Key, out var sl) ? sl.Label : null,
                        styleGroupThumbnail = h.SetStyles != null && h.SetStyles.TryGetValue(kvp.Key, out var sg) ? sg.GroupThumbnail : null,
                        thumbnailUrl = kvp.Value?.FirstOrDefault(u =>
                            u.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                            u.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                            u.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                    }).ToList()
                }).ToList();

                var json = JsonSerializer.Serialize(jsHeroes, _jsonOptions);
                await ExecuteScriptAsync($"loadHeroes({json})");
                
                var favoritesJson = JsonSerializer.Serialize(_favorites.ToList(), _jsonOptions);
                await ExecuteScriptAsync($"loadFavorites({favoritesJson})");

                await LoadSetUpdatesAsync();

                await UpdateStatusAsync($"Loaded {_heroes.Count} heroes");

                await PreloadThumbnailsAsync(_heroes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadHeroDataAsync error: {ex}");
                await UpdateStatusAsync($"Error loading heroes: {ex.Message}");
            }
        }

        private async Task LoadSetUpdatesAsync()
        {
            try
            {
                var updatesData = await _heroService.LoadSetUpdatesAsync();
                
                if (updatesData.Updates.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("No set updates found");
                    return;
                }

                var recentUpdates = updatesData.GetRecentUpdates(7);
                var jsUpdates = SetUpdateResolver.Resolve(_heroes, recentUpdates);

                if (jsUpdates.Count > 0)
                {
                    var json = JsonSerializer.Serialize(jsUpdates, _jsonOptions);
                    await ExecuteScriptAsync($"loadLatestUpdates({json})");
                    var hidden = recentUpdates.Count - jsUpdates.Count;
                    System.Diagnostics.Debug.WriteLine(
                        $"[SetUpdate] Loaded {jsUpdates.Count} of {recentUpdates.Count} updates" +
                        (hidden > 0 ? $" ({hidden} hidden — set not in current heroes.json; data may be stale)" : ""));
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[SetUpdate] All {recentUpdates.Count} recent updates unresolved against heroes.json — hiding section (stale hero data).");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadSetUpdatesAsync error: {ex.Message}");
            }
        }

        private static string GetHeroThumbnail(HeroModel hero) => SetUpdateResolver.HeroPortraitUrl(hero);

        private async Task PreloadThumbnailsAsync(List<HeroModel> heroes)
        {
            var cacheService = AssetCacheService.Instance;

            var allUrls = CollectThumbnailUrls(heroes);
            if (allUrls.Count == 0)
            {
                await ExecuteScriptAsync("hideCachingOverlay()");
                return;
            }

            var cached = allUrls.Where(u => cacheService.IsCached(u)).ToList();
            var notCached = allUrls
                .Where(u => !cacheService.IsCached(u) && !cacheService.IsKnownMissing(u, MissingThumbnailTtl))
                .ToList();

            System.Diagnostics.Debug.WriteLine(
                $"[HeroGallery] Thumbnails: {cached.Count} cached, {notCached.Count} missing");

            if (notCached.Count == 0 && !cacheService.ShouldRefreshAssets(RefreshCooldown))
            {
                System.Diagnostics.Debug.WriteLine(
                    "[HeroGallery] All cached, cooldown active — skipping overlay");
                await ExecuteScriptAsync("hideCachingOverlay()");
                return;
            }

            if (notCached.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[HeroGallery] All cached, cooldown expired — silent freshness check");
                
                await ExecuteScriptAsync("hideCachingOverlay()");
                
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await RunSilentRefreshAsync(cacheService, cached);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[HeroGallery] Background refresh error: {ex.Message}");
                    }
                });
                return;
            }

            await RunDownloadWithOverlayAsync(cacheService, cached, notCached);
        }

        private static readonly TimeSpan RefreshCooldown = TimeSpan.FromMinutes(10);

        private static readonly TimeSpan MissingThumbnailTtl = TimeSpan.FromDays(7);

        private static List<string> CollectThumbnailUrls(List<HeroModel> heroes)
        {
            var urls = new List<string>();

            foreach (var hero in heroes)
            {
                if (hero.Sets == null) continue;

                foreach (var kvp in hero.Sets)
                {
                    var thumbUrl = kvp.Value?.FirstOrDefault(IsImageUrl);

                    if (!string.IsNullOrEmpty(thumbUrl))
                        urls.Add(thumbUrl);
                }
            }

            var covers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var hero in heroes)
            {
                if (hero.SetStyles == null) continue;

                foreach (var style in hero.SetStyles.Values)
                {
                    var cover = style?.GroupThumbnail;
                    if (!string.IsNullOrWhiteSpace(cover) && IsImageUrl(cover) && covers.Add(cover))
                        urls.Add(cover);
                }
            }

            return urls;
        }

        private static bool IsImageUrl(string url) =>
            url.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
            url.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            url.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);

        private async Task RunSilentRefreshAsync(AssetCacheService cacheService, List<string> cachedUrls)
        {
            try
            {
                var result = await cacheService.RefreshStaleAssetsAsync(cachedUrls);
                cacheService.MarkRefreshed();

                System.Diagnostics.Debug.WriteLine(
                    $"[HeroGallery] Silent refresh complete: " +
                    $"{result.refreshed} refreshed, {result.skipped} skipped, {result.failed} failed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[HeroGallery] Silent refresh error: {ex.Message}");
            }
        }

        private static readonly TimeSpan ThumbnailOverlayCap = TimeSpan.FromSeconds(30);

        private async Task RunDownloadWithOverlayAsync(
            AssetCacheService cacheService,
            List<string> cached,
            List<string> notCached)
        {
            await ExecuteScriptAsync("showCachingOverlay()");

            try
            {
                var work = DownloadThumbnailsCoreAsync(cacheService, cached, notCached);
                if (await Task.WhenAny(work, Task.Delay(ThumbnailOverlayCap)) != work)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[HeroGallery] Thumbnail preload exceeded overlay cap — releasing UI, downloads continue in background");
                }
            }
            finally
            {
                await Task.Delay(300);
                await ExecuteScriptAsync("hideCachingOverlay()");
            }
        }

        private async Task DownloadThumbnailsCoreAsync(
            AssetCacheService cacheService,
            List<string> cached,
            List<string> notCached)
        {
            try
            {
                int refreshed = 0;

                if (cached.Count > 0 && cacheService.ShouldRefreshAssets(RefreshCooldown))
                {
                    await ExecuteScriptAsync(
                        "document.getElementById('cachingStatus').textContent = 'Checking for updates...'");

                    var refreshProgress = new Progress<(int current, int total, string url)>(async p =>
                    {
                        try { await ExecuteScriptAsync($"updateCachingProgress({p.current}, {p.total})"); }
                        catch { }
                    });

                    var refreshResult = await cacheService.RefreshStaleAssetsAsync(cached, refreshProgress);
                    refreshed = refreshResult.refreshed;

                    System.Diagnostics.Debug.WriteLine(
                        $"[HeroGallery] Refreshed {refreshed} stale assets");
                }

                await ExecuteScriptAsync(
                    "document.getElementById('cachingStatus').textContent = 'Downloading thumbnails...'");
                await ExecuteScriptAsync($"updateCachingProgress(0, {notCached.Count})");

                var downloadProgress = new Progress<(int current, int total, string url)>(async p =>
                {
                    try { await ExecuteScriptAsync($"updateCachingProgress({p.current}, {p.total})"); }
                    catch { }
                });

                var downloadResult = await cacheService.PreloadAssetsWithProgressAsync(notCached, downloadProgress);

                cacheService.MarkRefreshed();

                System.Diagnostics.Debug.WriteLine(
                    $"[HeroGallery] Download complete: " +
                    $"{refreshed} refreshed, {downloadResult.downloaded} downloaded, " +
                    $"{downloadResult.skipped} skipped, {downloadResult.failed} failed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HeroGallery] Thumbnail preload error: {ex.Message}");
            }
        }

        private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var message = JsonSerializer.Deserialize<JsonElement>(e.WebMessageAsJson);
                var type = message.GetProperty("type").GetString();

                switch (type)
                {
                    case "selectionChanged":
                        if (message.TryGetProperty("selections", out var changedEl))
                            _selections = ParseSelections(changedEl);
                        break;

                    case "favoritesChanged":
                        HandleFavoritesChanged(message);
                        break;

                    case "generate":
                        if (message.TryGetProperty("selections", out var generateEl))
                            _selections = ParseSelections(generateEl);
                        BeginInvoke(new Action(async () => await _presenter.GenerateAsync(_heroes, _selections)));
                        break;

                    case "close":
                        BeginInvoke(new Action(() => this.Close()));
                        break;

                    case "startDrag":
                        BeginInvoke(new Action(() =>
                        {
                            ReleaseCapture();
                            SendMessage(this.Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                        }));
                        break;

                    case "savePreset":
                        await HandleSavePresetAsync(message);
                        break;

                    case "loadPreset":
                        await HandleLoadPresetAsync();
                        break;

                    case "alertDismissed":
                        _alertDismissed?.TrySetResult(true);
                        break;

                    case "generationLogOpened":
                        _generationLogViews++;
                        break;

                    case "baseNoSetConfirmed":
                        if (message.TryGetProperty("confirmed", out var confirmedEl) && confirmedEl.ValueKind == JsonValueKind.True)
                        {
                            _confirmBaseNoSet?.TrySetResult(true);
                        }
                        else
                        {
                            _confirmBaseNoSet?.TrySetResult(false);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebMessage error: {ex.Message}");
            }
        }

        private static Dictionary<string, HeroSelectionState> ParseSelections(JsonElement selectionsEl)
        {
            var result = new Dictionary<string, HeroSelectionState>();

            if (selectionsEl.ValueKind != JsonValueKind.Object)
                return result;

            foreach (var prop in selectionsEl.EnumerateObject())
            {
                HeroSelectionState? state;
                try
                {
                    state = prop.Value.Deserialize<HeroSelectionState>(_jsonOptions);
                }
                catch
                {
                    state = null;
                }

                if (state != null && state.HasAnySelection)
                    result[prop.Name] = state;
            }

            return result;
        }

        private void HandleFavoritesChanged(JsonElement message)
        {
            _favorites.Clear();

            if (message.TryGetProperty("favorites", out var favoritesEl))
            {
                foreach (var item in favoritesEl.EnumerateArray())
                {
                    var heroId = item.GetString();
                    if (!string.IsNullOrEmpty(heroId))
                    {
                        _favorites.Add(heroId);
                    }
                }
            }
        }

        private async Task HandleSavePresetAsync(JsonElement message)
        {
            try
            {
                if (_selections.Count == 0)
                {
                    await UpdateStatusAsync("No selections to save");
                    return;
                }

                using var sfd = new SaveFileDialog
                {
                    Title = "Save Skin Preset",
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    DefaultExt = "json",
                    AddExtension = true,
                    FileName = "skins_preset.json"
                };

                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    var json = JsonSerializer.Serialize(_selections, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(sfd.FileName, json);
                    await UpdateStatusAsync($"Saved {_selections.Count} selection(s)");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SavePreset error: {ex.Message}");
                await UpdateStatusAsync("Failed to save preset");
            }
        }

        private async Task HandleLoadPresetAsync()
        {
            try
            {
                using var ofd = new OpenFileDialog
                {
                    Title = "Load Skin Preset",
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    DefaultExt = "json"
                };

                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    var json = await File.ReadAllTextAsync(ofd.FileName);
                    var loadedSelections = JsonSerializer.Deserialize<Dictionary<string, HeroSelectionState>>(json);

                    if (loadedSelections != null && loadedSelections.Count > 0)
                    {
                        _selections = loadedSelections;

                        var selectionsJson = JsonSerializer.Serialize(_selections, _jsonOptions);
                        await ExecuteScriptAsync($"applyLoadedSelections({selectionsJson})");
                    }
                    else
                    {
                        await UpdateStatusAsync("No selections found in file");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadPreset error: {ex.Message}");
                await UpdateStatusAsync("Failed to load preset");
            }
        }


        public async Task<bool> ConfirmBaseNoSetAsync(string title, string htmlMessage)
        {
            if (_webView?.CoreWebView2 == null) return false;

            _confirmBaseNoSet = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            await ExecuteScriptAsync(
                $"showConfirmBaseNoSet('{JsEscape(title)}', '{JsEscape(htmlMessage)}')");

            var completed = await Task.WhenAny(_confirmBaseNoSet.Task, Task.Delay(DialogCallbackTimeout));
            if (completed != _confirmBaseNoSet.Task)
            {
                return false;
            }

            return await _confirmBaseNoSet.Task;
        }

        public async Task ShowGenerationAlertAsync(string title, string message, bool hasFailures, string? logText = null)
        {
            if (_webView?.CoreWebView2 == null) return;

            var iconType = hasFailures ? "warning" : "success";

            if (logText != null)
                await ExecuteScriptAsync($"setGenerationLog('{JsEscape(logText)}')");

            _alertDismissed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            await ExecuteScriptAsync(
                $"showAlert('{JsEscape(title)}', '{JsEscape(message)}', '{iconType}', true, {(logText != null ? "true" : "false")})");

            while (true)
            {
                var viewsBefore = _generationLogViews;
                var completed = await Task.WhenAny(_alertDismissed.Task, Task.Delay(DialogCallbackTimeout));
                if (completed == _alertDismissed.Task || _generationLogViews == viewsBefore)
                    break;
            }
        }

        public async Task ShowAlertAsync(string title, string message)
        {
            await ExecuteScriptAsync($"showAlert('{JsEscape(title)}', '{JsEscape(message)}')");
        }

        public bool ShowGenerationPreview(IReadOnlyList<(HeroModel hero, string setName, string? thumbnailUrl)> items)
        {
            using var previewForm = new GenerationPreviewForm(items.ToList());
            var result = previewForm.ShowDialog(this);
            return result == DialogResult.OK && previewForm.Confirmed;
        }

        public void ShowWarning(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public void ShowErrorDialog(string title, string subtitle, string details)
        {
            using var errorDialog = new ErrorLogDialog(title, subtitle, details);
            errorDialog.ShowDialog(this);
        }

        public Task<OperationResult> RunGenerationWithProgressAsync(
            string initialStatus,
            Func<ProgressOperationRunnerContext, Task<OperationResult>> operation)
        {
            return ProgressOperationRunner.RunAsync(this, initialStatus, operation, hideDownloadSpeed: true);
        }

        public void StoreResult(ModGenerationResult result) => GenerationResult = result;

        public void CloseWithSuccess()
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private static string JsEscape(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\n", "\\n")
                .Replace("\r", string.Empty);
        }

        public async Task SaveSelectionsAsync()
        {
            try
            {
                var path = GetSettingsPath();
                var json = JsonSerializer.Serialize(_selections, _jsonOptions);
                await File.WriteAllTextAsync(path, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveSelectionsAsync error: {ex.Message}");
            }
        }

        private async Task RestoreSelectionsAsync()
        {
            try
            {
                var path = GetSettingsPath();
                if (!File.Exists(path)) return;

                var json = await File.ReadAllTextAsync(path);
                if (string.IsNullOrWhiteSpace(json)) return;

                try
                {
                    var full = JsonSerializer.Deserialize<Dictionary<string, HeroSelectionState>>(json, _jsonOptions);
                    if (full != null && full.Count > 0)
                    {
                        _selections = full
                            .Where(kvp => kvp.Value != null && kvp.Value.HasAnySelection)
                            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

                        if (_selections.Count > 0)
                        {
                            var selectionsJson = JsonSerializer.Serialize(_selections, _jsonOptions);
                            await ExecuteScriptAsync($"applyLoadedSelections({selectionsJson})");
                            return;
                        }
                    }
                }
                catch
                {
                }

                List<string>? heroIds = null;
                try
                {
                    heroIds = JsonSerializer.Deserialize<List<string>>(json);
                }
                catch
                {
                    try
                    {
                        var oldFormat = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
                        if (oldFormat != null)
                            heroIds = oldFormat.Keys.ToList();
                    }
                    catch { }
                }

                if (heroIds != null && heroIds.Count > 0)
                {
                    var heroIdsJson = JsonSerializer.Serialize(heroIds, _jsonOptions);
                    await ExecuteScriptAsync($"loadHighlightedHeroes({heroIdsJson})");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RestoreSelectionsAsync error: {ex.Message}");
            }
        }

        private string GetSettingsPath()
        {
            var dotaPath = _configService.GetLastTargetPath();

            if (!string.IsNullOrEmpty(dotaPath))
            {
                var tempPath = Path.Combine(dotaPath, "game", "_ArdysaMods", "_temp");
                if (Directory.Exists(tempPath) || TryCreateDirectory(tempPath))
                {
                    return Path.Combine(tempPath, "hero_selections.json");
                }
            }

            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ArdysaModsTools");
            TryCreateDirectory(appDataPath);
            return Path.Combine(appDataPath, "hero_selections.json");
        }

        private bool TryCreateDirectory(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task UpdateStatusAsync(string status)
        {
            if (!_initialized || _webView?.CoreWebView2 == null) return;
            try
            {
                var escaped = status.Replace("\\", "\\\\").Replace("'", "\\'");
                await ExecuteScriptAsync($"updateStatus('{escaped}')");
            }
            catch { }
        }

        private async Task ExecuteScriptAsync(string script)
        {
            if (_webView?.CoreWebView2 == null) return;
            try
            {
                await _webView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch { }
        }

        private string GetGalleryHtml()
        {
            string appPath = AppDomain.CurrentDomain.BaseDirectory;
            string htmlPath = Path.Combine(appPath, "Assets", "Html", "hero_gallery.html");

            if (File.Exists(htmlPath))
            {
                return File.ReadAllText(htmlPath);
            }

            return @"<!DOCTYPE html>
<html><head><style>
body { background: #000; color: #fff; font-family: 'JetBrains Mono', monospace; display: flex; align-items: center; justify-content: center; min-height: 100vh; }
</style></head><body>
<div>Failed to load hero_gallery.html</div>
</body></html>";
        }
    }
}
