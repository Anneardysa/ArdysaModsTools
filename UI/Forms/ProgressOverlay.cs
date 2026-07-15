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
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Services.Localization;
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
using ArdysaModsTools.Core.Services.Update.Models;

namespace ArdysaModsTools.UI.Forms
{
    public partial class ProgressOverlay : Form
    {
        private WebView2? _webView;
        private bool _initialized;
        private int _lastProgress;
        private string _lastStatus = "";
        private string _lastSubstatus = "";
        
        private bool _cancelInProgress;
        private volatile bool _completed;
        public bool WasCancelled { get; private set; }
        public event EventHandler? CancelRequested;

        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        public bool HideDownloadSpeed { get; set; } = false;

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
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Theme.Canvas;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            
            this.Load += (s, e) =>
            {
                this.Size = ShowPreview ? new Size(1280, 720) : new Size(840, 600);
                this.CenterToScreen();
            };


            var webPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };
            
            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = Theme.Canvas
            };
            webPanel.Controls.Add(_webView);
            this.Controls.Add(webPanel);

            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    RequestCancel();
                    e.Handled = true;
                }
            };

            this.Paint += (s, e) =>
            {
                using var pen = new Pen(Theme.CanvasInk, 1);
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            };
        }
        
        private void RequestCancel()
        {
            if (_cancelInProgress || WasCancelled)
                return;
            
            _cancelInProgress = true;
            WasCancelled = true;
            
            CancelRequested?.Invoke(this, EventArgs.Empty);
            
            Complete();
        }
        
        private void SafeClose()
        {
            try
            {
                if (!this.IsDisposed)
                    Complete();
            }
            catch { }
        }

        public async Task InitializeAsync()
        {
            if (_initialized) return;

            try
            {
                var env = await WebView2EnvironmentHelper.CreateEnvironmentAsync();
                
                await _webView!.EnsureCoreWebView2Async(env);
                Helpers.DpiLayout.PinTo100(this, _webView!);
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;

                _webView.CoreWebView2.WebMessageReceived += (s, e) =>
                {
                    if (e.TryGetWebMessageAsString() == "cancel")
                    {
                        RequestCancel();
                    }
                };

                var navigationComplete = new TaskCompletionSource<bool>();
                
                void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
                {
                    _webView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
                    navigationComplete.TrySetResult(e.IsSuccess);
                }

                _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;

                string html = GetProgressHtml();
                _webView.CoreWebView2.NavigateToString(Helpers.WebViewTheming.Apply(html));

                var timeoutTask = Task.Delay(15000);
                var completedTask = await Task.WhenAny(navigationComplete.Task, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    _webView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
                    throw new TimeoutException("WebView2 navigation timeout");
                }

                await Task.Delay(100);
                _initialized = true;

                if (Loc.Service != null)
                    await _webView.CoreWebView2.ExecuteScriptAsync(WebViewLocalizer.BuildBootstrapScript(Loc.Service));

                if (HideDownloadSpeed)
                {
                    await _webView.CoreWebView2.ExecuteScriptAsync(
                        "document.querySelector('.metrics-container')?.remove()");
                }

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

        public async Task HideDownloadProgressAsync()
        {
            if (!_initialized || _webView?.CoreWebView2 == null) return;
            try
            {
                await _webView.CoreWebView2.ExecuteScriptAsync("hideDownloadProgress()");
            }
            catch { }
        }

        public async Task UpdateFileProgressAsync(int current, int total)
        {
            if (!_initialized || _webView?.CoreWebView2 == null) return;
            try
            {
                await _webView.CoreWebView2.ExecuteScriptAsync($"updateFileProgress({current}, {total})");
            }
            catch { }
        }

        private async Task LoadPreviewDataAsync()
        {
            if (!_initialized || _webView?.CoreWebView2 == null)
                return;

            try
            {
                await ExecuteScriptSafeAsync("showPreviewLoading()");

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

                string heroName = Path.GetFileNameWithoutExtension(fileName)
                    .Replace("_", " ")
                    .Replace("-", " ");

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

            heroes.Sort((a, b) => string.Compare(a["name"], b["name"], StringComparison.OrdinalIgnoreCase));
            return heroes;
        }

        private async Task ExecuteScriptSafeAsync(string script)
        {
            try
            {
                if (_webView?.CoreWebView2 != null)
                    await _webView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch { }
        }

        private static string EscapeForJs(string value)
        {
            return (value ?? "")
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "");
        }

        public void Complete()
        {
            if (_completed) return;
            
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(Complete));
                return;
            }
            
            _completed = true;
            
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
.server-log { display:none; margin-top:12px; padding:8px 12px; border:1px solid #222; min-width:160px; }
.server-log.visible { display:block; }
.server-entry { display:flex; align-items:center; gap:8px; padding:3px 0; font-size:10px; letter-spacing:1px; text-transform:uppercase; }
.server-dot { width:6px; height:6px; border-radius:50%; flex-shrink:0; }
.server-dot.standby { background:#444; opacity:0.4; }
.server-dot.active { background:#fff; }
.server-dot.success { background:#22c55e; }
.server-dot.failed { background:#ef4444; opacity:0.7; }
.server-name { color:#444; }
.server-name.active { color:#fff; font-weight:600; }
.server-name.success { color:#22c55e; }
.server-name.failed { color:#ef4444; opacity:0.7; text-decoration:line-through; }
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
<div class='server-log' id='serverLog'></div>
<div class='metrics'>
  <div>DL: <span id='dlSpeed' class='metric-val'>-- MB/S</span></div>
  <div>WRITE: <span id='writeSpeed' class='metric-val'>-- MB/S</span></div>
</div>
</div>
<script>
var circumference = 2 * Math.PI * 70;
function updateProgress(p) { document.getElementById('progressRing').style.strokeDashoffset = circumference - (p/100)*circumference; document.getElementById('percent').textContent = Math.round(p)+'%'; }
function updateStatus(t) { document.getElementById('status').textContent = t; }
function updateSubstatus(t) { var el = document.getElementById('substatus'); if (el) el.textContent = t; }
function updateDownloadSpeed(s) { document.getElementById('dlSpeed').textContent = s; }
function updateWriteSpeed(s) { document.getElementById('writeSpeed').textContent = s; }
function updateDownloadProgress(d,t) {}
function hideDownloadProgress() {}
function updateFileProgress(c,t) {}
function updateServerLog(servers) {
  var c = document.getElementById('serverLog');
  if (!servers||!servers.length){c.classList.remove('visible');return;}
  c.classList.add('visible');
  c.innerHTML = '';
  servers.forEach(function(s){
    var st = (s.Status||'standby').toLowerCase();
    var row = document.createElement('div');
    row.className = 'server-entry';
    var dot = document.createElement('span');
    dot.className = 'server-dot ' + st;
    var name = document.createElement('span');
    name.className = 'server-name ' + st;
    name.textContent = s.Name;
    row.appendChild(dot);
    row.appendChild(name);
    c.appendChild(row);
  });
}
</script>
</body>
</html>";
        }
    }
}
