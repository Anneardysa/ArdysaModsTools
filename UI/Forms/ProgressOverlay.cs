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
using System.IO;
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
        private Controls.RoundedButton? _cancelButton;
        public bool WasCancelled { get; private set; }
        public event EventHandler? CancelRequested;

        // For rounded corners
        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        /// <summary>
        /// When true, hides the download speed display.
        /// </summary>
        public bool HideDownloadSpeed { get; set; } = false;

        public ProgressOverlay()
        {
            InitializeComponent();
            SetupForm();
        }

        private void SetupForm()
        {
            // Form settings - match MainForm size
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(840, 600);
            this.BackColor = Color.Black;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            
            // Force center on screen after size is set
            this.Load += (s, e) => this.CenterToScreen();

            // No rounded corners - matches website's sharp aesthetic
            // this.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 20, 20));

            // WebView2 control with padding
            var webPanel = new Panel
            {
                Location = new Point(20, 20),
                Size = new Size(Width - 40, Height - 90),
                BackColor = Color.Transparent
            };
            
            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = Color.Black
            };
            webPanel.Controls.Add(_webView);
            this.Controls.Add(webPanel);

            // Cancel button - black bg, white text, inverts on hover
            _cancelButton = new Controls.RoundedButton
            {
                Text = "[ CANCEL ]",
                Size = new Size(160, 44),
                Location = new Point((Width - 160) / 2, Height - 60),
                BackColor = Color.Black,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("JetBrains Mono", 10F, FontStyle.Bold),
                BorderRadius = 0,
                HoverBackColor = Color.White,
                HoverForeColor = Color.Black
            };
            _cancelButton.Click += (s, e) => RequestCancel();
            this.Controls.Add(_cancelButton);

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
            
            // Visual feedback: update button to show cancelling
            if (_cancelButton != null)
            {
                _cancelButton.Text = "CANCELLING...";
                _cancelButton.Enabled = false;
                _cancelButton.BackColor = Color.FromArgb(80, 80, 80);
                _cancelButton.ForeColor = Color.Gray;
            }
            
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

