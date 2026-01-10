using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Models;
using ArdysaModsTools.UI.Controls.Widgets;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZstdSharp.Unsafe;

namespace ArdysaModsTools.UI.Forms
{
    public partial class SelectHero : Form
    {
        private readonly HashSet<string> favorites;
        private string _currentCategory = "all";
        private string _searchFilter = ""; // Track current search filter
        // start empty — we'll populate from heroes.json on first shown
        private readonly List<HeroModel> heroList = new List<HeroModel>();

        private readonly Dictionary<string, string> selectedByHero = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private bool _populated = false;
        private bool _isGenerating = false; // Prevent duplicate generation
        private const int RowSpacing = 18;

        // Hero lookup for O(1) access by ID (built once after loading)
        private Dictionary<string, HeroModel> _heroLookup = new Dictionary<string, HeroModel>(StringComparer.OrdinalIgnoreCase);

        // Category color mapping (category key -> highlight color)
        private readonly System.Collections.Generic.Dictionary<string, Color> _categoryColors =
            new System.Collections.Generic.Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            { "all", Color.FromArgb(170, 130, 140) },
            { "favorite", Color.HotPink },        // Pink
            { "strength", Color.FromArgb(220, 60, 60) },   // Red-ish
            { "agility", Color.FromArgb(80, 200, 80) },    // Green-ish
            { "intelligence", Color.FromArgb(80, 140, 220) }, // Blue-ish
            { "universal", Color.FromArgb(240, 200, 60) }  // Yellow-ish
        };

        // HeroService instance (loads heroes.json robustly)
        private readonly HeroService _heroService;

        public SelectHero()
        {
            InitializeComponent();

            if (System.Diagnostics.Debugger.IsAttached) ShowHeroesJsonStatus();

            // ----- favorites (unchanged) -----
            var loadedFav = FavoritesStore.Load();
            favorites = loadedFav ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var baseFolder = AppDomain.CurrentDomain.BaseDirectory;
            _heroService = new HeroService(baseFolder);

            // ----- rest of original initialization -----
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            KeyPreview = true;
            KeyDown += SelectHero_KeyDown;

            WireButtonsAndLabels();

            // Populate after shown so sizes are final
            Shown += SelectHero_Shown;

            // if form resized, update row widths
            this.Resize += (s, e) => UpdateRowWidths();
            this.ScrollContainer.Resize += (s, e) => UpdateRowWidths();

            InitializeUiBehavior();
            InitializeLayoutHandlers();

            // Apply font fallback if JetBrains Mono not installed
            FontHelper.ApplyToForm(this);

            // Wire up modern search box
            modernSearchBox.SearchTextChanged += ModernSearchBox_SearchTextChanged;

            FormClosing += (s, e) => FavoritesStore.Save(favorites);
        }

        #region Initialization helpers (clean, single-responsibility)

        private void InitializeUiBehavior()
        {
            WireButtonsAndLabels();

            // Populate after shown so sizes are final
            Shown += SelectHero_Shown;

            // actions - NOTE: btn_SelectGenerate.Click is already wired in Designer.cs
            btn_SelectLoad.Click += Btn_SelectLoad_Click;
            btn_SelectSave.Click += Btn_SelectSave_Click;
        }

        private void InitializeLayoutHandlers()
        {
            // if form resized, update row widths
            this.Resize += (s, e) => UpdateRowWidths();
            this.ScrollContainer.Resize += (s, e) => UpdateRowWidths();
        }

        #endregion

        /// <summary>
        /// On first shown: load heroes.json (if present) via HeroService (async)
        /// and then populate the UI.
        /// </summary>
        private async void SelectHero_Shown(object? sender, EventArgs e)
        {
            try
            {
                if (_populated) return;
                _populated = true;

                SetStatus("Loading heroes...");

                // Try to load heroes.json using HeroService
                try
                {
                    AppendDebug("Attempting to load heroes.json via HeroService...");
                    var list = await _heroService.LoadHeroesAsync();
                    if (list != null && list.Count > 0)
                    {
                        // Map HeroSummary -> your UI's HeroModel (preserve original shape)
                        heroList.Clear();
                        // Robust mapping (fixed) starts here
                        foreach (var hs in list)
                        {
                            // prefer used_by_heroes (npc string) as the internal id; fallback to Name
                            var internalId = !string.IsNullOrWhiteSpace(hs.UsedByHeroes) ? hs.UsedByHeroes : hs.Name ?? "";

                            // friendly display name
                            var friendlyName = hs.Name ?? internalId;

                            // detect primary attribute via reflection (try common property names)
                            string primaryAttr = "universal";
                            try
                            {
                                var paProp = hs.GetType().GetProperty("PrimaryAttribute")
                                             ?? hs.GetType().GetProperty("primary_attr")
                                             ?? hs.GetType().GetProperty("primaryAttr")
                                             ?? hs.GetType().GetProperty("PrimaryAttr");

                                if (paProp != null)
                                {
                                    var paVal = paProp.GetValue(hs) as string;
                                    if (!string.IsNullOrWhiteSpace(paVal)) primaryAttr = paVal;
                                }
                            }
                            catch { /* best-effort */ }

                            var hm = new HeroModel
                            {
                                Name = internalId,           // keep compatibility where HeroModel.Id returns Name
                                LocalizedName = friendlyName,
                                PrimaryAttribute = string.IsNullOrWhiteSpace(primaryAttr) ? "universal" : primaryAttr.ToLowerInvariant()
                            };

                            // --- 1) copy 'sets' robustly ---
                            try
                            {
                                hm.Sets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

                                var setsProp = hs.GetType().GetProperty("Sets") ?? hs.GetType().GetProperty("sets");
                                if (setsProp != null)
                                {
                                    var setsVal = setsProp.GetValue(hs);
                                    if (setsVal != null)
                                    {
                                        // IDictionary-like (most common)
                                        if (setsVal is System.Collections.IDictionary dict)
                                        {
                                            foreach (var keyObj in dict.Keys)
                                            {
                                                var keyName = keyObj?.ToString();
                                                if (string.IsNullOrEmpty(keyName)) continue;

                                                var valuesObj = dict[keyObj!];
                                                var valuesList = new List<string>();
                                                if (valuesObj is System.Collections.IEnumerable enumVal && !(valuesObj is string))
                                                {
                                                    foreach (var v in enumVal)
                                                    {
                                                        var sVal = v?.ToString();
                                                        if (!string.IsNullOrEmpty(sVal)) valuesList.Add(sVal);
                                                    }
                                                }
                                                else
                                                {
                                                    var sVal = valuesObj?.ToString();
                                                    if (!string.IsNullOrEmpty(sVal)) valuesList.Add(sVal);
                                                }

                                                if (valuesList.Count > 0) hm.Sets[keyName] = valuesList;
                                            }
                                        }
                                        // enumerable of JsonProperty or KV pairs (JsonElement or reflection)
                                        else if (setsVal is System.Collections.IEnumerable enumSets && !(setsVal is string))
                                        {
                                            foreach (var kv in enumSets)
                                            {
                                                if (kv == null) continue;
                                                var kvType = kv.GetType();

                                                // JsonProperty case (from System.Text.Json when enumerating an object)
                                                if (kvType.Name == "JsonProperty")
                                                {
                                                    var nm = kvType.GetProperty("Name")?.GetValue(kv)?.ToString();
                                                    var rawVal = kvType.GetProperty("Value")?.GetValue(kv);
                                                    if (string.IsNullOrEmpty(nm) || rawVal == null) continue;

                                                    if (rawVal is System.Text.Json.JsonElement je)
                                                    {
                                                        var valuesList = new List<string>();
                                                        if (je.ValueKind == System.Text.Json.JsonValueKind.Array)
                                                        {
                                                            foreach (var el in je.EnumerateArray())
                                                            {
                                                                if (el.ValueKind == System.Text.Json.JsonValueKind.String) valuesList.Add(el.GetString() ?? "");
                                                                else valuesList.Add(el.ToString());
                                                            }
                                                        }
                                                        else if (je.ValueKind == System.Text.Json.JsonValueKind.String)
                                                        {
                                                            valuesList.Add(je.GetString() ?? "");
                                                        }

                                                        if (valuesList.Count > 0) hm.Sets[nm] = valuesList;
                                                    }
                                                }
                                                else
                                                {
                                                    // KeyValuePair-like (Key and Value props)
                                                    var keyProp = kvType.GetProperty("Key") ?? kvType.GetProperty("Name");
                                                    var valueProp = kvType.GetProperty("Value");
                                                    if (keyProp != null && valueProp != null)
                                                    {
                                                        var k = keyProp.GetValue(kv)?.ToString();
                                                        var v = valueProp.GetValue(kv);
                                                        if (string.IsNullOrEmpty(k) || v == null) continue;

                                                        var valuesList = new List<string>();
                                                        if (v is System.Collections.IEnumerable ev && !(v is string))
                                                        {
                                                            foreach (var eitem in ev)
                                                            {
                                                                var s = eitem?.ToString();
                                                                if (!string.IsNullOrEmpty(s)) valuesList.Add(s);
                                                            }
                                                        }
                                                        else
                                                        {
                                                            var s = v?.ToString();
                                                            if (!string.IsNullOrEmpty(s)) valuesList.Add(s);
                                                        }

                                                        if (valuesList.Count > 0) hm.Sets[k] = valuesList;
                                                    }
                                                }
                                            }
                                        }
                                        // JsonElement fallback
                                        else if (setsVal is System.Text.Json.JsonElement jeRoot && jeRoot.ValueKind == System.Text.Json.JsonValueKind.Object)
                                        {
                                            foreach (var prop in jeRoot.EnumerateObject())
                                            {
                                                var valuesList = new List<string>();
                                                if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Array)
                                                {
                                                    foreach (var el in prop.Value.EnumerateArray())
                                                    {
                                                        if (el.ValueKind == System.Text.Json.JsonValueKind.String) valuesList.Add(el.GetString() ?? "");
                                                        else valuesList.Add(el.ToString());
                                                    }
                                                }
                                                else if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                                                {
                                                    valuesList.Add(prop.Value.GetString() ?? "");
                                                }

                                                if (valuesList.Count > 0) hm.Sets[prop.Name] = valuesList;
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
#if DEBUG
                                System.Diagnostics.Debug.WriteLine("[SelectHero] sets copy error: " + ex.Message);
#endif
                            }

                            // fallback if sets empty: attempt to coerce into "Default Set"
                            if (hm.Sets == null || hm.Sets.Count == 0)
                            {
                                try
                                {
                                    var fallbackItems = new List<string>();
                                    var maybeSets = hs.GetType().GetProperty("Sets")?.GetValue(hs) ?? hs.GetType().GetProperty("sets")?.GetValue(hs);
                                    if (maybeSets is System.Collections.IEnumerable enumMaybe && !(maybeSets is string))
                                    {
                                        foreach (var v in enumMaybe)
                                        {
                                            var s = v?.ToString();
                                            if (!string.IsNullOrEmpty(s)) fallbackItems.Add(s);
                                        }
                                    }

                                    if (fallbackItems.Count > 0)
                                    {
                                        hm.Sets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Default Set", fallbackItems }
                };
                                    }
                                }
                                catch { /* swallow fallback errors */ }
                            }

                            // --- 2) extract numeric item IDs ---
                            var foundIds = new List<int>();
                            var propNames = new[] { "ItemIds", "Ids", "id", "ids", "ID", "item_ids", "itemIds" };
                            foreach (var pn in propNames)
                            {
                                var pi = hs.GetType().GetProperty(pn);
                                if (pi == null) continue;
                                var val = pi.GetValue(hs);
                                if (val == null) continue;

                                if (val is System.Collections.IEnumerable enumVal && !(val is string))
                                {
                                    foreach (var it in enumVal)
                                    {
                                        if (it == null) continue;
                                        if (it is int ii) { foundIds.Add(ii); continue; }
                                        if (it is long ll) { foundIds.Add((int)ll); continue; }
                                        if (it is string s)
                                        {
                                            if (int.TryParse(s, out var p)) foundIds.Add(p);
                                            continue;
                                        }
                                        if (it is System.Text.Json.JsonElement je)
                                        {
                                            try
                                            {
                                                if (je.ValueKind == System.Text.Json.JsonValueKind.Number && je.TryGetInt32(out var jn))
                                                    foundIds.Add(jn);
                                                else if (je.ValueKind == System.Text.Json.JsonValueKind.String)
                                                {
                                                    var st = je.GetString();
                                                    if (int.TryParse(st, out var pn2)) foundIds.Add(pn2);
                                                }
                                            }
                                            catch { }
                                        }
                                    }
                                }
                                else
                                {
                                    if (val is int vi) foundIds.Add(vi);
                                    else if (val is long vl) foundIds.Add((int)vl);
                                    else if (val is string vs && int.TryParse(vs, out var vp)) foundIds.Add(vp);
                                    else if (val is System.Text.Json.JsonElement je)
                                    {
                                        try
                                        {
                                            if (je.ValueKind == System.Text.Json.JsonValueKind.Number && je.TryGetInt32(out var jn))
                                                foundIds.Add(jn);
                                            else if (je.ValueKind == System.Text.Json.JsonValueKind.String)
                                            {
                                                var st = je.GetString();
                                                if (int.TryParse(st, out var pn2)) foundIds.Add(pn2);
                                            }
                                        }
                                        catch { }
                                    }
                                }

                                if (foundIds.Count > 0) break;
                            }

                            if (foundIds.Count > 0)
                            {
                                hm.ItemIds.Clear();
                                hm.ItemIds.AddRange(foundIds.Distinct().OrderBy(x => x));
                            }
                            else
                            {
#if DEBUG
                                System.Diagnostics.Debug.WriteLine($"[SelectHero] Warning: hero '{hm.DisplayName}' ({internalId}) has no item IDs from HeroService — generator will fail unless heroes.json provides 'id'.");
#endif
                            }

                            // Add to final list
                            heroList.Add(hm);
                        }
                        // Robust mapping ends here

                        AppendDebug($"Loaded {heroList.Count} heroes from heroes.json.");
                    }
                    else
                    {
                        AppendDebug("HeroService returned empty list — using embedded sample heroes.");
                    }
                }
                catch (Exception ex)
                {
                    AppendDebug("HeroService load failed: " + ex.Message + " — falling back to embedded sample heroes.");
                    // keep embedded sample heroList (no further action)
                }

                // After ensuring heroList is prepared, build lookup for fast access
                _heroLookup = heroList.ToDictionary(h => h.Id, h => h, StringComparer.OrdinalIgnoreCase);

                PopulateHeroes();

                // Restore previous selections from AppData
                await RestoreSelectionsAsync();
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine("SelectHero_Shown error: " + ex);
#endif
            }
        }

        private void WireButtonsAndLabels()
        {
            // category labels
            lbl_All.Click += (s, e) => ApplyCategoryFilter("all");
            lbl_Favorite.Click += (s, e) => ApplyCategoryFilter("favorite");
            lbl_Strength.Click += (s, e) => ApplyCategoryFilter("strength");
            lbl_Agility.Click += (s, e) => ApplyCategoryFilter("agility");
            lbl_Intelligence.Click += (s, e) => ApplyCategoryFilter("intelligence");
            lbl_Universal.Click += (s, e) => ApplyCategoryFilter("universal");
        }

        private void SelectHero_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Generate handler using HeroGenerationService.
        /// Flow: Extract VPK → (Download + Merge + Patch each hero) → Recompile → Replace
        /// </summary>
        private async void Btn_SelectGenerate_Click(object? sender, EventArgs e)
        {
            // Prevent duplicate generation (double-click protection)
            if (_isGenerating)
            {
                return;
            }
            _isGenerating = true;

            var selections = GetSelections().ToList();
            if (!selections.Any())
            {
                MessageBox.Show("No selections made. Select skins before generating.");
                _isGenerating = false;
                return;
            }

            // Get target path from ConfigService
            var configService = new ConfigService();
            var targetPath = configService.GetLastTargetPath();
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                MessageBox.Show("No Dota 2 path set. Please set it in the main window first.  ");
                _isGenerating = false;
                return;
            }

            // Sort selections by hero order in heroList (top to bottom)
            var orderedSelections = selections
                .OrderBy(s => heroList.FindIndex(h =>
                    string.Equals(h.Id, s.heroId, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            // Build list of (HeroModel, setName) tuples for batch processing
            var heroesWithSets = new List<(HeroModel hero, string setName)>();
            var invalidSelections = new List<string>();

            foreach (var sel in orderedSelections)
            {
                var hero = heroList.FirstOrDefault(h =>
                    string.Equals(h.Id, sel.heroId, StringComparison.OrdinalIgnoreCase));

                if (hero == null)
                {
                    invalidSelections.Add(sel.heroId);
                    continue;
                }

                if (hero.Sets == null || !hero.Sets.ContainsKey(sel.skinId))
                {
                    invalidSelections.Add($"{hero.DisplayName}: {sel.skinId}");
                    continue;
                }

                heroesWithSets.Add((hero, sel.skinId));
            }

            if (heroesWithSets.Count == 0)
            {
                MessageBox.Show($"No valid hero selections found. Invalid: {string.Join(",", invalidSelections)}");
                _isGenerating = false;
                return;
            }

            // Show preview dialog with thumbnails
            var previewItems = new List<(HeroModel hero, string setName, string? thumbnailUrl)>();
            foreach (var (hero, setName) in heroesWithSets)
            {
                // Find thumbnail URL (first image URL in set)
                string? thumbUrl = null;
                if (hero.Sets != null && hero.Sets.TryGetValue(setName, out var urls) && urls != null)
                {
                    thumbUrl = urls.FirstOrDefault(u =>
                        u != null && (u.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                      u.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                      u.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)));
                }
                previewItems.Add((hero, setName, thumbUrl));
            }

            using (var previewForm = new GenerationPreviewForm(previewItems))
            {
                var result = previewForm.ShowDialog(this);
                if (result != DialogResult.OK || !previewForm.Confirmed)
                {
                    _isGenerating = false;
                    // Small delay to prevent rapid re-trigger from button focus
                    await Task.Delay(100);
                    return;
                }
            }

            // Disable UI
            SetUiEnabled(false);

            // Capture logs during generation for error reporting
            var generationLogs = new System.Text.StringBuilder();
            generationLogs.AppendLine($"=== Generation Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            generationLogs.AppendLine($"Target Path: {targetPath}");
            generationLogs.AppendLine($"Heroes Selected: {heroesWithSets.Count}");
            generationLogs.AppendLine();

            try
            {
                var operationResult = await ProgressOperationRunner.RunAsync(
                    this,
                    $"Preparing to generate {heroesWithSets.Count} hero set(s)...",
                    async (context) =>
                    {
                        var genService = new HeroGenerationService();

                        // Multi-stage progress handler
                        var stageProgress = new Progress<(int percent, string stage)>(p =>
                        {
                            context.Status.Report(p.stage);
                            context.Progress.Report(p.percent);
                        });

                        // Run batch generation with log capture
                        return await genService.GenerateBatchAsync(
                            targetPath,
                            heroesWithSets,
                            s => { 
                                context.Substatus.Report(s);
                                generationLogs.AppendLine(s); // Capture log
                            },
                            null,
                            stageProgress,
                            context.Speed,
                            context.Token);
                    },
                    hideDownloadSpeed: true);

                // Handle results
                if (operationResult.Success)
                {
                    // Successful generation
                    await SaveSelectionsAsync();

                    // Build detailed message including failed items
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

                    MessageBox.Show(messageBuilder.ToString().TrimEnd(),
                        "Generation Complete", MessageBoxButtons.OK,
                        operationResult.FailedItems?.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

                    // Auto-close the SelectHero form after successful generation
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    // Generation failed - show ErrorLogDialog with captured logs
                    if (operationResult.Message != "Operation cancelled by user.")
                    {
                        generationLogs.AppendLine();
                        generationLogs.AppendLine($"=== Error: {operationResult.Message} ===");
                        generationLogs.AppendLine($"=== Failed at: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                        
                        using var errorDialog = new ErrorLogDialog(
                            "Generation Failed",
                            "Copy the log below and send to developer for help.",
                            generationLogs.ToString());
                        errorDialog.ShowDialog(this);
                    }
                }
            }
            catch (Exception ex)
            {
                generationLogs.AppendLine();
                generationLogs.AppendLine($"=== EXCEPTION ===");
                generationLogs.AppendLine($"Type: {ex.GetType().Name}");
                generationLogs.AppendLine($"Message: {ex.Message}");
                generationLogs.AppendLine($"StackTrace: {ex.StackTrace}");
                
                using var errorDialog = new ErrorLogDialog(
                    "Unexpected Error",
                    "An unexpected error occurred. Copy the log and send to developer.",
                    generationLogs.ToString());
                errorDialog.ShowDialog(this);
                
                FallbackLogger.Log($"SelectHero generation error: {ex}");
            }
            finally
            {
                // Cleanup hero download cache
                CleanupHeroCache();
                
                // Re-enable UI
                SetUiEnabled(true);
                _isGenerating = false;
                SetStatus("Ready");
            }
        }

        private void SetUiEnabled(bool enabled)
        {
            modernSearchBox.Enabled = enabled;
            btn_SelectGenerate.Enabled = enabled;
            btn_SelectLoad.Enabled = enabled;
            btn_SelectSave.Enabled = enabled;
            lbl_All.Enabled = enabled;
            lbl_Favorite.Enabled = enabled;
            lbl_Strength.Enabled = enabled;
            lbl_Agility.Enabled = enabled;
            lbl_Intelligence.Enabled = enabled;
            lbl_Universal.Enabled = enabled;

            // Disable all HeroRow controls
            foreach (var row in RowsFlow.Controls.OfType<HeroRow>())
            {
                row.Enabled = enabled;
            }
        }

        private void Btn_SelectLoad_Click(object? sender, EventArgs e) => LoadPresetInteractive();
        private void Btn_SelectSave_Click(object? sender, EventArgs e) => SavePresetInteractive();

        /// <summary>
        /// </summary>
        private void ModernSearchBox_SearchTextChanged(object? sender, string searchText)
        {
            _searchFilter = searchText?.Trim() ?? "";
            ApplyFilters();
        }

        /// <summary>
        /// Apply both category and search filters to hero rows.
        /// </summary>
        private void ApplyFilters()
        {
            if (RowsFlow == null || ScrollContainer == null) return;

            RowsFlow.SuspendLayout();
            ScrollContainer.SuspendLayout();

            var rows = RowsFlow.Controls.OfType<HeroRow>().ToList();
            bool hasSearch = !string.IsNullOrWhiteSpace(_searchFilter);

            foreach (var hr in rows)
            {
                // 1) Check category filter
                bool matchesCategory = true;
                if (_currentCategory == "favorite")
                {
                    matchesCategory = favorites.Contains(hr.HeroId);
                }
                else if (_currentCategory != "all")
                {
                    if (_heroLookup.TryGetValue(hr.HeroId, out var hero))
                    {
                        var attr = (hero.PrimaryAttribute ?? "universal").ToLowerInvariant();
                        matchesCategory = attr == _currentCategory;
                    }
                }

                // 2) Check search filter
                bool matchesSearch = true;
                if (hasSearch && _heroLookup.TryGetValue(hr.HeroId, out var heroModel))
                {
                    var searchLower = _searchFilter.ToLowerInvariant();
                    matchesSearch =
                        heroModel.LocalizedName?.ToLowerInvariant().Contains(searchLower) == true ||
                        heroModel.Name?.ToLowerInvariant().Contains(searchLower) == true;
                }

                // 3) Show only if matches both filters
                hr.Visible = matchesCategory && matchesSearch;
            }

            RowsFlow.ResumeLayout(true);
            ScrollContainer.ResumeLayout(true);
            RowsFlow.PerformLayout();
            ScrollContainer.Invalidate();

            // Reset scroll to top
            try
            {
                ScrollContainer.AutoScrollPosition = new Point(0, 0);
                if (ScrollContainer.VerticalScroll != null && ScrollContainer.VerticalScroll.Visible)
                {
                    ScrollContainer.VerticalScroll.Value = Math.Max(ScrollContainer.VerticalScroll.Minimum, ScrollContainer.VerticalScroll.Value);
                    ScrollContainer.PerformLayout();
                }
            }
            catch { }
        }

        private void ApplyCategoryFilter(string cat)
        {
            _currentCategory = string.IsNullOrWhiteSpace(cat) ? "all" : cat.ToLowerInvariant();

            if (ScrollContainer == null) return;

            // 1) Update category label highlights
            var defaultColor = Theme.TitleColor;
            lbl_All.ForeColor = defaultColor;
            lbl_Favorite.ForeColor = defaultColor;
            lbl_Strength.ForeColor = defaultColor;
            lbl_Agility.ForeColor = defaultColor;
            lbl_Intelligence.ForeColor = defaultColor;
            lbl_Universal.ForeColor = defaultColor;

            if (_categoryColors.TryGetValue(_currentCategory, out var highlight))
            {
                switch (_currentCategory)
                {
                    case "all": lbl_All.ForeColor = highlight; break;
                    case "favorite": lbl_Favorite.ForeColor = highlight; break;
                    case "strength": lbl_Strength.ForeColor = highlight; break;
                    case "agility": lbl_Agility.ForeColor = highlight; break;
                    case "intelligence": lbl_Intelligence.ForeColor = highlight; break;
                    case "universal": lbl_Universal.ForeColor = highlight; break;
                }
            }

            // 2) Apply both category and search filters
            ApplyFilters();
        }

        private void PopulateHeroes()
        {
            if (RowsFlow == null) return;

            RowsFlow.SuspendLayout();
            RowsFlow.Controls.Clear();

            var list = heroList.ToList();
            int containerWidth = Math.Max(400, ScrollContainer.ClientSize.Width - ScrollContainer.Padding.Horizontal);
            int added = 0;

            foreach (var h in list)
            {
                var row = new HeroRow();
                row.Bind(h, favorites.Contains(h.Id));
                row.Tag = h.Id;
                row.TileClicked += OnTileClicked;
                row.FavoriteToggled += OnFavoriteToggled;
                row.ExpandedChanged += OnRowExpandedChanged;

                // Set width - FlowLayoutPanel handles positioning
                row.Width = containerWidth;
                row.Margin = new Padding(0);

                // Start collapsed by default
                row.SetExpanded(false);

                RowsFlow.Controls.Add(row);
                added++;

                // Auto-select: prefer "Default Set", then first available set
                if (!selectedByHero.ContainsKey(h.Id) && h.Skins != null && h.Skins.Count > 0)
                {
                    string autoSel = h.Skins.FirstOrDefault(s => s?.Contains("Default", StringComparison.OrdinalIgnoreCase) == true)
                                     ?? h.Skins.FirstOrDefault() ?? "";
                    if (!string.IsNullOrEmpty(autoSel))
                    {
                        selectedByHero[h.Id] = autoSel;
                    }
                }

                if (selectedByHero.TryGetValue(h.Id, out var sel))
                    row.ApplySelection(sel);
            }

            RowsFlow.ResumeLayout(true);

#if DEBUG
            System.Diagnostics.Debug.WriteLine($"PopulateHeroes: added={added}, RowsFlow.Height={RowsFlow.Height}");
#endif
        }

        /// <summary>
        /// Called when a HeroRow expands or collapses - implements accordion + auto-scroll.
        /// </summary>
        private void OnRowExpandedChanged(HeroRow expandedRow)
        {
            // Accordion behavior: collapse all other rows when one expands
            if (expandedRow.IsExpanded)
            {
                foreach (var row in RowsFlow.Controls.OfType<HeroRow>())
                {
                    if (row != expandedRow && row.IsExpanded)
                    {
                        row.SetExpanded(false);
                    }
                }

                // Scroll to show the expanded row
                BeginInvoke((Action)(() =>
                {
                    ScrollToRow(expandedRow);
                }));
            }

            // FlowLayoutPanel handles layout when child sizes change
            RowsFlow?.PerformLayout();
        }

        private void ScrollToRow(HeroRow row)
        {
            var rowTop = row.Top;
            var rowBottom = row.Top + row.Height;
            var containerHeight = ScrollContainer.ClientSize.Height;

            // Calculate target scroll position to center the row (or show it fully)
            int targetScroll;

            if (row.Height > containerHeight)
            {
                // Row is taller than container - scroll to top of row
                targetScroll = rowTop;
            }
            else
            {
                // Center the row in the visible area
                targetScroll = rowTop - (containerHeight - row.Height) / 2;
            }

            // Clamp to valid scroll range
            targetScroll = Math.Max(0, Math.Min(targetScroll, ScrollContainer.VerticalScroll.Maximum));

            // Apply scroll
            ScrollContainer.AutoScrollPosition = new Point(0, targetScroll);
        }

        /// <summary>
        /// Update row widths when container is resized - full width layout.
        /// </summary>
        private void UpdateRowWidths()
        {
            if (RowsFlow == null) return;

            // Use full available width (minus padding)
            int containerWidth = ScrollContainer.ClientSize.Width - ScrollContainer.Padding.Horizontal;
            int rowWidth = Math.Max(400, containerWidth);

            foreach (var row in RowsFlow.Controls.OfType<HeroRow>())
            {
                row.Width = rowWidth;
                row.Margin = new Padding(0, 4, 0, 4); // Left-aligned, no centering
                if (row.IsExpanded) row.RecalculateLayout();
            }
        }

        private void OnFavoriteToggled(string heroId, bool isFav)
        {
            if (isFav) favorites.Add(heroId); else favorites.Remove(heroId);
            FavoritesStore.Save(favorites);

            var row = RowsFlow?.Controls.OfType<HeroRow>().FirstOrDefault(r => r.HeroId == heroId);
            row?.SetFavorite(isFav);
        }

        private void OnTileClicked(string heroId, string skinId)
        {
            selectedByHero[heroId] = skinId;

            var row = RowsFlow?.Controls.OfType<HeroRow>().FirstOrDefault(r => r.HeroId == heroId);
            row?.ApplySelection(skinId);
        }

        #region Preset Save/Load
        private void SavePresetInteractive()
        {
            using var sfd = new SaveFileDialog { Title = "Save skin preset", Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*", DefaultExt = "json", AddExtension = true, FileName = "skins_preset.json" };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;
            SavePresetToFile(sfd.FileName);
            MessageBox.Show("Preset saved.");
        }

        private void LoadPresetInteractive()
        {
            using var ofd = new OpenFileDialog { Title = "Load skin preset", Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*", DefaultExt = "json" };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;
            LoadPresetFromFile(ofd.FileName);
            MessageBox.Show("Preset loaded.");
        }

        private void SavePresetToFile(string path)
        {
            var dict = selectedByHero.ToDictionary(kv => kv.Key, kv => kv.Value);
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(path, JsonSerializer.Serialize(dict, options));
        }

        private void LoadPresetFromFile(string path)
        {
            var json = File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
            selectedByHero.Clear();
            foreach (var kv in dict) selectedByHero[kv.Key] = kv.Value;

            foreach (HeroRow row in ScrollContainer.Controls.OfType<HeroRow>())
            {
                if (!string.IsNullOrEmpty(row.HeroId) && selectedByHero.TryGetValue(row.HeroId, out var sel))
                    row.ApplySelection(sel);
                else
                    row.ApplySelection(string.Empty);
            }
        }

        #endregion

        // Debug helper - paste inside SelectHero class
        private void ShowHeroesJsonStatus()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var path = Path.Combine(baseDir, "heroes.json");
                var exists = File.Exists(path);
                var msg = $"AppBase: {baseDir}\nheroes.json exists: {exists}\nPath: {path}\n";
                if (exists)
                {
                    var text = File.ReadAllText(path);
                    msg += $"Length: {text.Length} chars\nStartsWith: {text?.TrimStart().Substring(0, Math.Min(80, text.Length))}\n";
                    try
                    {
                        if (!string.IsNullOrEmpty(text))
                        {
                            using var doc = JsonDocument.Parse(text);
                            var root = doc.RootElement;
                            if (root.ValueKind == JsonValueKind.Array)
                                msg += $"Parsed root: Array (count = {root.GetArrayLength()})\n";
                            else
                                msg += $"Parsed root: {root.ValueKind}\n";
                        }
                        else
                        {
                            msg += "File is empty or contains only whitespace.\n";
                        }
                    }
                    catch (Exception ex)
                    {
                        msg += $"JSON parse error: {ex.Message}\n";
                    }
                }
                MessageBox.Show(this, msg, "heroes.json status", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Status check failed: " + ex.Message);
            }
        }

        private IEnumerable<(string heroId, string skinId)> GetSelections() => selectedByHero.Select(kv => (kv.Key, kv.Value));

        private void AppendDebug(string s)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine("[SelectHero] " + s);
#endif
        }

        /// <summary>
        /// Safe status setter — only updates lbl_Status if it exists on the form.
        /// Falls back to Debug.WriteLine if the control is not present.
        /// </summary>
        private void SetStatus(string text)
        {
            try
            {
                var fi = this.GetType().GetField("lbl_Status", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                if (fi != null)
                {
                    var lbl = fi.GetValue(this) as System.Windows.Forms.Label;
                    if (lbl != null && !lbl.IsDisposed)
                    {
                        if (lbl.InvokeRequired)
                        {
                            lbl.Invoke(new Action(() => { lbl.Text = text; lbl.Refresh(); }));
                        }
                        else
                        {
                            lbl.Text = text;
                            lbl.Refresh();
                        }
                        return;
                    }
                }

                // If no label exists, write to debug output
#if DEBUG
                System.Diagnostics.Debug.WriteLine("[Status] " + text);
#endif
            }
            catch
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine("[Status] (error updating) " + text);
#endif
            }
        }

        private void modernSearchBox_Load(object sender, EventArgs e)
        {

        }

        #region Selection Persistence

        /// <summary>
        /// Saves user's hero selections to AppData for restoration on next launch.
        /// </summary>
        private async Task SaveSelectionsAsync()
        {
            try
            {
                var settingsPath = GetSettingsPath();
                var dict = selectedByHero.ToDictionary(kv => kv.Key, kv => kv.Value);
                var json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(settingsPath, json);
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[SelectHero] Selections saved to {settingsPath}");
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[SelectHero] Failed to save selections: {ex.Message}");
#endif
            }
        }

        /// <summary>
        /// Restores user's previous hero selections from AppData.
        /// </summary>
        private async Task RestoreSelectionsAsync()
        {
            try
            {
                var settingsPath = GetSettingsPath();
                if (!File.Exists(settingsPath)) return;

                var json = await File.ReadAllTextAsync(settingsPath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

                if (dict == null || dict.Count == 0) return;

                // Apply to internal dictionary
                selectedByHero.Clear();
                foreach (var kv in dict)
                {
                    selectedByHero[kv.Key] = kv.Value;
                }

                // Apply to UI rows
                foreach (HeroRow row in RowsFlow.Controls.OfType<HeroRow>())
                {
                    if (!string.IsNullOrEmpty(row.HeroId) && selectedByHero.TryGetValue(row.HeroId, out var sel))
                    {
                        row.ApplySelection(sel);
                    }
                }

                SetStatus($"Restored {dict.Count} previous selection(s)");
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[SelectHero] Restored {dict.Count} selections from {settingsPath}");
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[SelectHero] Failed to restore selections: {ex.Message}");
#endif
            }
        }

        /// <summary>
        /// Gets the path to the hero selections settings file.
        /// </summary>
        private static string GetSettingsPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "ArdysaModsTools");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "hero_selections.json");
        }

        /// <summary>
        /// Cleans up all hero download cache folders to prevent game errors.
        /// </summary>
        private static void CleanupHeroCache()
        {
            try
            {
                var tempPath = Path.GetTempPath();

                var selectHeroTemp = Path.Combine(tempPath, "ArdysaSelectHero");
                if (Directory.Exists(selectHeroTemp))
                {
                    Directory.Delete(selectHeroTemp, true);
                }

                foreach (var dir in Directory.GetDirectories(tempPath, "ArdysaHero_*"))
                {
                    try
                    {
                        Directory.Delete(dir, true);
                    }
                    catch { /* Ignore individual failures */ }
                }

                var setsCache = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "sets");
                if (Directory.Exists(setsCache))
                {
                    Directory.Delete(setsCache, true);
                }

#if DEBUG
                System.Diagnostics.Debug.WriteLine("[SelectHero] Hero cache cleanup completed");
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[SelectHero] Cache cleanup failed: {ex.Message}");
#endif
            }
        }

        #endregion

        private void modernSearchBox_Load_1(object sender, EventArgs e)
        {

        }
    }
}
