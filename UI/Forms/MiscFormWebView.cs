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
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Services.Localization;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Cache;
using ArdysaModsTools.Core.Services.Config;
using ArdysaModsTools.UI.Helpers;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ArdysaModsTools.UI.Forms
{
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

        public MiscFormWebView(string? targetPath, Action<string> log, Action disableButtons, Action enableButtons)
        {
            _targetPath = targetPath;
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _disableButtons = disableButtons ?? throw new ArgumentNullException(nameof(disableButtons));
            _enableButtons = enableButtons ?? throw new ArgumentNullException(nameof(enableButtons));

            InitializeComponent();
            SetupForm();

            this.Shown += async (s, e) => await InitializeAsync();
            Helpers.DpiLayout.AttachClamp(this);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(1320, 780);
            this.Name = "MiscFormWebView";
            this.Text = "Miscellaneous - AMT 2.0";
            this.ResumeLayout(false);
        }

        private void SetupForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.MinimumSize = new System.Drawing.Size(1040, 640);
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
                var env = await WebView2EnvironmentHelper.CreateEnvironmentAsync();
                await _webView!.EnsureCoreWebView2Async(env);
                Helpers.DpiLayout.PinTo100(this, _webView!);
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                WebViewAssetInterceptor.Attach(_webView.CoreWebView2, env, EnvironmentConfig.ContentBase);

                var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Html", "misc_form.html");
                if (File.Exists(htmlPath))
                {
                    var html = await File.ReadAllTextAsync(htmlPath);
                    _webView.CoreWebView2.NavigateToString(Helpers.WebViewTheming.Apply(html));
                }
                else
                {
                    throw new FileNotFoundException("misc_form.html not found");
                }

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

                if (Loc.Service != null)
                    await _webView.CoreWebView2.ExecuteScriptAsync(WebViewLocalizer.BuildBootstrapScript(Loc.Service));

                var version = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion ?? "unknown";
                await ExecuteScriptAsync($"setVersion('{version}')");

                await MiscCategoryService.PreloadConfigAsync();
                await PreloadMiscThumbnailsAsync();
                
                await LoadOptionsAsync();
                
                await RestoreSelectionsAsync();
            }
            catch (Exception)
            {
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
            try
            {

                var options = MiscCategoryService.GetAllOptions();
                var optionsData = options.Select(o => new
                {
                    id = o.Id,
                    name = o.DisplayName,
                    category = o.Category,
                    thumbnailPattern = o.ThumbnailUrlPattern ?? "",
                    excludesWith = o.ExcludesWith.Count > 0 ? o.ExcludesWith : null,
                    isSpecialVpk = o.IsSpecialVpk ? true : (bool?)null,
                    choices = o.Choices
                        .Select(c => new
                        {
                            id = c,
                            name = c,
                            thumbnailId = o.ChoiceThumbnailIds.TryGetValue(c, out var tid) ? tid : null,
                            styles = o.ChoiceStyles.ContainsKey(c) ? o.ChoiceStyles[c].Select(s => new {
                                id = s,
                                name = s,
                                thumbnailId = o.ChoiceThumbnailIds.TryGetValue(s, out var stid) ? stid : null
                            }).ToList() : null
                        }).ToList()
                }).ToList();

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

        private static readonly TimeSpan RefreshCooldown = TimeSpan.FromMinutes(10);

        private static readonly TimeSpan MissingThumbnailTtl = TimeSpan.FromDays(7);

        private async Task PreloadMiscThumbnailsAsync()
        {
            var cacheService = AssetCacheService.Instance;
            var options = MiscCategoryService.GetAllOptions();
            var allUrls = new List<string>();

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
                return;
            }

            try
            {
                var cached = allUrls.Where(u => cacheService.IsCached(u)).ToList();
                var notCached = allUrls
                    .Where(u => !cacheService.IsCached(u) && !cacheService.IsKnownMissing(u, MissingThumbnailTtl))
                    .ToList();
                
                System.Diagnostics.Debug.WriteLine(
                    $"[MiscForm] Thumbnails: {cached.Count} cached, {notCached.Count} missing");

                if (notCached.Count == 0 && !cacheService.ShouldRefreshAssets(RefreshCooldown))
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[MiscForm] All cached, cooldown active — skipping overlay");
                    return;
                }

                if (notCached.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[MiscForm] All cached, cooldown expired — silent freshness check");
                    
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await RunSilentRefreshAsync(cacheService, cached);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"[MiscForm] Background refresh error: {ex.Message}");
                        }
                    });
                    return;
                }

                async Task RunGuardedAsync()
                {
                    try { await RunDownloadWithOverlayAsync(cacheService, cached, notCached); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MiscForm] Thumbnail preload error: {ex.Message}"); }
                }
                var work = RunGuardedAsync();
                if (await Task.WhenAny(work, Task.Delay(ThumbnailOverlayCap)) != work)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[MiscForm] Thumbnail preload exceeded cap — releasing UI, downloads continue in background");
                }
            }
            finally
            {
                await Task.Delay(150);
            }
        }

        private async Task RunSilentRefreshAsync(AssetCacheService cacheService, List<string> cachedUrls)
        {
            try
            {
                var result = await cacheService.RefreshStaleAssetsAsync(cachedUrls);
                cacheService.MarkRefreshed();

                System.Diagnostics.Debug.WriteLine(
                    $"[MiscForm] Silent refresh complete: " +
                    $"{result.refreshed} refreshed, {result.skipped} skipped, {result.failed} failed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[MiscForm] Silent refresh error: {ex.Message}");
            }
        }

        private static readonly TimeSpan ThumbnailOverlayCap = TimeSpan.FromSeconds(30);

        private async Task RunDownloadWithOverlayAsync(
            AssetCacheService cacheService,
            List<string> cached,
            List<string> notCached)
        {
            int downloaded = 0;

            if (notCached.Count > 0)
            {
                await UpdateCachingStatusAsync("Downloading thumbnails...", 0, notCached.Count);

                var downloadProgress = new Progress<(int current, int total, string url)>(async p =>
                {
                    try { await UpdateCachingProgressAsync(p.current, p.total); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"DownloadProgress callback failed: {ex.Message}"); }
                });

                var downloadResult = await cacheService.PreloadAssetsWithProgressAsync(notCached, downloadProgress);
                downloaded = downloadResult.downloaded;
            }

            if (cached.Count > 0 && cacheService.ShouldRefreshAssets(RefreshCooldown))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await RunSilentRefreshAsync(cacheService, cached);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[MiscForm] Background refresh error: {ex.Message}");
                    }
                });
            }
            else
            {
                cacheService.MarkRefreshed();
            }

            if (downloaded > 0)
            {
                await UpdateCachingStatusAsync("Processing thumbnails...", downloaded, downloaded);
                await Task.Delay(200);
            }
        }

        private async Task UpdateCachingStatusAsync(string status, int current, int total)
        {
            var escapedStatus = EscapeJs(status);
            await ExecuteScriptAsync($@"
                document.getElementById('cachingStatus').textContent = '{escapedStatus}';
                updateCachingProgress({current}, {total});
            ");
        }

        private async Task UpdateCachingProgressAsync(int current, int total)
        {
            await ExecuteScriptAsync($"updateCachingProgress({current}, {total})");
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

                    case "clearSelections":
                        await HandleClearSelectionsAsync();
                        break;

                    case "copyConsole":
                        HandleCopyConsole(message);
                        break;

                    case "close":
                        if (!_isGenerating) BeginInvoke(new Action(() => this.Close()));
                        break;

                    case "startDrag":
                        BeginInvoke(new Action(() =>
                        {
                            ReleaseCapture();
                            SendMessage(this.Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                        }));
                        break;

                    case "alertDismissed":
                        _alertDismissed?.TrySetResult(true);
                        break;

                    case "cancelGeneration":
                        _generationCts?.Cancel();
                        break;
                }
            }
            catch
            {
            }
        }

        private async void HandleSelectionChanged(JsonElement message)
        {
            try
            {
                var optionId = message.GetProperty("optionId").GetString();
                var choiceId = message.GetProperty("choiceId").GetString();
                if (optionId != null && choiceId != null)
                {
                    _selections[optionId] = choiceId;

                    var options = MiscCategoryService.GetAllOptions();
                    var currentOption = options.FirstOrDefault(o => o.Id == optionId);
                    if (currentOption?.ExcludesWith.Count > 0)
                    {
                        bool isDefault = currentOption.Choices.Count > 0 && choiceId == currentOption.Choices[0];
                        if (!isDefault)
                        {
                            foreach (var excludedId in currentOption.ExcludesWith)
                            {
                                var excludedOption = options.FirstOrDefault(o => o.Id == excludedId);
                                if (excludedOption == null) continue;

                                string defaultChoice = excludedOption.Choices.Count > 0 ? excludedOption.Choices[0] : "default";
                                _selections[excludedId] = defaultChoice;
                                await ExecuteScriptAsync($"resetOption('{EscapeJs(excludedId)}', '{EscapeJs(defaultChoice)}')");
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private async Task HandleClearSelectionsAsync()
        {
            _selections.Clear();
            await SaveSelectionsAsync();
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

            var hasCustomSelections = _selections.Values.Any(v => v != "default" && !string.IsNullOrEmpty(v));
            if (!hasCustomSelections)
            {
                await ExecuteScriptAsync("showAlert('No Changes', 'No custom options selected. All options are set to default.', 'info')");
                return;
            }

            _modeSelected = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            await ExecuteScriptAsync("showModeModal()");
            var modeResult = await Task.WhenAny(_modeSelected.Task, Task.Delay(60000));

            string? mode = null;
            if (modeResult == _modeSelected.Task)
            {
                mode = await _modeSelected.Task;
            }
            else
            {
                _modeSelected = null;
                await ExecuteScriptAsync("closeModeModal()");
            }

            if (string.IsNullOrEmpty(mode)) return;

            var selectedMode = mode == "clean" 
                ? MiscGenerationMode.GenerateOnly 
                : MiscGenerationMode.AddToCurrent;

            _isGenerating = true;
            _disableButtons();
            await ExecuteScriptAsync("setGenerating(true)");
            
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

                await SaveSelectionsAsync();

                Action<string> logAction = (msg) =>
                {
                    if (string.IsNullOrWhiteSpace(msg)) return;
                    if (msg.StartsWith("Done!")) return;
                    
                    if (this.IsHandleCreated && !this.IsDisposed)
                    {
                        this.BeginInvoke(new Action(async () =>
                        {
                            await AppendConsoleAsync(msg);
                            
                            int pct = progressPercent;
                            if (msg.Contains("Validating")) pct = Math.Max(pct, 10);
                            else if (msg.Contains("conflict") || msg.Contains("Conflict")) pct = Math.Max(pct, 15);
                            else if (msg.Contains("Preparing") || msg.Contains("Extracting")) pct = Math.Max(pct, 30);
                            else if (msg.Contains("Applying") || msg.Contains("Fetching")) pct = Math.Max(pct, 55);
                            else if (msg.Contains("Building") || msg.Contains("Recompiling")) pct = Math.Max(pct, 75);
                            else if (msg.Contains("Installing")) pct = Math.Max(pct, 90);
                            else if (msg.Contains("Finalizing")) pct = Math.Max(pct, 95);
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
                _generationCts?.Dispose();
                _generationCts = new CancellationTokenSource();

                var result = await controller.GenerateModsAsync(
                    _targetPath!,
                    _selections,
                    selectedMode,
                    logAction,
                    _generationCts.Token,
                    null);

                result = await ResolveConflictsAndRetryAsync(
                    controller, result, selectedMode, logAction, _generationCts.Token);

                var elapsed = DateTime.Now - startTime;
                await AppendConsoleAsync("");
                await AppendConsoleAsync($"Completed in {elapsed.TotalSeconds:F1}s");
                await ExecuteScriptAsync("updateProgress(100, 'Complete!')");

                if (result.Success)
                {
                    if (result.Warnings?.Count > 0)
                    {
                        await AppendConsoleAsync("");
                        foreach (var w in result.Warnings)
                            await AppendConsoleAsync($"⚠ {w}");
                    }

                    await ExecuteScriptAsync("flashConsole('success')");
                    
                    GenerationResult = new ModGenerationResult
                    {
                        Success = true,
                        Type = GenerationType.Miscellaneous,
                        MiscMode = selectedMode,
                        OptionsCount = _selections.Count,
                        Duration = elapsed
                    };
                    
                    string successMessage;
                    if (result.Warnings?.Count > 0)
                    {
                        successMessage = $"Generation completed with {result.Warnings.Count} warning(s).\\n\\nSome options could not be downloaded and were skipped. Check the console log for details.";
                    }
                    else if (selectedMode == MiscGenerationMode.GenerateOnly)
                    {
                        successMessage = "Miscellaneous mods generated successfully!\\n\\nNote: Previous mods have been replaced.";
                    }
                    else
                    {
                        successMessage = "All mods have been successfully applied!";
                    }

                    _alertDismissed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    await ExecuteScriptAsync($"showAlert('Generation Complete', '{successMessage}', 'success')");
                    
                    await Task.WhenAny(_alertDismissed.Task, Task.Delay(60000));
                    
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else if (!result.WasCanceled)
                {
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

                    var detail = (result.Message ?? "Unknown error")
                        + (string.IsNullOrEmpty(result.ErrorCode) ? "" : $"\n\nError code: {result.ErrorCode}")
                        + "\n\nClick Show Log to review the step that failed. Full details were saved to ardysa_fallback.log.";
                    var errorMsg = detail.Replace("'", "\\'").Replace("\n", "\\n");
                    await ExecuteScriptAsync($"showAlert('Generation Failed', '{errorMsg}', 'error')");
                }
            }
            catch (Exception ex)
            {
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

                _generationCts?.Dispose();
                _generationCts = null;
            }
        }

        private async Task<OperationResult> ResolveConflictsAndRetryAsync(
            MiscController controller,
            OperationResult result,
            MiscGenerationMode selectedMode,
            Action<string> logAction,
            CancellationToken ct)
        {
            while (result.RequiresConflictResolution && result.Conflicts != null)
            {
                await AppendConsoleAsync("");
                await AppendConsoleAsync("Resolving conflicts...");

                var userChoices = new Dictionary<string, ConflictResolutionOption>();
                foreach (var conflict in result.Conflicts)
                {
                    using var dialog = new ConflictResolutionDialog(conflict);
                    if (dialog.ShowDialog(this) == DialogResult.OK && dialog.SelectedOption != null)
                    {
                        userChoices[conflict.Id] = dialog.SelectedOption;
                        logAction($"User chose: {dialog.SelectedOption.Strategy} for {conflict.Description}");
                    }
                    else
                    {
                        logAction("Conflict resolution cancelled by user.");
                        return OperationResult.Canceled("Conflict resolution cancelled by user.");
                    }
                }

                var (applyResult, adjustedSelections) = await controller.ApplyConflictResolutionsAsync(
                    result.Conflicts, userChoices, _selections, _targetPath!, logAction, ct);

                if (!applyResult.Success)
                {
                    logAction($"Failed to apply resolutions: {applyResult.Message}");
                    return applyResult;
                }

                _selections = adjustedSelections;
                await AppendConsoleAsync("Retrying generation with resolved conflicts...");
                result = await controller.GenerateModsAsync(
                    _targetPath!, _selections, selectedMode, logAction, ct, null);
            }

            return result;
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveSelectionsAsync failed: {ex.Message}");
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RestoreSelectionsAsync failed: {ex.Message}");
            }
        }

        private static string GetSettingsPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "ArdysaModsTools");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "misc_selections.json");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _generationCts?.Dispose();
                _generationCts = null;
            }
            base.Dispose(disposing);
        }
    }
}
