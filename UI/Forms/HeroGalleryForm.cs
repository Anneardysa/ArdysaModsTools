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
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Cache;
using ArdysaModsTools.Models;

namespace ArdysaModsTools.UI.Forms
{
    /// <summary>
    /// Modern WebView2-based hero selection gallery with Tailwind CSS styling.
    /// Replaces the traditional WinForms SelectHero form with a beautiful web UI.
    /// </summary>
    public partial class HeroGalleryForm : Form
    {
        private WebView2? _webView;
        private bool _initialized;
        private bool _isGenerating;
        private TaskCompletionSource<bool>? _alertDismissed;

        // Data
        private List<HeroModel> _heroes = new();
        private Dictionary<string, int> _selections = new(); // heroId -> setIndex
        private HashSet<string> _favorites = new(StringComparer.OrdinalIgnoreCase);
        private readonly HeroService _heroService;
        private readonly IConfigService _configService;
        private DateTime _generationStartTime;
        private int _heroCount;

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
                // Use temp folder for WebView2 user data
                string tempPath = Path.Combine(Path.GetTempPath(), "ArdysaModsTools.WebView2");
                var env = await CoreWebView2Environment.CreateAsync(null, tempPath);

                await _webView!.EnsureCoreWebView2Async(env);
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = 
                    System.Diagnostics.Debugger.IsAttached; // Enable DevTools in debug mode

                // Handle messages from JavaScript
                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

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

                var timeoutTask = Task.Delay(10000);
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
                _heroes = ConvertToHeroModels(heroSummaries);

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
                    
                    // If we couldn't find set thumbnail but have hero, use hero thumbnail as fallback
                    var heroThumbnail = hero != null ? GetHeroThumbnail(hero) : null;
                    
                    return new
                    {
                        heroId = u.HeroId,
                        heroName = hero?.DisplayName ?? hero?.Name ?? FormatHeroIdAsName(u.HeroId),
                        heroThumbnail = heroThumbnail,
                        setName = u.SetName,
                        setIndex = setIndex,
                        setThumbnail = setThumbnail ?? heroThumbnail, // Fallback to hero thumbnail
                        addedDate = u.AddedDate.ToString("yyyy-MM-dd"),
                        daysAgo = (int)(DateTime.Now - u.AddedDate).TotalDays,
                        isValid = hero != null && setIndex >= 0
                    };
                })
                // Keep original order from JSON (newest sets first)
                // Only filter out entries where hero lookup completely failed
                .Where(u => u.heroThumbnail != null)
                .ToList();
                
                if (jsUpdates.Count > 0)
                {
                    var json = JsonSerializer.Serialize(jsUpdates, _jsonOptions);
                    await ExecuteScriptAsync($"loadLatestUpdates({json})");
                    var validCount = jsUpdates.Count(u => u.isValid);
                    var withThumb = jsUpdates.Count(u => u.setThumbnail != null && !u.setThumbnail.Contains("steamstatic.com"));
                    System.Diagnostics.Debug.WriteLine($"[SetUpdate] Loaded {jsUpdates.Count} updates ({validCount} valid, {withThumb} with set thumbnails)");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadSetUpdatesAsync error: {ex.Message}");
                // Don't fail silently - just skip updates section
            }
        }

        /// <summary>
        /// Format a hero ID like "npc_dota_hero_crystal_maiden" to "Crystal Maiden".
        /// </summary>
        private static string FormatHeroIdAsName(string heroId)
        {
            var name = heroId.Replace("npc_dota_hero_", "").Replace("_", " ");
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);
        }


        /// <summary>
        /// Convert HeroSummary list to HeroModel list (same logic as SelectHero).
        /// </summary>
        private List<HeroModel> ConvertToHeroModels(List<HeroSummary> summaries)
        {
            var result = new List<HeroModel>();

            foreach (var hs in summaries)
            {
                var internalId = !string.IsNullOrWhiteSpace(hs.UsedByHeroes) ? hs.UsedByHeroes : hs.Name ?? "";
                var friendlyName = hs.Name ?? internalId;

                var hm = new HeroModel
                {
                    HeroId = internalId,  // This is the npc_dota_hero_xxx ID
                    Name = internalId,     // Keep for backwards compatibility  
                    LocalizedName = friendlyName,
                    PrimaryAttribute = !string.IsNullOrWhiteSpace(hs.PrimaryAttr) ? hs.PrimaryAttr.ToLowerInvariant() : "universal"
                };

                // Copy sets
                if (hs.Sets != null && hs.Sets.Count > 0)
                {
                    hm.Sets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kvp in hs.Sets)
                    {
                        hm.Sets[kvp.Key] = kvp.Value?.ToList() ?? new List<string>();
                    }
                }

                // Extract item IDs (parse from string array)
                if (hs.Ids != null && hs.Ids.Length > 0)
                {
                    hm.ItemIds.Clear();
                    var parsedIds = hs.Ids
                        .Select(id => int.TryParse(id, out var n) ? n : (int?)null)
                        .Where(n => n.HasValue)
                        .Select(n => n!.Value)
                        .Distinct()
                        .OrderBy(x => x);
                    hm.ItemIds.AddRange(parsedIds);
                }

                result.Add(hm);
            }

            return result;
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
        /// Preload set thumbnails into cache with HTML overlay showing progress.
        /// Two-phase: 1) Check freshness of cached items, 2) Download missing items.
        /// </summary>
        private async Task PreloadThumbnailsAsync(List<HeroModel> heroes)
        {
            var cacheService = AssetCacheService.Instance;
            var allUrls = new List<string>();

            // Collect all set thumbnail URLs
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
                        allUrls.Add(thumbUrl);
                }
            }

            if (allUrls.Count == 0)
            {
                await ExecuteScriptAsync("hideCachingOverlay()");
                return;
            }

            // Separate cached vs not cached
            var cached = allUrls.Where(u => cacheService.IsCached(u)).ToList();
            var notCached = allUrls.Where(u => !cacheService.IsCached(u)).ToList();

            // If nothing to do, hide quickly
            if (cached.Count == 0 && notCached.Count == 0)
            {
                await ExecuteScriptAsync("hideCachingOverlay()");
                return;
            }

            // Show caching overlay
            await ExecuteScriptAsync("showCachingOverlay()");
            
            try
            {
                int refreshed = 0, downloaded = 0;

                // Phase 1: Check freshness of cached items (quick HEAD requests)
                if (cached.Count > 0)
                {
                    await ExecuteScriptAsync($"document.getElementById('cachingStatus').textContent = 'Checking for updates...'");
                    
                    var refreshProgress = new Progress<(int current, int total, string url)>(async p =>
                    {
                        try { await ExecuteScriptAsync($"updateCachingProgress({p.current}, {p.total})"); }
                        catch { }
                    });

                    var refreshResult = await cacheService.RefreshStaleAssetsAsync(cached, refreshProgress);
                    refreshed = refreshResult.refreshed;
                    
                    System.Diagnostics.Debug.WriteLine($"[HeroGallery] Refreshed {refreshed} stale assets");
                }

                // Phase 2: Download missing items
                if (notCached.Count > 0)
                {
                    await ExecuteScriptAsync($"document.getElementById('cachingStatus').textContent = 'Downloading thumbnails...'");
                    await ExecuteScriptAsync($"updateCachingProgress(0, {notCached.Count})");

                    var downloadProgress = new Progress<(int current, int total, string url)>(async p =>
                    {
                        try { await ExecuteScriptAsync($"updateCachingProgress({p.current}, {p.total})"); }
                        catch { }
                    });

                    var downloadResult = await cacheService.PreloadAssetsWithProgressAsync(notCached, downloadProgress);
                    downloaded = downloadResult.downloaded;
                    
                    System.Diagnostics.Debug.WriteLine($"[HeroGallery] Downloaded {downloaded} new assets");
                }

                System.Diagnostics.Debug.WriteLine($"[HeroGallery] Total: {refreshed} refreshed, {downloaded} downloaded, {cached.Count - refreshed} fresh from cache");
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
                        HandleSelectionChanged(message);
                        break;

                    case "favoritesChanged":
                        HandleFavoritesChanged(message);
                        break;

                    case "generate":
                        await HandleGenerateAsync();
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
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebMessage error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle selection changes from JavaScript.
        /// </summary>
        private void HandleSelectionChanged(JsonElement message)
        {
            _selections.Clear();

            if (message.TryGetProperty("selections", out var selectionsEl))
            {
                foreach (var prop in selectionsEl.EnumerateObject())
                {
                    if (prop.Value.TryGetInt32(out var setIndex))
                    {
                        _selections[prop.Name] = setIndex;
                    }
                }
            }
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
                // Get current selections from message
                Dictionary<string, int> selectionsToSave = new();
                if (message.TryGetProperty("selections", out var selectionsEl))
                {
                    foreach (var prop in selectionsEl.EnumerateObject())
                    {
                        if (prop.Value.TryGetInt32(out var setIndex))
                        {
                            selectionsToSave[prop.Name] = setIndex;
                        }
                    }
                }

                if (selectionsToSave.Count == 0)
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
                    var json = JsonSerializer.Serialize(selectionsToSave, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(sfd.FileName, json);
                    await UpdateStatusAsync($"Saved {selectionsToSave.Count} selection(s)");
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
                    var loadedSelections = JsonSerializer.Deserialize<Dictionary<string, int>>(json);

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

        /// <summary>
        /// Handle generate button click from JavaScript.
        /// </summary>
        private async Task HandleGenerateAsync()
        {
            if (_isGenerating) return;
            _isGenerating = true;

            try
            {
                // Build selections list
                var heroesWithSets = new List<(HeroModel hero, string setName)>();

                foreach (var kvp in _selections)
                {
                    var hero = _heroes.FirstOrDefault(h => 
                        string.Equals(h.Id, kvp.Key, StringComparison.OrdinalIgnoreCase));
                    
                    if (hero?.Sets == null || hero.Sets.Count <= kvp.Value) continue;

                    var setName = hero.Sets.Keys.ElementAtOrDefault(kvp.Value);
                    if (string.IsNullOrEmpty(setName)) continue;

                    heroesWithSets.Add((hero, setName));
                }

                if (heroesWithSets.Count == 0)
                {
                    await UpdateStatusAsync("Please select at least one hero");
                    return;
                }

                // Get target path from injected config service
                var targetPath = _configService.GetLastTargetPath();
                if (string.IsNullOrWhiteSpace(targetPath))
                {
                    MessageBox.Show("No Dota 2 path set. Please set it in the main window first.",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Filter out default sets for preview
                var previewItems = heroesWithSets
                    .Where(x => !x.setName.Equals("Default Set", StringComparison.OrdinalIgnoreCase))
                    .Select(x =>
                    {
                        string? thumbUrl = null;
                        if (x.hero.Sets != null && x.hero.Sets.TryGetValue(x.setName, out var urls) && urls != null)
                        {
                            thumbUrl = urls.FirstOrDefault(u =>
                                u != null && (u.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                              u.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                              u.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)));
                        }
                        return (x.hero, x.setName, thumbUrl);
                    })
                    .ToList();

                if (previewItems.Count == 0)
                {
                    await _webView!.CoreWebView2.ExecuteScriptAsync("showAlert('No Selections', 'No custom sets selected. Only Default Set entries found.\\nSelect at least one custom skin to generate.')");
                    return;
                }

                // Show preview dialog
                using var previewForm = new GenerationPreviewForm(previewItems);
                var result = previewForm.ShowDialog(this);
                if (result != DialogResult.OK || !previewForm.Confirmed)
                {
                    return;
                }

                // Save selections before generating
                await SaveSelectionsAsync();

                // Track for result logging
                _generationStartTime = DateTime.Now;
                _heroCount = heroesWithSets.Count;

                // Run generation with progress overlay
                var operationResult = await ProgressOperationRunner.RunAsync(
                    this,
                    $"Preparing to generate {heroesWithSets.Count} hero set(s)...",
                    async (context) =>
                    {
                        var genService = new HeroGenerationService();

                        var stageProgress = new Progress<(int percent, string stage)>(p =>
                        {
                            context.Status.Report(p.stage);
                            context.Progress.Report(p.percent);
                        });

                        return await genService.GenerateBatchAsync(
                            targetPath,
                            heroesWithSets,
                            s => context.Substatus.Report(s),
                            null,
                            stageProgress,
                            context.Speed,
                            context.Token);
                    },
                    hideDownloadSpeed: true);

                // Handle results
                if (operationResult.Success)
                {
                    var messageBuilder = new System.Text.StringBuilder();
                    messageBuilder.AppendLine(operationResult.Message ?? "Installation complete!");

                    if (operationResult.FailedItems != null && operationResult.FailedItems.Count > 0)
                    {
                        messageBuilder.AppendLine();
                        messageBuilder.AppendLine("Failed sets:");
                        foreach (var (name, reason) in operationResult.FailedItems)
                        {
                            messageBuilder.AppendLine($"  • {name}: {reason}");
                        }
                    }

                    var alertMessage = messageBuilder.ToString().TrimEnd()
                        .Replace("'", "\\'")
                        .Replace("\n", "\\n")
                        .Replace("\r", "");
                    
                    var iconType = operationResult.FailedItems?.Count > 0 ? "warning" : "success";
                    
                    // Set up wait for alert dismissal
                    _alertDismissed = new TaskCompletionSource<bool>();
                    
                    await _webView!.CoreWebView2.ExecuteScriptAsync(
                        $"showAlert('Generation Complete', '{alertMessage}', '{iconType}', true)");

                    // Wait for user to click OK (with timeout fallback)
                    var timeoutTask = Task.Delay(60000); // 60 second timeout
                    await Task.WhenAny(_alertDismissed.Task, timeoutTask);
                    
                    // Store result for MainForm logging
                    GenerationResult = new ModGenerationResult
                    {
                        Success = true,
                        Type = GenerationType.SkinSelector,
                        OptionsCount = _heroCount,
                        Duration = DateTime.Now - _generationStartTime,
                        Details = $"{_heroCount} hero set(s)" + (operationResult.FailedItems?.Count > 0 ? $", {operationResult.FailedItems.Count} failed" : "")
                    };
                    
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else if (operationResult.Message != "Operation cancelled by user.")
                {
                    // Store failed result
                    GenerationResult = new ModGenerationResult
                    {
                        Success = false,
                        Type = GenerationType.SkinSelector,
                        OptionsCount = _heroCount,
                        Duration = DateTime.Now - _generationStartTime,
                        ErrorMessage = operationResult.Message
                    };
                    
                    using var errorDialog = new ErrorLogDialog(
                        "Generation Failed",
                        "Copy the log below and send to developer for help.",
                        operationResult.Message ?? "Unknown error");
                    errorDialog.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                using var errorDialog = new ErrorLogDialog(
                    "Unexpected Error",
                    "An unexpected error occurred. Copy the log and send to developer.",
                    $"Error: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}");
                errorDialog.ShowDialog(this);
            }
            finally
            {
                _isGenerating = false;
                await UpdateStatusAsync("Ready");
            }
        }

        /// <summary>
        /// Save selections to settings file.
        /// </summary>
        private async Task SaveSelectionsAsync()
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

        /// <summary>
        /// Restore selections from settings file.
        /// </summary>
        private async Task RestoreSelectionsAsync()
        {
            try
            {
                var path = GetSettingsPath();
                if (!File.Exists(path)) return;

                var json = await File.ReadAllTextAsync(path);
                var selections = JsonSerializer.Deserialize<Dictionary<string, int>>(json) 
                    ?? new Dictionary<string, int>();

                _selections = selections;

                // Send to JavaScript
                var selectionsJson = JsonSerializer.Serialize(_selections, _jsonOptions);
                await ExecuteScriptAsync($"loadSelections({selectionsJson})");
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
        private async Task UpdateStatusAsync(string status)
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
body { background: #000; color: #fff; font-family: monospace; display: flex; align-items: center; justify-content: center; min-height: 100vh; }
</style></head><body>
<div>Failed to load hero_gallery.html</div>
</body></html>";
        }
    }
}
