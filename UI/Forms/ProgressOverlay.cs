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
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace ArdysaModsTools.UI.Forms
{
    /// <summary>
    /// Modal progress overlay using WebView2 for animated HTML/CSS/JS UI.
    /// </summary>
    public partial class ProgressOverlay : Form
    {
        private WebView2? _webView;
        private bool _initialized;
        private int _lastProgress;
        private string _lastStatus = "";
        private string _lastSubstatus = "";
        
        // Cancel support - robust state management
        private bool _cancelInProgress;
        public bool WasCancelled { get; private set; }
        public event EventHandler? CancelRequested;

        // For rounded corners
        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        /// <summary>
        /// When true, hides the download speed display.
        /// </summary>
        public bool HideDownloadSpeed { get; set; } = false;

        /// <summary>
        /// When true, shows the ModsPack preview panel alongside the progress.
        /// Form expands to 1280×720 to accommodate side-by-side layout.
        /// </summary>
        public bool ShowPreview { get; set; } = false;

        private static readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        private const string GitHubApiUrl =
            "https://api.github.com/repos/Anneardysa/ArdysaMods/contents/assets/updates";

        public ProgressOverlay()
        {
            InitializeComponent();
            SetupForm();
        }

        private void SetupForm()
        {
            // Form settings
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.Black;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            
            // Apply size based on preview mode, center on screen
            this.Load += (s, e) =>
            {
                this.Size = ShowPreview ? new Size(1280, 720) : new Size(840, 600);
                this.CenterToScreen();
            };

            // No rounded corners - matches website's sharp aesthetic
            // this.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 20, 20));

            // WebView2 panel — uses Dock.Fill so it adapts to Load-time resize
            var webPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };
            
            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = Color.Black
            };
            webPanel.Controls.Add(_webView);
            this.Controls.Add(webPanel);

            // ESC to cancel
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    RequestCancel();
                    e.Handled = true;
                }
            };

            // Draw white border
            this.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.White, 1);
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            };
        }
        
        /// <summary>
        /// Request cancellation with robust state management.
        /// Prevents double-clicks and provides visual feedback.
        /// </summary>
        private void RequestCancel()
        {
            // Guard: prevent double-click or multiple cancellations
            if (_cancelInProgress || WasCancelled)
                return;
            
            _cancelInProgress = true;
            
            // Visual feedback: update HTML cancel button
            _ = ExecuteScriptSafeAsync(
                "var b=document.getElementById('cancelBtn');" +
                "if(b){b.textContent='CANCELLING...';b.disabled=true;b.style.opacity='0.4';b.style.cursor='default'}");
            
            // Update status to show cancellation in progress
            try { _ = UpdateStatusAsync("Cancelling..."); } catch { }
            
            // Set cancelled flag and fire event
            WasCancelled = true;
            CancelRequested?.Invoke(this, EventArgs.Empty);
            
            // Close overlay after a brief delay for visual feedback
            Task.Delay(500).ContinueWith(_ =>
            {
                if (this.InvokeRequired)
                    this.BeginInvoke(new Action(() => SafeClose()));
                else
                    SafeClose();
            });
        }
        
        /// <summary>
        /// Safely close the overlay, handling disposed state.
        /// </summary>
        private void SafeClose()
        {
            try
            {
                if (!this.IsDisposed)
                    Complete();
            }
            catch { }
        }

        /// <summary>
        /// Initialize WebView2 with user data in temp folder.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_initialized) return;

            try
            {
                // Use temp folder for WebView2 user data
                string tempPath = Path.Combine(Path.GetTempPath(), "ArdysaModsTools.WebView2");
                var env = await CoreWebView2Environment.CreateAsync(null, tempPath);
                
                await _webView!.EnsureCoreWebView2Async(env);
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;

                // Wire up WebMessage bridge for HTML cancel button
                _webView.CoreWebView2.WebMessageReceived += (s, e) =>
                {
                    if (e.TryGetWebMessageAsString() == "cancel")
                    {
                        RequestCancel();
                    }
                };

                // Wait for navigation completion
                var navigationComplete = new TaskCompletionSource<bool>();
                
                void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
                {
                    _webView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
                    navigationComplete.TrySetResult(e.IsSuccess);
                }

                _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;

                // Load HTML
                string html = GetProgressHtml();
                _webView.CoreWebView2.NavigateToString(html);

                // Wait with timeout
                var timeoutTask = Task.Delay(5000);
                var completedTask = await Task.WhenAny(navigationComplete.Task, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    _webView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
                    throw new TimeoutException("WebView2 navigation timeout");
                }

                await Task.Delay(100);
                _initialized = true;
                
                // Hide download speed if requested
                if (HideDownloadSpeed)
                {
                    await _webView.CoreWebView2.ExecuteScriptAsync(
                        "document.querySelector('.metrics-container')?.remove()");
                }

                // Start preview loading in background (fire-and-forget)
                if (ShowPreview)
                {
                    _ = LoadPreviewDataAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebView2 init failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Update progress percentage (0-100).
        /// </summary>
        public async Task UpdateProgressAsync(int percent)
        {
            if (!_initialized || _webView?.CoreWebView2 == null) return;
            if (percent == _lastProgress) return;
            
            _lastProgress = percent;
            try
            {
                await _webView.CoreWebView2.ExecuteScriptAsync($"updateProgress({percent})");
            }
            catch { }
        }

        /// <summary>
        /// Update status text.
        /// </summary>
        public async Task UpdateStatusAsync(string status)
        {
            if (!_initialized || _webView?.CoreWebView2 == null) return;
            if (status == _lastStatus) return;

            _lastStatus = status;
            try
            {
                string escaped = status.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n");
                await _webView.CoreWebView2.ExecuteScriptAsync($"updateStatus('{escaped}')");
            }
            catch { }
        }

        /// <summary>
        /// Update substatus text (download progress).
        /// </summary>
        public async Task UpdateSubstatusAsync(string substatus)
        {
            if (!_initialized || _webView?.CoreWebView2 == null) return;
            if (substatus == _lastSubstatus) return;

            _lastSubstatus = substatus;
            try
            {
                string escaped = substatus.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n");
                await _webView.CoreWebView2.ExecuteScriptAsync($"updateSubstatus('{escaped}')");
            }
            catch { }
        }

        /// <summary>
        /// Update download speed text.
        /// </summary>
        public async Task UpdateDownloadSpeedAsync(string speed)
        {
            if (!_initialized || _webView?.CoreWebView2 == null) return;
            try
            {
                string escaped = (speed ?? "-- MB/S").Replace("'", "\\' ");
                await _webView.CoreWebView2.ExecuteScriptAsync($"updateDownloadSpeed('{escaped}')");
            }
            catch { }
        }

        /// <summary>
        /// Update write speed text.
        /// </summary>
        public async Task UpdateWriteSpeedAsync(string speed)
        {
            if (!_initialized || _webView?.CoreWebView2 == null) return;
            try
            {
                string escaped = (speed ?? "-- MB/S").Replace("'", "\\' ");
                await _webView.CoreWebView2.ExecuteScriptAsync($"updateWriteSpeed('{escaped}')");
            }
            catch { }
        }

        /// <summary>
        /// Update download progress (downloaded/total MB).
        /// </summary>
        public async Task UpdateDownloadProgressAsync(double downloadedMB, double totalMB)
        {
            if (!_initialized || _webView?.CoreWebView2 == null) return;
            try
            {
                await _webView.CoreWebView2.ExecuteScriptAsync(
                    $"updateDownloadProgress({downloadedMB:F1}, {totalMB:F1})");
            }
            catch { }
        }

        /// <summary>
        /// Hide download progress and speed display.
        /// Call this when transitioning from download to build phase.
        /// </summary>
        public async Task HideDownloadProgressAsync()
        {
            if (!_initialized || _webView?.CoreWebView2 == null) return;
            try
            {
                await _webView.CoreWebView2.ExecuteScriptAsync("hideDownloadProgress()");
            }
            catch { }
        }

        /// <summary>
        /// Update file progress (current/total files).
        /// Used during Building, Installing, and Download Assets phases.
        /// </summary>
        public async Task UpdateFileProgressAsync(int current, int total)
        {
            if (!_initialized || _webView?.CoreWebView2 == null) return;
            try
            {
                await _webView.CoreWebView2.ExecuteScriptAsync($"updateFileProgress({current}, {total})");
            }
            catch { }
        }

        /// <summary>
        /// Fetch hero preview data from GitHub API and inject into WebView2.
        /// Runs in background — failures are logged and shown as error state,
        /// never affecting the progress operation.
        /// </summary>
        private async Task LoadPreviewDataAsync()
        {
            if (!_initialized || _webView?.CoreWebView2 == null)
                return;

            try
            {
                // Show loading state in preview panel immediately
                await ExecuteScriptSafeAsync("showPreviewLoading()");

                // Fetch hero list from GitHub API
                using var request = new HttpRequestMessage(HttpMethod.Get, GitHubApiUrl);
                request.Headers.Add("User-Agent", "ArdysaModsTools");
                request.Headers.Add("Accept", "application/vnd.github.v3+json");

                using var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                var heroes = ParseHeroListFromGitHubResponse(json);

                if (heroes.Count == 0)
                {
                    await ExecuteScriptSafeAsync("showPreviewError('No hero skins found')");
                    return;
                }

                // Serialize hero list and inject into WebView2
                string heroesJson = JsonSerializer.Serialize(heroes);
                string escapedJson = heroesJson.Replace("\\", "\\\\").Replace("'", "\\'");
                await ExecuteScriptSafeAsync($"initPreview(JSON.parse('{escapedJson}'))");
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Preview fetch failed: {ex.Message}");
                await ExecuteScriptSafeAsync(
                    $"showPreviewError('Network error: {EscapeForJs(ex.Message)}')");
            }
            catch (TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("Preview fetch timed out");
                await ExecuteScriptSafeAsync("showPreviewError('Request timed out')");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Preview error: {ex.Message}");
                await ExecuteScriptSafeAsync(
                    $"showPreviewError('Failed to load preview')");
            }
        }

        /// <summary>
        /// Parse GitHub Contents API response into a list of hero entries.
        /// Extracts .jpg files and converts filenames to display names.
        /// </summary>
        private static List<Dictionary<string, string>> ParseHeroListFromGitHubResponse(string json)
        {
            var heroes = new List<Dictionary<string, string>>();

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return heroes;

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("name", out var nameEl))
                    continue;

                string fileName = nameEl.GetString() ?? "";
                if (!fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) &&
                    !fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Convert filename to display name: "anti_mage.jpg" → "Anti Mage"
                string heroName = Path.GetFileNameWithoutExtension(fileName)
                    .Replace("_", " ")
                    .Replace("-", " ");

                // Title case
                if (heroName.Length > 0)
                {
                    heroName = System.Globalization.CultureInfo.CurrentCulture
                        .TextInfo.ToTitleCase(heroName.ToLower());
                }

                heroes.Add(new Dictionary<string, string>
                {
                    { "name", heroName },
                    { "file", fileName }
                });
            }

            // Sort alphabetically by name
            heroes.Sort((a, b) => string.Compare(a["name"], b["name"], StringComparison.OrdinalIgnoreCase));
            return heroes;
        }

        /// <summary>
        /// Execute a JS script on the WebView2, swallowing any errors silently.
        /// </summary>
        private async Task ExecuteScriptSafeAsync(string script)
        {
            try
            {
                if (_webView?.CoreWebView2 != null)
                    await _webView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch { }
        }

        /// <summary>
        /// Escape a string for safe embedding in a JS string literal.
        /// </summary>
        private static string EscapeForJs(string value)
        {
            return (value ?? "")
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "");
        }

        /// <summary>
        /// Signal completion and close.
        /// </summary>
        public void Complete()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(Complete));
                return;
            }
            
            // Dispose WebView2 to release memory
            try
            {
                if (_webView != null)
                {
                    _webView.Dispose();
                    _webView = null!;
                }
            }
            catch { }
            
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private string GetProgressHtml()
        {
            string appPath = AppDomain.CurrentDomain.BaseDirectory;
            string htmlPath = Path.Combine(appPath, "Assets", "Html", "progress.html");
            
            if (File.Exists(htmlPath))
            {
                return File.ReadAllText(htmlPath);
            }

            return GetFallbackHtml();
        }

        private static string GetFallbackHtml()
        {
            return @"<!DOCTYPE html>
<html>
<head>
<style>
* { margin: 0; padding: 0; box-sizing: border-box; }
body { background: #000; min-height: 100vh; display: flex; flex-direction: column; align-items: center; justify-content: center; font-family: 'JetBrains Mono', monospace; color: #fff; }
.container { text-align: center; padding: 32px; }
.progress-ring-container { position: relative; width: 160px; height: 160px; margin: 0 auto 32px; }
.progress-ring { transform: rotate(-90deg); }
.progress-ring-bg { fill: none; stroke: #333; stroke-width: 2; }
.progress-ring-fill { fill: none; stroke: #fff; stroke-width: 2; stroke-linecap: square; stroke-dasharray: 439.82; stroke-dashoffset: 439.82; transition: stroke-dashoffset 0.3s ease; }
.percent-text { position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%); font-size: 32px; font-weight: 700; color: #fff; letter-spacing: 2px; }
.status { font-size: 11px; color: #888; margin-bottom: 8px; text-transform: uppercase; letter-spacing: 2px; }
.status::before { content: '[ '; color: #444; }
.status::after { content: ' ]'; color: #444; }
.substatus { font-size: 11px; color: #444; margin-bottom: 20px; }
.metrics { display: flex; justify-content: center; gap: 16px; font-size: 10px; color: #444; margin-bottom: 10px; }
.metric-val { color: #888; margin-left: 4px; }
</style>
</head>
<body>
<div class='container'>
<div class='progress-ring-container'>
<svg class='progress-ring' width='160' height='160'>
<circle class='progress-ring-bg' cx='80' cy='80' r='70'/>
<circle class='progress-ring-fill' id='progressRing' cx='80' cy='80' r='70'/>
</svg>
<div class='percent-text' id='percent'>0%</div>
</div>
<div class='status' id='status'>Preparing</div>
<div class='substatus' id='substatus'></div>
<div class='metrics'>
  <div>DL: <span id='dlSpeed' class='metric-val'>-- MB/S</span></div>
  <div>WRITE: <span id='writeSpeed' class='metric-val'>-- MB/S</span></div>
</div>
</div>
<script>
const circumference = 2 * Math.PI * 70;
function updateProgress(p) { document.getElementById('progressRing').style.strokeDashoffset = circumference - (p/100)*circumference; document.getElementById('percent').textContent = Math.round(p)+'%'; }
function updateStatus(t) { document.getElementById('status').textContent = t; }
function updateSubstatus(t) { var el = document.getElementById('substatus'); if (el) el.textContent = t; }
function updateDownloadSpeed(s) { document.getElementById('dlSpeed').textContent = s; }
function updateWriteSpeed(s) { document.getElementById('writeSpeed').textContent = s; }
</script>
</body>
</html>";
        }
    }
}

