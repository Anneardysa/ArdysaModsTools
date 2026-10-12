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
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Helpers;
using ArdysaModsTools.Core.DependencyInjection;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Services.Localization;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Cache;
using ArdysaModsTools.Core.Services.Config;
using ArdysaModsTools.Core.Services.Hero;
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
        private bool _playRequestedFromAlert;
        private TaskCompletionSource<bool>? _confirmBaseNoSet;

        private int _generationLogViews;

        private static readonly TimeSpan DialogCallbackTimeout = TimeSpan.FromSeconds(60);

        private static readonly TimeSpan GalleryLoadTimeout = TimeSpan.FromSeconds(30);

        private List<HeroModel> _heroes = new();
        private Dictionary<string, HeroSelectionState> _selections = new();
        private HashSet<string> _favorites = new(StringComparer.OrdinalIgnoreCase);
        private readonly HeroService _heroService;
        private readonly IConfigService _configService;
        private readonly HeroGalleryPresenter _presenter;

        private SetUpdatesData? _lastSetUpdatesData;

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

        private static readonly JsonSerializerOptions _heroPayloadJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
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
            this.ClientSize = new Size(1732, 1032);
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
                await WebViewNavigation.NavigateAndWaitReadyAsync(
                    _webView.CoreWebView2, Helpers.WebViewTheming.Apply(html), GalleryLoadTimeout);

                _initialized = true;

                if (Loc.Service != null)
                    await _webView.CoreWebView2.ExecuteScriptAsync(WebViewLocalizer.BuildBootstrapScript(Loc.Service));

                var version = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion ?? "unknown";
                await _webView.CoreWebView2.ExecuteScriptAsync($"setVersion('{version}')");

                OnExtensionsReady();
#if DEBUG
                await EnableDebugCardAsync();
#endif

                await LoadHeroDataAsync();
                await _presenter.SyncCooldownStatusAsync();

                _ = _presenter.DetectInstalledAsync(_heroes);

                _ = Task.Run(async () =>
                {
                    await SkinSelectorCooldownService.CalibrateServerTimeAsync();
                    if (_initialized)
                    {
                        try { BeginInvoke(new Action(async () => await _presenter.SyncCooldownStatusAsync())); } catch { }
                    }
                });
            }
            catch (Exception ex)
            {
                StartupLog.Append($"HeroGallery init failed: {ex}");
                MessageBox.Show(this,
                    Loc.T("form.webview.initFailed.body", new { error = ex.Message }),
                    Loc.T("form.webview.initFailed.title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.DialogResult = DialogResult.Abort;
                this.Close();
            }
        }

        private async Task LoadHeroDataAsync()
        {
            try
            {
                await UpdateStatusAsync("Loading heroes...");

                var setUpdatesTask = _heroService.LoadSetUpdatesAsync();

                var heroSummaries = await _heroService.LoadHeroesAsync();
                if (heroSummaries == null || heroSummaries.Count == 0)
                {
                    await UpdateStatusAsync("No heroes found");
                    await ExecuteScriptAsync("heroesFailed()");
                    return;
                }

                var heroes = HeroModelMapper.MapFromSummaries(heroSummaries);
                var json = await Task.Run(() => BuildHeroPayloadJson(heroes));
                _heroes = heroes;
                var particlesJson = JsonSerializer.Serialize(
                    Core.Data.HeroParticleCatalog.All.Select(p => new
                    {
                        id = p.Id, name = p.Name, source = p.Source, fit = p.Fit, courier = p.Courier, pools = p.Pools,
                        thumbnail = Core.Data.HeroParticleCatalog.WithPreview.Contains(p.Id)
                            ? EnvironmentConfig.BuildRawUrl($"{Core.Constants.CdnConfig.ParticleThumbsPath}/{p.Id}.gif")
                            : null,
                    }), _jsonOptions);
                await ExecuteScriptAsync($"loadParticles({particlesJson})");
                await ExecuteScriptAsync($"loadHeroes({json})");

                var favoritesJson = JsonSerializer.Serialize(_favorites.ToList(), _jsonOptions);
                await ExecuteScriptAsync($"loadFavorites({favoritesJson})");

                await LoadSetUpdatesAsync(await setUpdatesTask);

                await UpdateStatusAsync($"Loaded {_heroes.Count} heroes");

                await PreloadThumbnailsAsync(_heroes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadHeroDataAsync error: {ex}");
                await UpdateStatusAsync($"Error loading heroes: {ex.Message}");
                if (_heroes.Count == 0) await ExecuteScriptAsync("heroesFailed()");
            }
        }

        private static string BuildHeroPayloadJson(List<HeroModel> heroes)
        {
                var jsHeroes = heroes.Select(h =>
                {
                    var orderedSets = h.Sets?.ToList() ?? new List<KeyValuePair<string, List<string>>>();
                    var conflicts = SetCompatibilityResolver.BuildConflictMap(
                        orderedSets,
                        h.Incompatible,
                        msg => Core.Services.FallbackLogger.LogFileOnly($"[skinselector] {h.DisplayName}: {msg}"));
                    var carriers = SetCompatibilityResolver.BuildEffectCarriers(orderedSets, h.SetNames);
                    var notices = SetNoticeResolver.Resolve(
                        orderedSets,
                        h.Notices,
                        msg => Core.Services.FallbackLogger.LogFileOnly($"[skinselector] {h.DisplayName}: {msg}"));

                    return new
                    {
                        id = h.Id,
                        name = h.Name,
                        displayName = h.DisplayName,
                        attribute = h.PrimaryAttribute ?? "universal",
                        thumbnail = GetHeroThumbnail(h),
                        abilityNames = h.AbilityNames is { Count: > 0 } names ? names : null,
                        abilityIcons = h.AbilityIcons is { Count: > 0 } icons ? icons : null,
                        particleHosts = Core.Data.HeroParticleCatalog.HostsFor(h) is { Count: > 0 } hosts
                            ? hosts.Select(x => new { id = x.Id, label = x.Label }).ToList()
                            : null,
                        sets = orderedSets.Select((kvp, idx) => new
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
                                u.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)),
                            displayName = ResolveSetDisplayName(h, kvp.Value),
                            abilityIconUrl = ResolveEffectIconUrl(h, kvp.Value),
                            conflicts = conflicts[idx].Count > 0 ? conflicts[idx] : null,
                            carriedBy = carriers[idx].Count > 0 ? carriers[idx] : null,
                            notices = notices[idx],
                            locks = BaseSlotLock.SlotsFor(h, kvp.Key) is { Count: > 0 } locked ? locked : null
                        }).ToList()
                    };
                }).ToList();

                return JsonSerializer.Serialize(jsHeroes, _heroPayloadJsonOptions);
        }

        private async Task LoadSetUpdatesAsync(SetUpdatesData updatesData)
        {
            try
            {
                _lastSetUpdatesData = updatesData;
                
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

        private static string? ResolveEffectIconUrl(HeroModel hero, List<string>? assetUrls)
        {
            if (hero.EffectIcons is not { Count: > 0 }) return null;
            var stem = HeroModelMapper.ExtractArchiveStem(assetUrls);
            return stem != null && hero.EffectIcons.TryGetValue(stem, out var url) ? url : null;
        }

        private static string? ResolveSetDisplayName(HeroModel hero, List<string>? assetUrls)
        {
            if (hero.SetNames == null || hero.SetNames.Count == 0)
                return null;

            var stem = HeroModelMapper.ExtractArchiveStem(assetUrls);
            return !string.IsNullOrEmpty(stem) && hero.SetNames.TryGetValue(stem!, out var display)
                ? display
                : null;
        }

        private async Task PreloadThumbnailsAsync(List<HeroModel> heroes)
        {
            var cacheService = AssetCacheService.Instance;

            var allUrls = CollectThumbnailUrls(heroes);
            if (allUrls.Count == 0)
            {
                await ExecuteScriptAsync("hideCachingOverlay()");
                return;
            }

            var notCached = await Task.Run(() =>
            {
                InvalidateChangedSetThumbnails(cacheService, heroes);

                return allUrls
                    .Where(u => !cacheService.IsCached(u) && !cacheService.IsKnownMissing(u, MissingThumbnailTtl))
                    .ToList();
            });

            Core.Services.FallbackLogger.LogFileOnly(
                $"[HeroGallery] Thumbnails: {allUrls.Count - notCached.Count} cached, {notCached.Count} missing");

            if (notCached.Count == 0)
            {
                await ExecuteScriptAsync("hideCachingOverlay()");
                return;
            }

            await RunDownloadWithOverlayAsync(cacheService, notCached);
        }

        private void InvalidateChangedSetThumbnails(AssetCacheService cacheService, List<HeroModel> heroes)
        {
            var updatesData = _lastSetUpdatesData;
            if (updatesData == null || string.IsNullOrEmpty(updatesData.Version))
                return;

            cacheService.InvalidateOnEpochChange("hero-set-update", updatesData.ThumbnailEpoch,
                lastSeen => SetUpdateResolver.Resolve(heroes, updatesData.GetUpdatesSinceEpoch(lastSeen))
                    .Select(c => c.SetThumbnail)
                    .Where(u => !string.IsNullOrEmpty(u)));
        }

        private static readonly TimeSpan MissingThumbnailTtl = TimeSpan.FromDays(7);

        internal static List<string> CollectThumbnailUrls(List<HeroModel> heroes)
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

            foreach (var hero in heroes)
                foreach (var icon in hero.AbilityIcons.Values.Concat(hero.EffectIcons.Values))
                    if (covers.Add(icon))
                        urls.Add(icon);

            return urls;
        }

        private static bool IsImageUrl(string url) =>
            url.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
            url.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            url.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);

        private static readonly TimeSpan ThumbnailOverlayCap = TimeSpan.FromSeconds(30);

        private async Task RunDownloadWithOverlayAsync(AssetCacheService cacheService, List<string> notCached)
        {
            await ExecuteScriptAsync("showCachingOverlay()");

            try
            {
                var work = DownloadThumbnailsCoreAsync(cacheService, notCached);
                if (await Task.WhenAny(work, Task.Delay(ThumbnailOverlayCap)) != work)
                {
                    Core.Services.FallbackLogger.LogFileOnly(
                        "[HeroGallery] Thumbnail preload exceeded overlay cap — releasing UI, downloads continue in background");
                }
            }
            finally
            {
                await Task.Delay(300);
                await ExecuteScriptAsync("hideCachingOverlay()");
            }
        }

        private async Task DownloadThumbnailsCoreAsync(AssetCacheService cacheService, List<string> notCached)
        {
            try
            {
                var status = JsonSerializer.Serialize(Loc.T("common.updatingAssets"));
                await ExecuteScriptAsync(
                    $"(function(){{var e=document.getElementById('cachingStatus');if(e)e.textContent={status};}})()");
                await ExecuteScriptAsync($"updateCachingProgress(0, {notCached.Count})");

                int lastPct = -1;
                var downloadProgress = new Progress<(int current, int total, string url)>(async p =>
                {
                    var pct = p.current * 100 / Math.Max(1, p.total);
                    if (pct == lastPct && p.current != p.total) return;
                    lastPct = pct;
                    try { await ExecuteScriptAsync($"updateCachingProgress({p.current}, {p.total})"); }
                    catch { }
                });

                var downloadResult = await cacheService.PreloadAssetsWithProgressAsync(notCached, downloadProgress);

                Core.Services.FallbackLogger.LogFileOnly(
                    $"[HeroGallery] Thumbnail download complete: " +
                    $"{downloadResult.downloaded} downloaded, " +
                    $"{downloadResult.skipped} skipped, {downloadResult.failed} failed");
            }
            catch (Exception ex)
            {
                Core.Services.FallbackLogger.LogFileOnly($"[HeroGallery] Thumbnail preload error: {ex.Message}");
            }
        }

        partial void OnExtensionsReady();

        partial void OnExtensionMessage(string? type, JsonElement message);

        partial void OnExtensionsShutdown();

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            OnExtensionsShutdown();
            base.OnFormClosed(e);
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

                    case "resetCooldown":
                        if (EnvironmentConfig.IsDevMode)
                        {
                            BeginInvoke(new Action(async () => await _presenter.ResetCooldownAsync()));
                        }
                        break;

                    case "generate":
                        if (message.TryGetProperty("selections", out var generateEl))
                            _selections = ParseSelections(generateEl);
                        BeginInvoke(new Action(async () => await _presenter.GenerateAsync(_heroes, _selections)));
                        break;

                    case "close":
                        BeginInvoke(new Action(() => this.Close()));
                        break;

                    case "openUrl":
                        if (message.TryGetProperty("url", out var linkUrl))
                        {
                            var target = linkUrl.GetString();
                            BeginInvoke(new Action(() => UIHelpers.OpenUrlWithErrorDialog(target, "GitHub")));
                        }
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

                    case "alertPlay":
                        _playRequestedFromAlert = true;
                        _alertDismissed?.TrySetResult(true);
                        break;

                    case "generationLogOpened":
                        _generationLogViews++;
                        break;

                    case "copyConsole":
                        if (message.TryGetProperty("text", out var copyEl) &&
                            copyEl.GetString() is { Length: > 0 } copyText)
                        {
                            try { Clipboard.SetText(copyText); } catch { }
                        }
                        break;

                    case "particleSlots":
                        if (message.TryGetProperty("heroId", out var psHero) && psHero.ValueKind == JsonValueKind.String
                            && message.TryGetProperty("setIndex", out var psIdx) && psIdx.ValueKind == JsonValueKind.Number && psIdx.TryGetInt32(out var setIdx))
                            await _presenter.LoadParticleSlotsAsync(_heroes, psHero.GetString()!, setIdx);
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

                    default:
#if DEBUG
                        if (HandleDebugMessage(type, message))
                            break;
#endif
                        OnExtensionMessage(type, message);
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
                    HeroGalleryPresenter.AttachStems(_heroes, _selections);
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
                        var missing = HeroGalleryPresenter.ResolveStems(_heroes, loadedSelections);
                        if (missing.Count > 0)
                            Core.Services.FallbackLogger.LogFileOnly(
                                $"[skinselector] preset picks no longer in heroes.json: {string.Join(", ", missing)}");
                        _selections = loadedSelections;

                        var selectionsJson = JsonSerializer.Serialize(_selections, _jsonOptions);
                        await ExecuteScriptAsync($"applyLoadedSelections({selectionsJson})");
                        if (missing.Count > 0)
                            await UpdateStatusAsync($"{missing.Count} preset pick(s) are no longer available, skipped");
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
            if (!_initialized) return false;

            _confirmBaseNoSet = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            await ExecuteScriptAsync(
                $"showConfirmBaseNoSet('{JsEscape(title)}', '{JsEscape(htmlMessage)}')");

            var completed = await Task.WhenAny(_confirmBaseNoSet.Task, Task.Delay(DialogCallbackTimeout));
            if (completed != _confirmBaseNoSet.Task)
            {
                await ExecuteScriptAsync("hideConfirmBaseNoSet()");
                return false;
            }

            return await _confirmBaseNoSet.Task;
        }

        public async Task<bool> ShowGenerationAlertAsync(string title, string message, bool hasFailures, string? logText = null)
        {
            if (!_initialized) return false;

            _playRequestedFromAlert = false;
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

            return _playRequestedFromAlert;
        }

        public async Task ShowAlertAsync(string title, string message)
        {
            await ExecuteScriptAsync($"showAlert('{JsEscape(title)}', '{JsEscape(message)}')");
        }

        public async Task UpdateCooldownAsync(bool active, int remainingSeconds, int totalSeconds)
        {
            var payload = JsonSerializer.Serialize(new
            {
                active,
                remainingSeconds,
                totalSeconds
            }, _jsonOptions);

            await ExecuteScriptAsync($"updateCooldown({payload})");
        }

        public bool ShowGenerationPreview(IReadOnlyList<(HeroModel hero, string setName, string? thumbnailUrl)> items)
        {
            return UiThread.Run(this, () =>
            {
                using var previewForm = new GenerationPreviewForm(items.ToList());
                var result = previewForm.ShowDialog(this);
                return result == DialogResult.OK && previewForm.Confirmed;
            });
        }

        public void ShowWarning(string message, string title) =>
            UiThread.Post(this, () => MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning));

        public void ShowErrorDialog(string title, string subtitle, string details) =>
            UiThread.Post(this, () =>
            {
                using var errorDialog = new ErrorLogDialog(title, subtitle, details);
                errorDialog.ShowDialog(this);
            });

        public Task<OperationResult> RunGenerationWithProgressAsync(
            string initialStatus,
            Func<ProgressOperationRunnerContext, Task<OperationResult>> operation)
        {
            return UiThread.RunAsync(this,
                () => ProgressOperationRunner.RunAsync(this, initialStatus, operation, hideDownloadSpeed: true),
                fallback: OperationResult.Fail("UI unavailable"));
        }

        public async Task ApplyDetectedSelectionsAsync(IReadOnlyDictionary<string, HeroSelectionState> selections)
        {
            _selections = selections.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

            var json = JsonSerializer.Serialize(_selections, _jsonOptions);
            await ExecuteScriptAsync($"applyDetectedSelections({json})");
        }

        public Task ShowDetectionAsync(bool found, int count) =>
            ExecuteScriptAsync($"showDetection({(found ? "true" : "false")}, {count})");

        public Task ShowDetectionFailedAsync() => ExecuteScriptAsync("showDetectionFailed()");

        public Task ShowParticleSlotsAsync(string heroId, int setIndex, Core.Data.ParticleSlots? slots) =>
            ExecuteScriptAsync($"particleSlotsLoaded({JsonSerializer.Serialize(new { heroId, setIndex, slots }, _jsonOptions)})");

        public void StoreResult(ModGenerationResult result) => GenerationResult = result;

        public void CloseWithSuccess() => UiThread.Post(this, () =>
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        });

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
            if (!_initialized) return;
            var escaped = status.Replace("\\", "\\\\").Replace("'", "\\'");
            await ExecuteScriptAsync($"updateStatus('{escaped}')");
        }

        public async Task UpdateCooldownAsync(bool active, int remainingSeconds, int totalSeconds, int dailyUsed, int dailyMax, bool isDailyLimit)
        {
            if (!_initialized) return;
            var payload = JsonSerializer.Serialize(new
            {
                active,
                remainingSeconds,
                totalSeconds,
                dailyUsed,
                dailyMax,
                isDailyLimit
            }, _jsonOptions);

            await ExecuteScriptAsync($"updateCooldown({payload})");
        }

        private readonly HashSet<string> _loggedScriptErrorTypes = new();

        private Task ExecuteScriptAsync(string script) => UiThread.RunAsync(this, async () =>
        {
            if (_webView?.CoreWebView2 == null) return;
            try
            {
                await _webView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                if (_loggedScriptErrorTypes.Add(ex.GetType().Name))
                    StartupLog.Append($"HeroGallery ExecuteScriptAsync failed ({ex.GetType().Name}): {ex.Message}");
            }
        });

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
