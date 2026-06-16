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
    /// <summary>
    /// Modern WebView2-based hero selection gallery with Tailwind CSS styling.
    /// Replaces the traditional WinForms SelectHero form with a beautiful web UI.
    /// This form is a thin WebView2 host; all generation logic lives in
    /// <see cref="HeroGalleryPresenter"/>.
    /// </summary>
    public partial class HeroGalleryForm : Form, IHeroGalleryView
    {
        private WebView2? _webView;
        private bool _initialized;
        private TaskCompletionSource<bool>? _alertDismissed;
        private TaskCompletionSource<bool>? _confirmBaseNoSet;

        // Timeout guard so a missing JS callback can never hang an awaiting bridge call.
        private static readonly TimeSpan DialogCallbackTimeout = TimeSpan.FromSeconds(60);

        // Data
        private List<HeroModel> _heroes = new();
        private Dictionary<string, HeroSelectionState> _selections = new(); // heroId -> structured selection
        private HashSet<string> _favorites = new(StringComparer.OrdinalIgnoreCase);
        private readonly HeroService _heroService;
        private readonly IConfigService _configService;
        private readonly HeroGalleryPresenter _presenter;

        /// <summary>
        /// Result of the generation operation. Check after form closes.
        /// </summary>
        public ModGenerationResult? GenerationResult { get; private set; }

        // P/Invoke for window dragging
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        // JSON serialization options
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

            // Presenter owns the generation flow; this form only reflects state via IHeroGalleryView.
            _presenter = new HeroGalleryPresenter(new HeroGenerationService(), _configService);
            _presenter.SetView(this);

            // Load favorites
            var loadedFav = FavoritesStore.Load();
            _favorites = loadedFav ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            this.Shown += async (s, e) => await InitializeAsync();
            this.FormClosing += (s, e) => FavoritesStore.Save(_favorites);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(1200, 800);
            this.Name = "HeroGalleryForm";
            this.Text = "Select Hero Sets - AMT 2.0";
            this.ResumeLayout(false);
        }

        private void SetupForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.MinimumSize = new Size(800, 600);
            this.BackColor = Color.Black;
            this.StartPosition = FormStartPosition.Manual;

            // Center relative to parent on load
            this.Load += (s, e) =>
            {
                if (this.Owner != null)
                {
                    this.Left = this.Owner.Left + (this.Owner.Width - this.Width) / 2;
                    this.Top = this.Owner.Top + (this.Owner.Height - this.Height) / 2;
                }
                else
                {
                    // Fallback to screen center
                    var screen = Screen.FromControl(this);
                    this.Left = screen.WorkingArea.Left + (screen.WorkingArea.Width - this.Width) / 2;
                    this.Top = screen.WorkingArea.Top + (screen.WorkingArea.Height - this.Height) / 2;
                }
            };

            // WebView2 control - fill entire form
            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = Color.Black
            };
            this.Controls.Add(_webView);

            // ESC to close
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

        /// <summary>
        /// Initialize WebView2 and load the hero gallery.
        /// </summary>
        private async Task InitializeAsync()
        {
            if (_initialized) return;

            try
            {
                // Persistent WebView2 user data (%LocalAppData%) so the browser cache survives
                // temp cleanup and updates.
                var env = await WebView2EnvironmentHelper.CreateEnvironmentAsync();

                await _webView!.EnsureCoreWebView2Async(env);
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled =
                    System.Diagnostics.Debugger.IsAttached; // Enable DevTools in debug mode

                // Handle messages from JavaScript
                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                // Serve CDN set thumbnails from the persistent asset cache instead of re-downloading.
                // Hero default portraits live on a different host (steamstatic) and are left untouched.
                WebViewAssetInterceptor.Attach(_webView.CoreWebView2, env, EnvironmentConfig.ContentBase);

                // Load HTML
                string html = GetGalleryHtml();
                _webView.CoreWebView2.NavigateToString(html);

                // Wait for navigation to complete
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

                await Task.Delay(200); // Let DOM settle
                _initialized = true;

                // Set app version in footer
                var version = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion ?? "unknown";
                await _webView.CoreWebView2.ExecuteScriptAsync($"setVersion('{version}')");

                // Load hero data
                await LoadHeroDataAsync();
                await RestoreSelectionsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebView2 init failed: {ex.Message}");
                // Signal caller to use classic fallback - caller handles the transition
                this.DialogResult = DialogResult.Abort;
                this.Close();
            }
        }

        /// <summary>
        /// Load heroes from HeroService and send to JavaScript.
        /// </summary>
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

                // Convert to JavaScript-friendly format
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
                        // Style grouping metadata (null when the set is not a styled variant).
                        // Lets the Skin Selector collapse flattened style entries into one Style Card.
                        styleGroup = h.SetStyles != null && h.SetStyles.TryGetValue(kvp.Key, out var si) ? si.Group : null,
                        styleLabel = h.SetStyles != null && h.SetStyles.TryGetValue(kvp.Key, out var sl) ? sl.Label : null,
                        thumbnailUrl = kvp.Value?.FirstOrDefault(u =>
                            u.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                            u.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                            u.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                    }).ToList()
                }).ToList();

                var json = JsonSerializer.Serialize(jsHeroes, _jsonOptions);
                await ExecuteScriptAsync($"loadHeroes({json})");
                
                // Load favorites
                var favoritesJson = JsonSerializer.Serialize(_favorites.ToList(), _jsonOptions);
                await ExecuteScriptAsync($"loadFavorites({favoritesJson})");

                // Load set updates for "Latest Updates" section
                await LoadSetUpdatesAsync();

                await UpdateStatusAsync($"Loaded {_heroes.Count} heroes");

                // Preload thumbnails with HTML overlay (shows progress before content is visible)
                await PreloadThumbnailsAsync(_heroes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadHeroDataAsync error: {ex}");
                await UpdateStatusAsync($"Error loading heroes: {ex.Message}");
            }
        }

        /// <summary>
        /// Load set updates and send to JavaScript for the Latest Updates section.
        /// </summary>
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

                // Get recent updates (last 7 days) and convert to JS format
                var recentUpdates = updatesData.GetRecentUpdates(7);
                
                // Build update entries with hero info from our loaded heroes
                var jsUpdates = recentUpdates.Select(u => {
                    // Try to find hero - handle both "npc_dota_hero_xxx" and just "xxx" formats
                    var hero = _heroes.FirstOrDefault(h => 
                        string.Equals(h.Id, u.HeroId, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(h.Id, $"npc_dota_hero_{u.HeroId.Replace("npc_dota_hero_", "")}", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(h.Id?.Replace("npc_dota_hero_", ""), u.HeroId.Replace("npc_dota_hero_", ""), StringComparison.OrdinalIgnoreCase));
                    
                    // Find set thumbnail
                    string? setThumbnail = null;
                    int setIndex = -1;
                    if (hero?.Sets != null)
                    {
                        var setEntry = hero.Sets
                            .Select((kvp, idx) => new { Name = kvp.Key, Files = kvp.Value, Index = idx })
                            .FirstOrDefault(s => string.Equals(s.Name, u.SetName, StringComparison.OrdinalIgnoreCase));
                        
                        if (setEntry != null)
                        {
                            setIndex = setEntry.Index;
                            setThumbnail = setEntry.Files?.FirstOrDefault(f =>
                                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase));
                            
                            // Debug: log if thumbnail wasn't found
                            if (setThumbnail == null)
                            {
                                System.Diagnostics.Debug.WriteLine($"[SetUpdate] {u.HeroId}/{u.SetName}: No image in {setEntry.Files?.Count ?? 0} files");
                            }
                        }
                        else
                        {
                            // Debug: log if set wasn't found
                            System.Diagnostics.Debug.WriteLine($"[SetUpdate] {u.HeroId}/{u.SetName}: Set not found in hero.Sets (has {hero.Sets.Count} sets: {string.Join(", ", hero.Sets.Keys)})");
                        }
                    }
                    else
                    {
                        // Debug: log if hero wasn't found
                        System.Diagnostics.Debug.WriteLine($"[SetUpdate] {u.HeroId}/{u.SetName}: Hero not found or has no sets");
                    }
                    
                    var heroThumbnail = hero != null ? GetHeroThumbnail(hero) : null;

                    return new
                    {
                        heroId = u.HeroId,
                        heroName = hero?.DisplayName ?? hero?.Name ?? HeroModelMapper.FormatHeroIdAsName(u.HeroId),
                        heroThumbnail = heroThumbnail,
                        setName = u.SetName,
                        setIndex = setIndex,
                        // Real resolved set thumbnail only — NOT coalesced to the hero portrait. A null
                        // here means the update references a set absent from the loaded heroes.json (stale
                        // hero data vs a fresher set_update.json). Such entries are filtered out below so
                        // the carousel never collapses every card onto one shared portrait. See plan #1.
                        setThumbnail = setThumbnail,
                        addedDate = u.AddedDate.ToString("yyyy-MM-dd"),
                        daysAgo = (int)(DateTime.Now - u.AddedDate).TotalDays,
                        isValid = hero != null && setIndex >= 0
                    };
                })
                // Keep original order from JSON (newest sets first). Only surface updates whose set
                // actually resolves to a real, distinct thumbnail in the current heroes.json. Unresolved
                // entries (stale/desynced hero data) are hidden rather than rendered as duplicates.
                .Where(u => u.isValid && u.setThumbnail != null)
                .ToList();

                if (jsUpdates.Count > 0)
                {
                    var json = JsonSerializer.Serialize(jsUpdates, _jsonOptions);
                    // [AMT:PRO] Paired with loadLatestUpdates() in Assets/Html/hero_gallery.html — the
                    // payload shape (heroId/setIndex/setThumbnail/...) must stay in sync with that JS.
                    await ExecuteScriptAsync($"loadLatestUpdates({json})");
                    var hidden = recentUpdates.Count - jsUpdates.Count;
                    System.Diagnostics.Debug.WriteLine(
                        $"[SetUpdate] Loaded {jsUpdates.Count} of {recentUpdates.Count} updates" +
                        (hidden > 0 ? $" ({hidden} hidden — set not in current heroes.json; data may be stale)" : ""));
                }
                else
                {
                    // Every recent update was unresolved → heroes.json is out of sync with set_update.json.
                    System.Diagnostics.Debug.WriteLine(
                        $"[SetUpdate] All {recentUpdates.Count} recent updates unresolved against heroes.json — hiding section (stale hero data).");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadSetUpdatesAsync error: {ex.Message}");
                // Don't fail silently - just skip updates section
            }
        }

        /// <summary>
        /// Get thumbnail URL for a hero - always use Dota 2 CDN for default portrait.
        /// </summary>
        private string GetHeroThumbnail(HeroModel hero)
        {
            // Always use Dota 2 CDN for default hero portrait (not set thumbnails)
            var heroName = hero.Name?.Replace("npc_dota_hero_", "") ?? "";
            return $"https://cdn.cloudflare.steamstatic.com/apps/dota2/images/dota_react/heroes/{heroName}.png";
        }

        /// <summary>
        /// Preload set thumbnails into cache with smart overlay logic.
        /// 
        /// Three execution paths to avoid showing the overlay on every open:
        /// 
        /// 1. ALL CACHED + RECENTLY REFRESHED (within cooldown):
        ///    Skip entirely — no overlay, no network requests. Instant open.
        ///    
        /// 2. ALL CACHED + COOLDOWN EXPIRED:
        ///    Run freshness check silently in background (no overlay).
        ///    Only re-downloads if the server has newer versions.
        ///    
        /// 3. MISSING THUMBNAILS (not cached):
        ///    Show overlay with download progress for new items only.
        /// </summary>
        private async Task PreloadThumbnailsAsync(List<HeroModel> heroes)
        {
            var cacheService = AssetCacheService.Instance;

            // ── Step 1: Collect all set thumbnail URLs ──────────────────────
            var allUrls = CollectThumbnailUrls(heroes);
            if (allUrls.Count == 0)
            {
                await ExecuteScriptAsync("hideCachingOverlay()");
                return;
            }

            // ── Step 2: Partition into cached vs missing ────────────────────
            // Sets the CDN has reported as not-found (within the TTL) are excluded so the
            // overlay never re-downloads thumbnails that don't exist on the CDN.
            var cached = allUrls.Where(u => cacheService.IsCached(u)).ToList();
            var notCached = allUrls
                .Where(u => !cacheService.IsCached(u) && !cacheService.IsKnownMissing(u, MissingThumbnailTtl))
                .ToList();

            System.Diagnostics.Debug.WriteLine(
                $"[HeroGallery] Thumbnails: {cached.Count} cached, {notCached.Count} missing");

            // ── Path 1: All cached + recently refreshed → skip entirely ─────
            if (notCached.Count == 0 && !cacheService.ShouldRefreshAssets(RefreshCooldown))
            {
                System.Diagnostics.Debug.WriteLine(
                    "[HeroGallery] All cached, cooldown active — skipping overlay");
                await ExecuteScriptAsync("hideCachingOverlay()");
                return;
            }

            // ── Path 2: All cached + cooldown expired → silent refresh ──────
            if (notCached.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[HeroGallery] All cached, cooldown expired — silent freshness check");
                
                // Hide overlay FIRST so user can interact immediately
                await ExecuteScriptAsync("hideCachingOverlay()");
                
                // Run freshness check in background (fire-and-forget)
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

            // ── Path 3: Missing thumbnails → show overlay for downloads ─────
            await RunDownloadWithOverlayAsync(cacheService, cached, notCached);
        }

        /// <summary>
        /// Cooldown period between batch freshness checks.
        /// Within this window, subsequent skin selector opens skip the overlay entirely.
        /// </summary>
        private static readonly TimeSpan RefreshCooldown = TimeSpan.FromMinutes(10);

        /// <summary>
        /// How long a set thumbnail the CDN reported as not-found (404/403) is skipped before it
        /// is re-checked, so the overlay doesn't re-attempt sets with no thumbnail on the CDN.
        /// </summary>
        private static readonly TimeSpan MissingThumbnailTtl = TimeSpan.FromDays(7);

        /// <summary>
        /// Collect all set thumbnail URLs from hero models.
        /// </summary>
        private static List<string> CollectThumbnailUrls(List<HeroModel> heroes)
        {
            var urls = new List<string>();

            foreach (var hero in heroes)
            {
                if (hero.Sets == null) continue;

                foreach (var kvp in hero.Sets)
                {
                    var thumbUrl = kvp.Value?.FirstOrDefault(u =>
                        u.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                        u.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                        u.EndsWith(".webp", StringComparison.OrdinalIgnoreCase));

                    if (!string.IsNullOrEmpty(thumbUrl))
                        urls.Add(thumbUrl);
                }
            }

            return urls;
        }

        /// <summary>
        /// Run freshness check silently in the background (no overlay).
        /// Called when all assets are cached but the cooldown has expired.
        /// </summary>
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

        /// <summary>
        /// Download missing thumbnails with visible overlay progress.
        /// Also runs a freshness check on cached items if the cooldown has expired.
        /// </summary>
        private async Task RunDownloadWithOverlayAsync(
            AssetCacheService cacheService,
            List<string> cached,
            List<string> notCached)
        {
            await ExecuteScriptAsync("showCachingOverlay()");

            try
            {
                int refreshed = 0;

                // Phase 1: Check freshness of cached items (only if cooldown expired)
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

                // Phase 2: Download missing items
                await ExecuteScriptAsync(
                    "document.getElementById('cachingStatus').textContent = 'Downloading thumbnails...'");
                await ExecuteScriptAsync($"updateCachingProgress(0, {notCached.Count})");

                var downloadProgress = new Progress<(int current, int total, string url)>(async p =>
                {
                    try { await ExecuteScriptAsync($"updateCachingProgress({p.current}, {p.total})"); }
                    catch { }
                });

                var downloadResult = await cacheService.PreloadAssetsWithProgressAsync(notCached, downloadProgress);

                // Mark refresh complete after both phases
                cacheService.MarkRefreshed();

                System.Diagnostics.Debug.WriteLine(
                    $"[HeroGallery] Download complete: " +
                    $"{refreshed} refreshed, {downloadResult.downloaded} downloaded, " +
                    $"{downloadResult.skipped} skipped, {downloadResult.failed} failed");
            }
            finally
            {
                // Hide overlay with small delay for smooth transition
                await Task.Delay(300);
                await ExecuteScriptAsync("hideCachingOverlay()");
            }
        }

        /// <summary>
        /// Handle messages from JavaScript.
        /// </summary>
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
                        // Use the payload's selections as the authoritative snapshot rather than
                        // trusting the cached field — this keeps generation and the saved preset
                        // consistent even if a selectionChanged message was ever missed.
                        if (message.TryGetProperty("selections", out var generateEl))
                            _selections = ParseSelections(generateEl);
                        await _presenter.GenerateAsync(_heroes, _selections);
                        break;

                    case "close":
                        this.Close();
                        break;

                    case "startDrag":
                        // Allow dragging the borderless window via Windows API
                        ReleaseCapture();
                        SendMessage(this.Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                        break;

                    case "savePreset":
                        await HandleSavePresetAsync(message);
                        break;

                    case "loadPreset":
                        await HandleLoadPresetAsync();
                        break;

                    case "alertDismissed":
                        // Signal that the alert was dismissed
                        _alertDismissed?.TrySetResult(true);
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

        /// <summary>
        /// Parse the structured per-hero selections object sent from JavaScript
        /// ({ heroId: { set, items, base } }) into the typed model, keeping only heroes
        /// that have an active selection.
        /// </summary>
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
                    // HeroSelectionState carries [JsonPropertyName] for set/items/base.
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

        /// <summary>
        /// Handle favorites changes from JavaScript.
        /// </summary>
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

        /// <summary>
        /// Handle save preset request from JavaScript.
        /// </summary>
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

        /// <summary>
        /// Handle load preset request from JavaScript.
        /// </summary>
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

                        // Send to JavaScript to update UI
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

        // ── IHeroGalleryView implementation ─────────────────────────────────────────
        // The presenter drives generation; these methods are the WebView2/WinForms bridge.

        /// <summary>
        /// [AMT:PRO] Bridge handler — paired with hero_gallery.html showConfirmBaseNoSet()/closeConfirm().
        /// The timeout guard is required: without it a missing JS callback would hang generation
        /// (and leave the Generate button dead) indefinitely.
        /// </summary>
        public async Task<bool> ConfirmBaseNoSetAsync(string title, string htmlMessage)
        {
            if (_webView?.CoreWebView2 == null) return false;

            _confirmBaseNoSet = new TaskCompletionSource<bool>();
            await ExecuteScriptAsync(
                $"showConfirmBaseNoSet('{JsEscape(title)}', '{JsEscape(htmlMessage)}')");

            var completed = await Task.WhenAny(_confirmBaseNoSet.Task, Task.Delay(DialogCallbackTimeout));
            if (completed != _confirmBaseNoSet.Task)
            {
                // Timed out waiting for the JS callback — do not proceed.
                return false;
            }

            return await _confirmBaseNoSet.Task;
        }

        /// <summary>
        /// Shows the terminal generation alert and waits (bounded) for the user to dismiss it.
        /// </summary>
        public async Task ShowGenerationAlertAsync(string title, string message, bool hasFailures)
        {
            if (_webView?.CoreWebView2 == null) return;

            var iconType = hasFailures ? "warning" : "success";
            _alertDismissed = new TaskCompletionSource<bool>();

            await ExecuteScriptAsync(
                $"showAlert('{JsEscape(title)}', '{JsEscape(message)}', '{iconType}', true)");

            await Task.WhenAny(_alertDismissed.Task, Task.Delay(DialogCallbackTimeout));
        }

        /// <summary>
        /// Shows a simple informational alert (no dismissal wait).
        /// </summary>
        public async Task ShowAlertAsync(string title, string message)
        {
            await ExecuteScriptAsync($"showAlert('{JsEscape(title)}', '{JsEscape(message)}')");
        }

        /// <summary>
        /// Shows the generation preview dialog and returns whether the user confirmed.
        /// </summary>
        public bool ShowGenerationPreview(IReadOnlyList<(HeroModel hero, string setName, string? thumbnailUrl)> items)
        {
            using var previewForm = new GenerationPreviewForm(items.ToList());
            var result = previewForm.ShowDialog(this);
            return result == DialogResult.OK && previewForm.Confirmed;
        }

        /// <summary>
        /// Shows a blocking warning message box.
        /// </summary>
        public void ShowWarning(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        /// <summary>
        /// Shows the copyable error log dialog.
        /// </summary>
        public void ShowErrorDialog(string title, string subtitle, string details)
        {
            using var errorDialog = new ErrorLogDialog(title, subtitle, details);
            errorDialog.ShowDialog(this);
        }

        /// <summary>
        /// Runs the generation operation behind the shared progress overlay.
        /// </summary>
        public Task<OperationResult> RunGenerationWithProgressAsync(
            string initialStatus,
            Func<ProgressOperationRunnerContext, Task<OperationResult>> operation)
        {
            return ProgressOperationRunner.RunAsync(this, initialStatus, operation, hideDownloadSpeed: true);
        }

        /// <summary>
        /// Stores the generation result for the parent form to read after close.
        /// </summary>
        public void StoreResult(ModGenerationResult result) => GenerationResult = result;

        /// <summary>
        /// Closes the gallery with a successful dialog result.
        /// </summary>
        public void CloseWithSuccess()
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        /// <summary>
        /// Escapes a string for safe embedding inside a single-quoted JS string literal.
        /// </summary>
        private static string JsEscape(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\n", "\\n")
                .Replace("\r", string.Empty);
        }

        /// <summary>
        /// Save selections to settings file.
        /// </summary>
        public async Task SaveSelectionsAsync()
        {
            try
            {
                var path = GetSettingsPath();
                // Save only highlighted hero IDs (not specific selections)
                var highlightedHeroes = _selections.Keys.ToList();
                var json = JsonSerializer.Serialize(highlightedHeroes, _jsonOptions);
                await File.WriteAllTextAsync(path, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveSelectionsAsync error: {ex.Message}");
            }
        }

        /// <summary>
        /// Restore highlighted heroes from settings file.
        /// Only restores which heroes are highlighted, not specific selections.
        /// </summary>
        private async Task RestoreSelectionsAsync()
        {
            try
            {
                var path = GetSettingsPath();
                if (!File.Exists(path)) return;

                var json = await File.ReadAllTextAsync(path);

                // Try new format (list of hero IDs)
                List<string>? heroIds = null;
                try
                {
                    heroIds = JsonSerializer.Deserialize<List<string>>(json);
                }
                catch
                {
                    // Backward compat: try old format { heroId: setIndex }
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
                    // Send highlighted heroes to JavaScript
                    var heroIdsJson = JsonSerializer.Serialize(heroIds, _jsonOptions);
                    await ExecuteScriptAsync($"loadHighlightedHeroes({heroIdsJson})");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RestoreSelectionsAsync error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get path to settings file.
        /// </summary>
        private string GetSettingsPath()
        {
            // Use injected config service
            var dotaPath = _configService.GetLastTargetPath();

            if (!string.IsNullOrEmpty(dotaPath))
            {
                var tempPath = Path.Combine(dotaPath, "game", "_ArdysaMods", "_temp");
                if (Directory.Exists(tempPath) || TryCreateDirectory(tempPath))
                {
                    return Path.Combine(tempPath, "hero_selections.json");
                }
            }

            // Fallback to AppData
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

        /// <summary>
        /// Update status text in JavaScript.
        /// </summary>
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

        /// <summary>
        /// Execute JavaScript safely.
        /// </summary>
        private async Task ExecuteScriptAsync(string script)
        {
            if (_webView?.CoreWebView2 == null) return;
            try
            {
                await _webView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch { }
        }

        /// <summary>
        /// Get the gallery HTML from file or generate fallback.
        /// </summary>
        private string GetGalleryHtml()
        {
            string appPath = AppDomain.CurrentDomain.BaseDirectory;
            string htmlPath = Path.Combine(appPath, "Assets", "Html", "hero_gallery.html");

            if (File.Exists(htmlPath))
            {
                return File.ReadAllText(htmlPath);
            }

            // Fallback HTML
            return @"<!DOCTYPE html>
<html><head><style>
body { background: #000; color: #fff; font-family: 'JetBrains Mono', monospace; display: flex; align-items: center; justify-content: center; min-height: 100vh; }
</style></head><body>
<div>Failed to load hero_gallery.html</div>
</body></html>";
        }
    }
}
