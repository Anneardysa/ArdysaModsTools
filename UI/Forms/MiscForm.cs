using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Controllers;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.UI.Controls;
using ArdysaModsTools.UI.Controls.Widgets;
using ArdysaModsTools.UI;

namespace ArdysaModsTools
{
    public partial class MiscForm : Form
    {
        private readonly string? _targetPath;
        private readonly Action<string> _log;
        private readonly Action _disableButtons;
        private readonly Action _enableButtons;

        private readonly MiscUtilityService _utils;
        private readonly Logger _miscLogger;

        // UI state
        private readonly Dictionary<string, MiscRow> _rows = new();

        public MiscForm(string? targetPath, Action<string> log, Action disableButtons, Action enableButtons)
        {
            _targetPath = targetPath;
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _disableButtons = disableButtons ?? throw new ArgumentNullException(nameof(disableButtons));
            _enableButtons = enableButtons ?? throw new ArgumentNullException(nameof(enableButtons));

            InitializeComponent();

            _utils = new MiscUtilityService();
            _miscLogger = new Logger(ConsoleLogBox);

            // Wire button events
            LoadPreset.Click += (s, e) => LoadPreset_Click();
            SavePreset.Click += (s, e) => SavePreset_Click();
            generateButton.Click += async (s, e) => await GenerateButton_Click();

            // Apply font fallback
            UI.FontHelper.ApplyToForm(this);
        }

        // FORM EVENTS

        private async void MiscForm_Load(object sender, EventArgs e)
        {
            try
            {
                // Preload remote configuration from GitHub
                _miscLogger.Log("Loading configuration...");
                await MiscCategoryService.PreloadConfigAsync();
                _miscLogger.Log("Configuration loaded.");
                
                PopulateOptions();
                await RestoreSelectionsAsync();
            }
            catch (Exception ex)
            {
                _miscLogger.Log($"Error during form load: {ex.Message}");
            }
        }

        // POPULATE UI

        private void PopulateOptions()
        {
            RowsFlow.SuspendLayout();
            RowsFlow.Controls.Clear();
            _rows.Clear();

            var options = MiscCategoryService.GetAllOptions();
            var categories = MiscCategoryService.GetCategories();

            string? lastCategory = null;

            foreach (var option in options)
            {
                // Add section header when category changes
                if (option.Category != lastCategory)
                {
                    var header = new MiscSectionHeader(option.Category)
                    {
                        Width = RowsFlow.ClientSize.Width - 40
                    };
                    RowsFlow.Controls.Add(header);
                    lastCategory = option.Category;
                }

                // Create MiscRow for this option
                var row = new MiscRow
                {
                    Width = RowsFlow.ClientSize.Width - 40
                };
                row.Bind(option);
                row.SelectionChanged += OnSelectionChanged;
                row.ExpandedChanged += OnRowExpandedChanged;

                RowsFlow.Controls.Add(row);
                _rows[option.Id] = row;
            }

            RowsFlow.ResumeLayout();

            // Initial layout
            UpdateRowWidths();

            // Handle resize for row widths
            ScrollContainer.Resize += (s, e) => UpdateRowWidths();
        }

        private void UpdateRowWidths()
        {
            int containerWidth = RowsFlow.ClientSize.Width;
            int maxRowWidth = 800; // Max width for content
            int rowWidth = Math.Min(containerWidth - 40, maxRowWidth);
            int leftMargin = Math.Max(0, (containerWidth - rowWidth) / 2);

            foreach (Control ctrl in RowsFlow.Controls)
            {
                if (ctrl is MiscRow row)
                {
                    row.Width = rowWidth;
                    row.Margin = new Padding(leftMargin, 4, 0, 4);
                    if (row.IsExpanded) row.RecalculateLayout();
                }
                else if (ctrl is MiscSectionHeader header)
                {
                    header.Width = rowWidth;
                    header.Margin = new Padding(leftMargin, 16, 0, 8);
                }
            }
        }

        private void OnSelectionChanged(string optionId, string choice)
        {
            // Optional: Log selection changes
            System.Diagnostics.Debug.WriteLine($"Selection changed: {optionId} = {choice}");
        }

        private void OnRowExpandedChanged(MiscRow expandedRow)
        {
            // Accordion behavior: collapse all other rows when one expands
            if (expandedRow.IsExpanded)
            {
                foreach (var row in _rows.Values)
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
        }

        private void ScrollToRow(MiscRow row)
        {
            var rowTop = row.Top;
            var rowBottom = row.Top + row.Height;
            var containerHeight = ScrollContainer.ClientSize.Height;
            var currentScroll = ScrollContainer.VerticalScroll.Value;

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

        // SELECTION HELPERS

        public Dictionary<string, string> GetSelections()
        {
            var selections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in _rows)
            {
                selections[kvp.Key] = kvp.Value.SelectedChoice;
            }
            return selections;
        }

        private void CopyConsoleBtn_Click(object? sender, EventArgs e)
        {
            try
            {
                var text = ConsoleLogBox.GetAllText();
                if (!string.IsNullOrEmpty(text))
                {
                    Clipboard.SetText(text);
                    _miscLogger.Log("Console text copied to clipboard.");
                }
            }
            catch (Exception ex)
            {
                _miscLogger.Log($"Failed to copy: {ex.Message}");
            }
        }

        public void ApplySelections(Dictionary<string, string> selections)
        {
            foreach (var kvp in selections)
            {
                if (_rows.TryGetValue(kvp.Key, out var row))
                {
                    row.ApplySelection(kvp.Value);
                }
            }
        }

        // PRESET LOAD/SAVE

        private void LoadPreset_Click()
        {
            using var open = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Load Preset",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (open.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var json = File.ReadAllText(open.FileName);
                var preset = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (preset != null)
                {
                    ApplySelections(preset);
                    _miscLogger.Log($"Preset loaded from {open.FileName}");
                }
            }
            catch (Exception ex)
            {
                _miscLogger.Log($"Error loading preset: {ex.Message}");
            }
        }

        private void SavePreset_Click()
        {
            using var save = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Save Preset",
                DefaultExt = "json",
                FileName = "MiscPreset.json",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (save.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var selections = GetSelections();
                var json = System.Text.Json.JsonSerializer.Serialize(selections, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(save.FileName, json);
                _miscLogger.Log($"Preset saved to {save.FileName}");
            }
            catch (Exception ex)
            {
                _miscLogger.Log($"Error saving preset: {ex.Message}");
            }
        }


        private async Task GenerateButton_Click()
        {
            var selections = GetSelections();
            
            // Show styled preview dialog
            using (var previewDialog = new UI.Forms.MiscGenerationPreviewForm(selections))
            {
                if (previewDialog.ShowDialog(this) != DialogResult.OK || !previewDialog.Confirmed)
                    return;
            }
            
            // Show mode selection dialog
            MiscGenerationMode selectedMode;
            using (var modeDialog = new UI.Controls.MiscModeDialog())
            {
                if (modeDialog.ShowDialog(this) != DialogResult.OK)
                    return;
                selectedMode = modeDialog.SelectedMode;
            }
            
            _disableButtons();
            DisableFormControls();

            // Track timing for summary
            var startTime = DateTime.Now;

            try
            {
                if (string.IsNullOrEmpty(_targetPath))
                {
                    _miscLogger.Log("Error: No target path set.");
                    return;
                }

                // Clear console and show header
                ConsoleLogBox.Clear();
                
                // Show mode
                string modeText = selectedMode == MiscGenerationMode.GenerateOnly 
                    ? "Generate Only (Clean)" 
                    : "Add to Current Mods";
                _miscLogger.Log($"Mode: {modeText}");
                _miscLogger.Log($"Options: {selections.Count} selected");
                _miscLogger.Log("");

                // Save selections for next time
                await SaveSelectionsAsync();

                Action<string> cleanLog = (msg) =>
                {
                    // Skip empty messages
                    if (string.IsNullOrWhiteSpace(msg)) return;
                    
                    // Format messages based on type
                    if (msg.StartsWith("Done!"))
                    {
                        // Skip - we'll show our own completion
                    }
                    else if (msg.StartsWith("Error") || msg.StartsWith("Warning"))
                    {
                        _miscLogger.Log($"! {msg}");
                    }
                    else if (msg.Contains("applied") || msg.Contains("completed"))
                    {
                        _miscLogger.Log($"  + {msg}");
                    }
                    else
                    {
                        _miscLogger.Log(msg);
                    }
                };

                // Run operation directly without overlay
                var controller = new MiscController();
                var cts = new System.Threading.CancellationTokenSource();
                
                var operationResult = await controller.GenerateModsAsync(
                    _targetPath!, 
                    selections, 
                    selectedMode,
                    cleanLog, 
                    cts.Token,
                    null); // No speed progress

                var elapsed = DateTime.Now - startTime;

                if (operationResult.Success)
                {
                    // Show completion
                    _miscLogger.Log("");
                    _miscLogger.Log($"Completed in {elapsed.TotalSeconds:F1}s");
                    
                    // Give UI time to update log
                    await Task.Delay(500);
                    Application.DoEvents();
                    
                    // Show success dialog
                    string successMessage = selectedMode == MiscGenerationMode.GenerateOnly
                        ? "Miscellaneous mods generated successfully!\n\nNote: Previous mods have been replaced."
                        : "All mods have been successfully applied!";
                    
                    MessageBox.Show(successMessage, "Miscellaneous - Generation Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    if (operationResult.Message != "Operation cancelled by user.")
                    {
                        _miscLogger.Log($"! Error: {operationResult.Message}");
                        MessageBox.Show($"Generation failed: {operationResult.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        _miscLogger.Log("! Generation cancelled by user.");
                    }
                }
            }
            catch (Exception ex)
            {
                _miscLogger.Log($"! Unexpected Error: {ex.Message}");
                MessageBox.Show($"An unexpected error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _enableButtons();
                EnableFormControls();
            }
        }

        // UI HELPERS

        private void DisableFormControls()
        {
            generateButton.Enabled = false;
            LoadPreset.Enabled = false;
            SavePreset.Enabled = false;
            foreach (var row in _rows.Values)
            {
                row.Enabled = false;
            }
        }

        private void EnableFormControls()
        {
            generateButton.Enabled = true;
            LoadPreset.Enabled = true;
            SavePreset.Enabled = true;
            foreach (var row in _rows.Values)
            {
                row.Enabled = true;
            }
        }

        // SETTINGS PERSISTENCE

        private async Task SaveSelectionsAsync()
        {
            try
            {
                var settingsPath = GetSettingsPath();
                var selections = GetSelections();
                var json = System.Text.Json.JsonSerializer.Serialize(selections, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(settingsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save selections: {ex.Message}");
            }
        }

        private async Task RestoreSelectionsAsync()
        {
            try
            {
                var settingsPath = GetSettingsPath();
                if (File.Exists(settingsPath))
                {
                    var json = await File.ReadAllTextAsync(settingsPath);
                    var selections = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (selections != null)
                    {
                        ApplySelections(selections);
                        _miscLogger.Log("Previous selections restored.");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to restore selections: {ex.Message}");
            }
        }

        private static string GetSettingsPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "ArdysaModsTools");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "misc_selections.json");
        }
    }
}
