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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Controllers;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Cache;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ArdysaModsTools.UI.Forms
{
    /// <summary>
    /// Modern WebView2-based miscellaneous options form with Tailwind CSS styling.
    /// </summary>
    public partial class MiscFormWebView : Form
    {
        private WebView2? _webView;
        private bool _initialized;
        private bool _isGenerating;
        private CancellationTokenSource? _generationCts;
        private TaskCompletionSource<bool>? _alertDismissed;
        private TaskCompletionSource<string?>? _modeSelected;

        private readonly string? _targetPath;
        private readonly Action<string> _log;
        private readonly Action _disableButtons;
        private readonly Action _enableButtons;

        private Dictionary<string, string> _selections = new(StringComparer.OrdinalIgnoreCase);

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

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public MiscFormWebView(string? targetPath, Action<string> log, Action disableButtons, Action enableButtons)
        {
            _targetPath = targetPath;
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _disableButtons = disableButtons ?? throw new ArgumentNullException(nameof(disableButtons));
            _enableButtons = enableButtons ?? throw new ArgumentNullException(nameof(enableButtons));

            InitializeComponent();
            SetupForm();

            this.Shown += async (s, e) => await InitializeAsync();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1000, 750);
            this.Name = "MiscFormWebView";
            this.Text = "Miscellaneous - AMT 2.0";
            this.ResumeLayout(false);
        }

        private void SetupForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.MinimumSize = new System.Drawing.Size(800, 600);
            this.BackColor = System.Drawing.Color.Black;
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
                    var screen = Screen.FromControl(this);
                    this.Left = screen.WorkingArea.Left + (screen.WorkingArea.Width - this.Width) / 2;
                    this.Top = screen.WorkingArea.Top + (screen.WorkingArea.Height - this.Height) / 2;
                }
            };

            // WebView2 control
            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = System.Drawing.Color.Black
            };
            this.Controls.Add(_webView);

            // ESC to close
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape && !_isGenerating)
                {
                    Close();
                    e.Handled = true;
                }
            };
        }

        private async Task InitializeAsync()
        {
            try
            {
                // Use temp folder for WebView2 user data (avoids permission issues in Program Files)
                string tempPath = Path.Combine(Path.GetTempPath(), "ArdysaModsTools.WebView2");
                var env = await CoreWebView2Environment.CreateAsync(null, tempPath);
                await _webView!.EnsureCoreWebView2Async(env);
                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                // Load HTML
                var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Html", "misc_form.html");
                if (File.Exists(htmlPath))
                {
                    var html = await File.ReadAllTextAsync(htmlPath);
                    _webView.CoreWebView2.NavigateToString(html);
                }
                else
                {
                    throw new FileNotFoundException("misc_form.html not found");
                }

                // Wait for navigation
                var tcs = new TaskCompletionSource<bool>();
                void handler(object? s, CoreWebView2NavigationCompletedEventArgs e)
                {
                    _webView.CoreWebView2.NavigationCompleted -= handler;
                    tcs.SetResult(e.IsSuccess);
                }
                _webView.CoreWebView2.NavigationCompleted += handler;
                await tcs.Task;

                await Task.Delay(100);

                if (_initialized) return;
                _initialized = true;

                // Set version
                var version = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion ?? "unknown";
                await ExecuteScriptAsync($"setVersion('{version}')");

                // Load options
                await LoadOptionsAsync();
                
                // Preload thumbnails with HTML overlay (same UI pattern as Skin Selector)
                await PreloadMiscThumbnailsAsync();
                
                await RestoreSelectionsAsync();
            }
            catch (Exception)
            {
                // Signal caller to use classic fallback - don't show error here
                // (caller in MainForm.View.cs handles the fallback transition)
                this.DialogResult = DialogResult.Abort;
                this.Close();
            }
        }

        private async Task ExecuteScriptAsync(string script)
        {
            if (_webView?.CoreWebView2 != null)
            {
                await _webView.CoreWebView2.ExecuteScriptAsync(script);
            }
        }

        private async Task LoadOptionsAsync()
        {
            await AppendConsoleAsync("Loading configuration...");
            
            try
            {
                await MiscCategoryService.PreloadConfigAsync();
                await AppendConsoleAsync("Configuration loaded.");

                var options = MiscCategoryService.GetAllOptions();
                var optionsData = options.Select(o => new
                {
                    id = o.Id,
                    name = o.DisplayName,
                    category = o.Category,
                    thumbnailPattern = o.ThumbnailUrlPattern ?? "",
                    choices = o.Choices.Select(c => new
                    {
                        id = c,
                        name = c
                    }).ToList()
                }).ToList();

                // Set default thumbnail path (app icon)
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Icons", "AppIcon.ico");
                var iconUri = new Uri(iconPath).AbsoluteUri;
                await ExecuteScriptAsync($"setDefaultThumb('{iconUri}')");

                var json = JsonSerializer.Serialize(optionsData, _jsonOptions);
                await ExecuteScriptAsync($"loadOptions({json})");
            }
            catch (Exception ex)
            {
                await AppendConsoleAsync($"Error loading options: {ex.Message}");
            }
        }

        /// <summary>
        /// Preload all misc option thumbnails using AssetCacheService with HTML overlay progress.
        /// Two-phase: 1) Check freshness of cached items, 2) Download missing items.
        /// </summary>
        private async Task PreloadMiscThumbnailsAsync()
        {
            var cacheService = AssetCacheService.Instance;
            var options = MiscCategoryService.GetAllOptions();
            var allUrls = new List<string>();

            // Collect all thumbnail URLs from options
            foreach (var option in options)
            {
                if (string.IsNullOrEmpty(option.ThumbnailUrlPattern)) continue;
                
                foreach (var choice in option.Choices)
                {
                    var thumbUrl = option.GetThumbnailUrl(choice);
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
                    
                    System.Diagnostics.Debug.WriteLine($"[MiscForm] Refreshed {refreshed} stale assets");
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
                    
                    System.Diagnostics.Debug.WriteLine($"[MiscForm] Downloaded {downloaded} new assets");
                }

                System.Diagnostics.Debug.WriteLine($"[MiscForm] Total: {refreshed} refreshed, {downloaded} downloaded, {cached.Count - refreshed} fresh from cache");
            }
            finally
            {
                // Hide overlay with small delay for smooth transition
                await Task.Delay(300);
                await ExecuteScriptAsync("hideCachingOverlay()");
            }
        }

        private async Task AppendConsoleAsync(string message)
        {
            var escaped = EscapeJs(message);
            await ExecuteScriptAsync($"appendConsole('{escaped}')");
        }

        private static string EscapeJs(string text)
        {
            return text.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "");
        }

        private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var message = JsonSerializer.Deserialize<JsonElement>(e.WebMessageAsJson);
                var type = message.GetProperty("type").GetString();

                switch (type)
                {
                    case "generate":
                        await HandleGenerateAsync(message);
                        break;

                    case "modeSelected":
                        HandleModeSelected(message);
                        break;

                    case "loadPreset":
                        await HandleLoadPresetAsync();
                        break;

                    case "savePreset":
                        await HandleSavePresetAsync(message);
                        break;

                    case "selectionChanged":
                        HandleSelectionChanged(message);
                        break;

                    case "copyConsole":
                        HandleCopyConsole(message);
                        break;

                    case "close":
                        if (!_isGenerating) this.Close();
                        break;

                    case "startDrag":
                        ReleaseCapture();
                        SendMessage(this.Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                        break;

                    case "alertDismissed":
                        _alertDismissed?.TrySetResult(true);
                        break;

                    case "cancelGeneration":
                        _generationCts?.Cancel();
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebMessage error: {ex.Message}");
            }
        }

        private void HandleSelectionChanged(JsonElement message)
        {
            try
            {
                var optionId = message.GetProperty("optionId").GetString();
                var choiceId = message.GetProperty("choiceId").GetString();
                if (optionId != null && choiceId != null)
                {
                    _selections[optionId] = choiceId;
                }
            }
            catch { }
        }

        private void HandleModeSelected(JsonElement message)
        {
            try
            {
                var mode = message.GetProperty("mode").GetString();
                _modeSelected?.TrySetResult(mode);
            }
            catch { }
        }

        private void HandleCopyConsole(JsonElement message)
        {
            try
            {
                var text = message.GetProperty("text").GetString();
                if (!string.IsNullOrEmpty(text))
                {
                    Clipboard.SetText(text);
                }
            }
            catch { }
        }

        private async Task HandleGenerateAsync(JsonElement message)
        {
            if (_isGenerating || string.IsNullOrEmpty(_targetPath)) return;

            // Get selections from message
            try
            {
                if (message.TryGetProperty("selections", out var selectionsElement))
                {
                    foreach (var prop in selectionsElement.EnumerateObject())
                    {
                        _selections[prop.Name] = prop.Value.GetString() ?? "";
                    }
                }
            }
            catch { }

            // Check if any non-default selections
            var hasCustomSelections = _selections.Values.Any(v => v != "default" && !string.IsNullOrEmpty(v));
            if (!hasCustomSelections)
            {
                await ExecuteScriptAsync("showAlert('No Changes', 'No custom options selected. All options are set to default.', 'info')");
                return;
            }

            // Show mode selection modal
            await ExecuteScriptAsync("showModeModal()");
            _modeSelected = new TaskCompletionSource<string?>();
            var modeResult = await Task.WhenAny(_modeSelected.Task, Task.Delay(60000));
            
            string? mode = null;
            if (modeResult == _modeSelected.Task)
            {
                mode = await _modeSelected.Task;
            }
            
            if (string.IsNullOrEmpty(mode)) return;

            var selectedMode = mode == "clean" 
                ? MiscGenerationMode.GenerateOnly 
                : MiscGenerationMode.AddToCurrent;

            _isGenerating = true;
            _disableButtons();
            await ExecuteScriptAsync("setGenerating(true)");
            
            // Show progress overlay
            string modeText = selectedMode == MiscGenerationMode.GenerateOnly
                ? "Clean Generate"
                : "Add to Current";
            await ExecuteScriptAsync($"showProgress('Generating - {modeText}')");
            await ExecuteScriptAsync("clearConsole()");
            await ExecuteScriptAsync("setStatus('Generating...')");

            var startTime = DateTime.Now;
            int progressPercent = 0;

            try
            {
                await AppendConsoleAsync($"Mode: {modeText}");
                await AppendConsoleAsync($"Options: {_selections.Count} selected");
                await AppendConsoleAsync("");

                // Save selections
                await SaveSelectionsAsync();

                Action<string> logAction = (msg) =>
                {
                    if (string.IsNullOrWhiteSpace(msg)) return;
                    if (msg.StartsWith("Done!")) return;
                    
                    // Use BeginInvoke to properly execute on UI thread
                    if (this.IsHandleCreated && !this.IsDisposed)
                    {
                        this.BeginInvoke(new Action(async () =>
                        {
                            await AppendConsoleAsync(msg);
                            
                            // Update progress based on message content
                            int pct = progressPercent;
                            if (msg.Contains("Extracting")) pct = 20;
                            else if (msg.Contains("Downloading")) pct = 40;
                            else if (msg.Contains("Applying")) pct = 60;
                            else if (msg.Contains("Recompiling")) pct = 80;
                            else if (msg.Contains("Complete") || msg.Contains("Success")) pct = 100;
                            
                            if (pct != progressPercent)
                            {
                                progressPercent = pct;
                                await ExecuteScriptAsync($"updateProgress({progressPercent}, '{EscapeJs(msg)}')");
                            }
                        }));
                    }
                };

                var controller = new MiscController();
                _generationCts = new CancellationTokenSource();

                var result = await controller.GenerateModsAsync(
                    _targetPath!,
                    _selections,
                    selectedMode,
                    logAction,
                    _generationCts.Token,
                    null);

                var elapsed = DateTime.Now - startTime;
                await AppendConsoleAsync("");
                await AppendConsoleAsync($"Completed in {elapsed.TotalSeconds:F1}s");
                await ExecuteScriptAsync("updateProgress(100, 'Complete!')");

                if (result.Success)
                {
                    await ExecuteScriptAsync("flashConsole('success')");
                    
                    // Store generation result for MainForm logging
                    GenerationResult = new ModGenerationResult
                    {
                        Success = true,
                        Type = GenerationType.Miscellaneous,
                        MiscMode = selectedMode,
                        OptionsCount = _selections.Count,
                        Duration = elapsed
                    };
                    
                    string successMessage = selectedMode == MiscGenerationMode.GenerateOnly
                        ? "Miscellaneous mods generated successfully!\\n\\nNote: Previous mods have been replaced."
                        : "All mods have been successfully applied!";

                    _alertDismissed = new TaskCompletionSource<bool>();
                    await ExecuteScriptAsync($"showAlert('Generation Complete', '{successMessage}', 'success')");
                    
                    // Wait for alert dismiss
                    await Task.WhenAny(_alertDismissed.Task, Task.Delay(60000));
                    
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else if (result.Message != "Operation cancelled by user.")
                {
                    // Store failed result
                    GenerationResult = new ModGenerationResult
                    {
                        Success = false,
                        Type = GenerationType.Miscellaneous,
                        MiscMode = selectedMode,
                        OptionsCount = _selections.Count,
                        Duration = elapsed,
                        ErrorMessage = result.Message
                    };
                    
                    await ExecuteScriptAsync("flashConsole('error')");
                    var errorMsg = (result.Message ?? "Unknown error").Replace("'", "\\'").Replace("\n", "\\n");
                    await ExecuteScriptAsync($"showAlert('Generation Failed', '{errorMsg}', 'error')");
                }
            }
            catch (Exception ex)
            {
                // Store exception result
                GenerationResult = new ModGenerationResult
                {
                    Success = false,
                    Type = GenerationType.Miscellaneous,
                    MiscMode = selectedMode,
                    OptionsCount = _selections.Count,
                    Duration = DateTime.Now - startTime,
                    ErrorMessage = ex.Message
                };
                
                await ExecuteScriptAsync("flashConsole('error')");
                var errorMsg = ex.Message.Replace("'", "\\'");
                await ExecuteScriptAsync($"showAlert('Error', '{errorMsg}', 'error')");
            }
            finally
            {
                _isGenerating = false;
                _enableButtons();
                await ExecuteScriptAsync("setGenerating(false)");
                await ExecuteScriptAsync("hideProgress()");
                await ExecuteScriptAsync("setStatus('Ready')");
            }
        }

        private async Task HandleLoadPresetAsync()
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
                var json = await File.ReadAllTextAsync(open.FileName);
                var preset = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (preset != null)
                {
                    _selections = new Dictionary<string, string>(preset, StringComparer.OrdinalIgnoreCase);
                    var selectionsJson = JsonSerializer.Serialize(_selections, _jsonOptions);
                    await ExecuteScriptAsync($"loadSelections({selectionsJson})");
                    await AppendConsoleAsync($"Preset loaded from {Path.GetFileName(open.FileName)}");
                }
            }
            catch (Exception ex)
            {
                await AppendConsoleAsync($"Error loading preset: {ex.Message}");
            }
        }

        private async Task HandleSavePresetAsync(JsonElement message)
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
                // Get selections from message
                if (message.TryGetProperty("selections", out var selectionsElement))
                {
                    foreach (var prop in selectionsElement.EnumerateObject())
                    {
                        _selections[prop.Name] = prop.Value.GetString() ?? "";
                    }
                }

                var json = JsonSerializer.Serialize(_selections, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(save.FileName, json);
                await AppendConsoleAsync($"Preset saved to {Path.GetFileName(save.FileName)}");
            }
            catch (Exception ex)
            {
                await AppendConsoleAsync($"Error saving preset: {ex.Message}");
            }
        }

        private async Task SaveSelectionsAsync()
        {
            try
            {
                var settingsPath = GetSettingsPath();
                var json = JsonSerializer.Serialize(_selections, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(settingsPath, json);
            }
            catch { }
        }

        private async Task RestoreSelectionsAsync()
        {
            try
            {
                var settingsPath = GetSettingsPath();
                if (File.Exists(settingsPath))
                {
                    var json = await File.ReadAllTextAsync(settingsPath);
                    var selections = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (selections != null)
                    {
                        _selections = new Dictionary<string, string>(selections, StringComparer.OrdinalIgnoreCase);
                        var selectionsJson = JsonSerializer.Serialize(_selections, _jsonOptions);
                        await ExecuteScriptAsync($"loadSelections({selectionsJson})");
                        await AppendConsoleAsync("Previous selections restored.");
                    }
                }
            }
            catch { }
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
