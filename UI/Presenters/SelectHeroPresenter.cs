using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Models;
using ArdysaModsTools.UI.Interfaces;

namespace ArdysaModsTools.UI.Presenters
{
    /// <summary>
    /// Handles all business logic for SelectHero form.
    /// Manages hero loading, selection, filtering, and generation.
    /// </summary>
    public class SelectHeroPresenter : IDisposable
    {
        #region Private Fields

        private readonly ISelectHeroView _view;
        private readonly HeroService _heroService;
        private readonly ConfigService _configService;
        
        private List<HeroModel> _heroList = new();
        private Dictionary<string, HeroModel> _heroLookup = new(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _favorites = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, string> _selections = new(StringComparer.OrdinalIgnoreCase);
        
        private string _currentCategory = "all";
        private string _searchFilter = "";
        private bool _isGenerating;
        private bool _disposed;

        private static readonly string SettingsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ArdysaModsTools");
        private static readonly string SelectionsFile = Path.Combine(SettingsFolder, "hero_selections.json");

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new SelectHeroPresenter.
        /// </summary>
        /// <param name="view">The view to control</param>
        public SelectHeroPresenter(ISelectHeroView view)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            
            var baseFolder = AppDomain.CurrentDomain.BaseDirectory;
            _heroService = new HeroService(baseFolder);
            _configService = new ConfigService();
            
            // Load favorites
            var loaded = FavoritesStore.Load();
            _favorites = loaded ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Loads heroes and initializes the view.
        /// </summary>
        public async Task InitializeAsync()
        {
            _view.SetStatus("Loading heroes...");

            try
            {
                // Load heroes from service
                var heroSummaries = await _heroService.LoadHeroesAsync();
                
                if (heroSummaries != null && heroSummaries.Count > 0)
                {
                    _heroList = MapHeroSummariesToModels(heroSummaries);
                    _heroLookup = _heroList.ToDictionary(h => h.Id, h => h, StringComparer.OrdinalIgnoreCase);
                    
                    _view.AppendDebug($"Loaded {_heroList.Count} heroes.");
                }
                else
                {
                    _view.AppendDebug("No heroes loaded from service.");
                }

                // Populate view
                _view.PopulateHeroes(_heroList);
                
                // Restore previous selections
                await RestoreSelectionsAsync();
                
                _view.SetStatus($"Loaded {_heroList.Count} heroes. Ready.");
            }
            catch (Exception ex)
            {
                _view.AppendDebug($"Failed to load heroes: {ex.Message}");
                _view.SetStatus("Failed to load heroes.");
            }
        }

        private List<HeroModel> MapHeroSummariesToModels(List<HeroSummary> summaries)
        {
            var models = new List<HeroModel>();

            foreach (var hs in summaries)
            {
                var internalId = !string.IsNullOrWhiteSpace(hs.UsedByHeroes) 
                    ? hs.UsedByHeroes 
                    : hs.Name ?? "";
                var friendlyName = hs.Name ?? internalId;

                var hm = new HeroModel
                {
                    Name = internalId,
                    LocalizedName = friendlyName,
                    PrimaryAttribute = string.IsNullOrWhiteSpace(hs.PrimaryAttr) 
                        ? "universal" 
                        : hs.PrimaryAttr.ToLowerInvariant()
                };

                // Copy sets
                if (hs.Sets != null && hs.Sets.Count > 0)
                {
                    hm.Sets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kvp in hs.Sets)
                    {
                        hm.Sets[kvp.Key] = new List<string>(kvp.Value);
                    }
                }

                // Copy item IDs from Ids property (convert string to int)
                if (hs.Ids != null && hs.Ids.Length > 0)
                {
                    hm.ItemIds.Clear();
                    foreach (var idStr in hs.Ids)
                    {
                        if (int.TryParse(idStr, out int id))
                        {
                            hm.ItemIds.Add(id);
                        }
                    }
                }

                models.Add(hm);
            }

            return models;
        }

        #endregion

        #region Filtering

        /// <summary>
        /// Applies the category filter.
        /// </summary>
        /// <param name="category">Category to filter by</param>
        public void SetCategoryFilter(string category)
        {
            _currentCategory = category.ToLowerInvariant();
            ApplyFilters();
        }

        /// <summary>
        /// Applies the search filter.
        /// </summary>
        /// <param name="searchText">Text to search for</param>
        public void SetSearchFilter(string searchText)
        {
            _searchFilter = searchText?.Trim() ?? "";
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            // Filter heroes based on category and search
            // The view handles the actual filtering of UI elements
            _view.ApplyCategoryFilter(_currentCategory);
            _view.ApplySearchFilter(_searchFilter);
        }

        /// <summary>
        /// Gets whether a hero matches current filters.
        /// </summary>
        public bool HeroMatchesFilters(string heroId)
        {
            if (!_heroLookup.TryGetValue(heroId, out var hero))
                return false;

            // Check category
            bool matchesCategory = _currentCategory switch
            {
                "all" => true,
                "favorite" => _favorites.Contains(heroId),
                _ => hero.PrimaryAttribute?.ToLowerInvariant() == _currentCategory
            };

            // Check search
            bool matchesSearch = string.IsNullOrWhiteSpace(_searchFilter) ||
                (hero.LocalizedName?.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (hero.Name?.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ?? false);

            return matchesCategory && matchesSearch;
        }

        #endregion

        #region Selections

        /// <summary>
        /// Sets the selection for a hero.
        /// </summary>
        public void SetSelection(string heroId, string setName)
        {
            if (string.IsNullOrEmpty(heroId))
                return;

            if (string.IsNullOrEmpty(setName))
            {
                _selections.Remove(heroId);
            }
            else
            {
                _selections[heroId] = setName;
            }
        }

        /// <summary>
        /// Gets the current selection for a hero.
        /// </summary>
        public string? GetSelection(string heroId)
        {
            return _selections.TryGetValue(heroId, out var set) ? set : null;
        }

        /// <summary>
        /// Gets all current selections.
        /// </summary>
        public IEnumerable<(string heroId, string setName)> GetSelections()
        {
            return _selections.Select(kvp => (kvp.Key, kvp.Value));
        }

        /// <summary>
        /// Clears all selections.
        /// </summary>
        public void ClearSelections()
        {
            _selections.Clear();
            _view.ClearSelections();
        }

        #endregion

        #region Favorites

        /// <summary>
        /// Toggles the favorite status of a hero.
        /// </summary>
        public bool ToggleFavorite(string heroId)
        {
            if (string.IsNullOrEmpty(heroId))
                return false;

            bool isFavorite;
            if (_favorites.Contains(heroId))
            {
                _favorites.Remove(heroId);
                isFavorite = false;
            }
            else
            {
                _favorites.Add(heroId);
                isFavorite = true;
            }

            return isFavorite;
        }

        /// <summary>
        /// Saves favorites to disk.
        /// </summary>
        public void SaveFavorites()
        {
            FavoritesStore.Save(_favorites);
        }

        /// <summary>
        /// Gets whether a hero is a favorite.
        /// </summary>
        public bool IsFavorite(string heroId)
        {
            return _favorites.Contains(heroId);
        }

        #endregion

        #region Generation

        /// <summary>
        /// Generates the selected hero sets.
        /// </summary>
        public async Task<bool> GenerateAsync()
        {
            if (_isGenerating)
            {
                _view.ShowMessageBox("Generation already in progress.", "Busy",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            var selections = GetSelections().ToList();
            if (!selections.Any())
            {
                _view.ShowMessageBox("No selections made. Select skins before generating.",
                    "No Selections", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            var targetPath = _configService.GetLastTargetPath();
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                _view.ShowMessageBox("No Dota 2 path set. Please set it in the main window first.",
                    "No Path", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            _isGenerating = true;
            _view.SetControlsEnabled(false);

            try
            {
                await _view.ShowProgressOverlayAsync();
                await _view.UpdateProgressAsync(0, $"Preparing to generate {selections.Count} hero set(s)...");

                // Build hero list with sets
                var heroesWithSets = new List<(HeroModel hero, string setName)>();
                var invalidSelections = new List<string>();

                foreach (var (heroId, setName) in selections)
                {
                    if (!_heroLookup.TryGetValue(heroId, out var hero))
                    {
                        invalidSelections.Add(heroId);
                        continue;
                    }

                    if (hero.Sets == null || !hero.Sets.ContainsKey(setName))
                    {
                        invalidSelections.Add($"{hero.DisplayName}: {setName}");
                        continue;
                    }

                    heroesWithSets.Add((hero, setName));
                }

                if (heroesWithSets.Count == 0)
                {
                    _view.ShowMessageBox($"No valid hero selections found.\nInvalid: {string.Join(", ", invalidSelections)}",
                        "Invalid Selections", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                // Create generator
                var generator = new HeroGenerationService();
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));

                // Create hero progress reporter (for individual hero updates)
                var progress = new Progress<(int current, int total, string heroName)>(async p =>
                {
                    string substatus = $"Hero {p.current} of {p.total}";
                    await _view.UpdateProgressAsync(-1, string.Empty, substatus); // -1 means don't update percent
                });

                // Create stage progress reporter (for overall progress: 0-100%)
                var stageProgress = new Progress<(int percent, string stage)>(async p =>
                {
                    _view.SetStatus($"{p.stage}... ({p.percent}%)");
                    await _view.UpdateProgressAsync(p.percent, p.stage, null);
                });

                var result = await generator.GenerateBatchAsync(
                    targetPath,
                    heroesWithSets,
                    async s => _view.SetStatus(s),
                    progress,
                    stageProgress,
                    null, // speedProgress
                    cts.Token);

                // Handle result
                if (result.Success)
                {
                    await SaveSelectionsAsync();
                    CleanupHeroCache();

                    var message = BuildResultMessage(result);
                    _view.ShowMessageBox(message, "Generation Complete",
                        MessageBoxButtons.OK,
                        result.FailedItems?.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

                    _view.CloseWithResult(DialogResult.OK);
                    return true;
                }
                else
                {
                    var message = BuildErrorMessage(result);
                    _view.ShowMessageBox(message, "Generation Failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                _view.ShowMessageBox("Generation was cancelled or timed out.",
                    "Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            catch (Exception ex)
            {
                FallbackLogger.Log($"SelectHero generation error: {ex}");
                _view.ShowMessageBox($"Generation failed: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            finally
            {
                CleanupHeroCache();
                _view.HideProgressOverlay();
                _view.SetControlsEnabled(true);
                _isGenerating = false;
                _view.SetStatus("Ready");
            }
        }

        private string BuildResultMessage(OperationResult result)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(result.Message);

            if (result.FailedItems?.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Failed sets:");
                foreach (var (name, reason) in result.FailedItems)
                {
                    sb.AppendLine($"  • {name}: {reason}");
                }
            }

            return sb.ToString().TrimEnd();
        }

        private string BuildErrorMessage(OperationResult result)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Generation failed: {result.Message}");

            if (result.FailedItems?.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Failed sets:");
                foreach (var (name, reason) in result.FailedItems)
                {
                    sb.AppendLine($"  • {name}: {reason}");
                }
            }

            return sb.ToString().TrimEnd();
        }

        private void CleanupHeroCache()
        {
            try
            {
                var targetPath = _configService.GetLastTargetPath();
                if (string.IsNullOrEmpty(targetPath))
                    return;

                var heroDownloadDir = Path.Combine(targetPath, "_hero_downloads");
                if (Directory.Exists(heroDownloadDir))
                {
                    Directory.Delete(heroDownloadDir, true);
                }
            }
            catch (Exception ex)
            {
                _view.AppendDebug($"Cache cleanup failed: {ex.Message}");
            }
        }

        #endregion

        #region Persistence

        /// <summary>
        /// Saves current selections to disk.
        /// </summary>
        public async Task SaveSelectionsAsync()
        {
            try
            {
                Directory.CreateDirectory(SettingsFolder);
                
                var json = JsonSerializer.Serialize(_selections, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                await File.WriteAllTextAsync(SelectionsFile, json);
            }
            catch (Exception ex)
            {
                _view.AppendDebug($"Failed to save selections: {ex.Message}");
            }
        }

        /// <summary>
        /// Restores selections from disk.
        /// </summary>
        public async Task RestoreSelectionsAsync()
        {
            try
            {
                if (!File.Exists(SelectionsFile))
                    return;

                var json = await File.ReadAllTextAsync(SelectionsFile);
                var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

                if (loaded != null)
                {
                    _selections = new Dictionary<string, string>(loaded, StringComparer.OrdinalIgnoreCase);
                    _view.RestoreSelections(_selections);
                }
            }
            catch (Exception ex)
            {
                _view.AppendDebug($"Failed to restore selections: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves selections to a preset file.
        /// </summary>
        public async Task<bool> SavePresetAsync()
        {
            var path = _view.ShowSaveFileDialog("Save Preset", "JSON Files (*.json)|*.json");
            if (string.IsNullOrEmpty(path))
                return false;

            try
            {
                var json = JsonSerializer.Serialize(_selections, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(path, json);
                return true;
            }
            catch (Exception ex)
            {
                _view.ShowMessageBox($"Failed to save preset: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// Loads selections from a preset file.
        /// </summary>
        public async Task<bool> LoadPresetAsync()
        {
            var path = _view.ShowOpenFileDialog("Load Preset", "JSON Files (*.json)|*.json");
            if (string.IsNullOrEmpty(path))
                return false;

            try
            {
                var json = await File.ReadAllTextAsync(path);
                var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

                if (loaded != null)
                {
                    _selections = new Dictionary<string, string>(loaded, StringComparer.OrdinalIgnoreCase);
                    _view.RestoreSelections(_selections);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _view.ShowMessageBox($"Failed to load preset: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return false;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the list of loaded heroes.
        /// </summary>
        public IReadOnlyList<HeroModel> Heroes => _heroList.AsReadOnly();

        /// <summary>
        /// Gets a hero by ID.
        /// </summary>
        public HeroModel? GetHero(string heroId)
        {
            return _heroLookup.TryGetValue(heroId, out var hero) ? hero : null;
        }

        /// <summary>
        /// Gets whether generation is in progress.
        /// </summary>
        public bool IsGenerating => _isGenerating;

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes of resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            SaveFavorites();
            _disposed = true;
        }

        #endregion
    }
}
