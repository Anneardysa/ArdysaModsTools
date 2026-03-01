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
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ArdysaModsTools.UI.Forms
{
    /// <summary>
    /// WebView2-based Dota 2 performance optimizer form.
    /// Reads/writes autoexec.cfg with interactive presets and per-cvar tuning.
    /// </summary>
    public sealed class Dota2PerformanceForm : Form
    {
        private WebView2? _webView;
        private bool _initialized;
        private readonly string? _gamePath;

        // P/Invoke for window dragging
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        /// <summary>
        /// Creates a new Dota 2 Performance form.
        /// </summary>
        /// <param name="gamePath">
        /// The detected Dota 2 game path (e.g. "...\dota 2 beta\game").
        /// If null, falls back to default Steam path.
        /// </param>
        public Dota2PerformanceForm(string? gamePath)
        {
            _gamePath = gamePath;

            InitializeComponent();
            SetupForm();

            this.Shown += async (s, e) => await InitializeAsync();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1600, 850);
            this.Name = "Dota2PerformanceForm";
            this.Text = "Dota 2 Performance Tweak - AMT 2.0";
            this.ResumeLayout(false);
        }

        private void SetupForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = System.Drawing.Color.Black;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;

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
                if (e.KeyCode == Keys.Escape)
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
                string tempPath = Path.Combine(Path.GetTempPath(), "ArdysaModsTools.WebView2");
                var env = await CoreWebView2Environment.CreateAsync(null, tempPath);
                await _webView!.EnsureCoreWebView2Async(env);
                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                // Disable right-click context menu
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

                // Load HTML
                var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Html", "dota2_performance.html");
                if (File.Exists(htmlPath))
                {
                    var html = await File.ReadAllTextAsync(htmlPath);
                    _webView.CoreWebView2.NavigateToString(html);
                }
                else
                {
                    throw new FileNotFoundException("dota2_performance.html not found");
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

                // Load existing autoexec.cfg settings
                await LoadCurrentSettingsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize Performance Tweak: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        /// <summary>
        /// Ensures form closes properly from WebView context.
        /// </summary>
        private void SafeClose()
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;
            this.BeginInvoke(new Action(() => this.Close()));
        }

        #region Autoexec.cfg I/O

        private string GetCfgDirectory()
        {
            // Try detected game path first
            if (!string.IsNullOrEmpty(_gamePath))
            {
                // _gamePath is typically "...\dota 2 beta\game"
                var cfgDir = Path.Combine(_gamePath, "dota", "cfg");
                if (Directory.Exists(cfgDir))
                    return cfgDir;

                // Maybe _gamePath already includes "dota" or deeper
                cfgDir = Path.Combine(_gamePath, "cfg");
                if (Directory.Exists(cfgDir))
                    return cfgDir;
            }

            // Fallback to default Steam path
            var defaultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Steam", "steamapps", "common", "dota 2 beta", "game", "dota", "cfg");

            if (Directory.Exists(defaultPath))
                return defaultPath;

            return string.Empty;
        }

        private async Task LoadCurrentSettingsAsync()
        {
            var cfgDir = GetCfgDirectory();
            if (string.IsNullOrEmpty(cfgDir))
            {
                await ExecuteScriptAsync("showToast('Dota 2 cfg folder not found. Using defaults.', 'info')");
                return;
            }

            var autoexecPath = Path.Combine(cfgDir, "autoexec.cfg");
            if (!File.Exists(autoexecPath))
            {
                await ExecuteScriptAsync("showToast('No autoexec.cfg found. Using defaults.', 'info')");
                return;
            }

            try
            {
                var lines = await File.ReadAllLinesAsync(autoexecPath);
                var settings = ParseAutoexec(lines);

                if (settings.Count > 0)
                {
                    var json = JsonSerializer.Serialize(settings);
                    var escaped = json.Replace("'", "\\'");
                    await ExecuteScriptAsync($"loadSettings('{escaped}')");
                    await ExecuteScriptAsync($"showToast('Loaded {settings.Count} settings from autoexec.cfg', 'success')");
                }
            }
            catch (Exception ex)
            {
                await ExecuteScriptAsync($"showToast('Error reading autoexec.cfg: {EscapeJs(ex.Message)}', 'error')");
            }
        }

        /// <summary>
        /// Parses cvar values from autoexec.cfg lines.
        /// Handles formats like: "cvar_name value // comment" and "cvar_name value"
        /// </summary>
        internal static Dictionary<string, string> ParseAutoexec(string[] lines)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                // Skip empty lines, pure comments, and alias lines
                if (string.IsNullOrEmpty(line) || line.StartsWith("//") || line.StartsWith("alias"))
                    continue;

                // Remove inline comment
                var commentIdx = line.IndexOf("//");
                var cleanLine = commentIdx >= 0 ? line.Substring(0, commentIdx).Trim() : line;

                if (string.IsNullOrEmpty(cleanLine))
                    continue;

                // Split "cvar_name value"
                var parts = cleanLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var cvar = parts[0];
                    var value = parts[1];

                    // Only store known cvar-like entries (starts with letter or underscore)
                    if (char.IsLetter(cvar[0]) || cvar[0] == '_')
                    {
                        result[cvar] = value;
                    }
                }
            }

            return result;
        }

        private async Task ApplySettingsAsync()
        {
            var cfgDir = GetCfgDirectory();
            if (string.IsNullOrEmpty(cfgDir))
            {
                await ExecuteScriptAsync("showToast('Dota 2 cfg folder not found!', 'error')");
                return;
            }

            try
            {
                // Get current settings from JS
                var result = await _webView!.CoreWebView2.ExecuteScriptAsync("getAllSettings()");
                // Result comes back as a JSON-encoded string (with escaped quotes)
                var jsonString = JsonSerializer.Deserialize<string>(result);
                if (string.IsNullOrEmpty(jsonString)) return;

                var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString);
                if (settings == null || settings.Count == 0) return;

                var autoexecPath = Path.Combine(cfgDir, "autoexec.cfg");

                // Backup existing
                if (File.Exists(autoexecPath))
                {
                    var backupPath = autoexecPath + ".bak";
                    File.Copy(autoexecPath, backupPath, true);
                }

                // Generate cfg content
                var content = GenerateAutoexecContent(settings);
                await File.WriteAllTextAsync(autoexecPath, content, Encoding.UTF8);

                await ExecuteScriptAsync($"showToast('autoexec.cfg saved! Backup created as .bak', 'success')");
                await ExecuteScriptAsync($"loadSettings('{JsonSerializer.Serialize(settings).Replace("'", "\\\\'")}')" );
            }
            catch (Exception ex)
            {
                await ExecuteScriptAsync($"showToast('Error: {EscapeJs(ex.Message)}', 'error')");
            }
        }

        private async Task ExportCfgAsync()
        {
            try
            {
                var result = await _webView!.CoreWebView2.ExecuteScriptAsync("getAllSettings()");
                var jsonString = JsonSerializer.Deserialize<string>(result);
                if (string.IsNullOrEmpty(jsonString)) return;

                var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString);
                if (settings == null || settings.Count == 0) return;

                using var save = new SaveFileDialog
                {
                    Filter = "CFG files (*.cfg)|*.cfg|All files (*.*)|*.*",
                    Title = "Export Autoexec Config",
                    DefaultExt = "cfg",
                    FileName = "autoexec.cfg",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                };

                if (save.ShowDialog(this) != DialogResult.OK) return;

                var content = GenerateAutoexecContent(settings);
                await File.WriteAllTextAsync(save.FileName, content, Encoding.UTF8);

                await ExecuteScriptAsync($"showToast('Exported to {EscapeJs(Path.GetFileName(save.FileName))}', 'success')");
            }
            catch (Exception ex)
            {
                await ExecuteScriptAsync($"showToast('Export failed: {EscapeJs(ex.Message)}', 'error')");
            }
        }

        /// <summary>
        /// Generates formatted autoexec.cfg content with comments.
        /// </summary>
        internal static string GenerateAutoexecContent(Dictionary<string, string> settings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// DOTA 2 AUTOEXEC.CFG — Generated by ArdysaModsTools Performance Tweak");
            sb.AppendLine($"// Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("// Save as: Steam\\steamapps\\common\\dota 2 beta\\game\\dota\\cfg\\autoexec.cfg");
            sb.AppendLine();

            // Group cvars by category for readability
            var categories = new (string Header, string[] Cvars)[]
            {
                ("DISPLAY & FPS", new[] { "fps_max", "fps_max_ui", "mat_viewportscale", "r_fullscreen_gamma" }),
                ("VISUAL TOGGLES", new[] {
                    "dota_portrait_animate", "r_deferred_additive_pass", "r_deferred_simple_light", "r_ssao",
                    "r_dota_normal_maps", "r_dota_allow_parallax_mapping", "dota_ambient_creatures", "dota_ambient_cloth",
                    "r_grass_quality", "r_dota_fxaa", "r_deferred_specular", "r_deferred_specular_bloom",
                    "dota_cheap_water", "r_deferred_height_fog", "r_dashboard_render_quality",
                    "r_dota_allow_wind_on_trees", "r_dota_bloom_compute_shader"
                }),
                ("QUALITY", new[] { "r_texture_stream_mip_bias", "cl_particle_fallback_base", "cl_globallight_shadow_mode", "r_texturefilteringquality" }),
                ("ENGINE TWEAKS", new[] {
                    "cl_particle_fallback_multiplier", "cl_particle_sim_fallback_threshold_ms",
                    "dota_allow_clientside_particles", "dota_disable_particle_lights",
                    "lb_shadow_texture_width_override", "lb_shadow_texture_height_override",
                    "r_dota_spotlight_shadows_resolution", "r_particle_max_detail_level",
                    "r_dota_color_correction", "r_dota_render_2d_skybox", "engine_no_focus_sleep"
                }),
                ("VSYNC & LATENCY", new[] { "engine_low_latency_sleep_after_client_tick", "r_experimental_lag_limiter", "r_low_latency" }),
                ("NETWORK", new[] { "rate", "cl_updaterate", "cl_interp_ratio", "cl_smooth" }),
            };

            var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (header, cvars) in categories)
            {
                sb.AppendLine($"// ── {header} ──");
                foreach (var cvar in cvars)
                {
                    if (settings.TryGetValue(cvar, out var value))
                    {
                        sb.AppendLine($"{cvar} {value}");
                        written.Add(cvar);
                    }
                }
                sb.AppendLine();
            }

            // Write any remaining cvars not covered by categories
            var remaining = settings.Where(kv => !written.Contains(kv.Key)).ToList();
            if (remaining.Count > 0)
            {
                sb.AppendLine("// ── OTHER ──");
                foreach (var kv in remaining)
                {
                    sb.AppendLine($"{kv.Key} {kv.Value}");
                }
                sb.AppendLine();
            }

            sb.AppendLine("// End of ArdysaModsTools autoexec.cfg");
            return sb.ToString();
        }

        #endregion

        #region WebView Message Handling

        private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var message = JsonSerializer.Deserialize<JsonElement>(e.WebMessageAsJson);
                var type = message.GetProperty("type").GetString();

                switch (type)
                {
                    case "applySettings":
                        await ApplySettingsAsync();
                        break;

                    case "exportCfg":
                        await ExportCfgAsync();
                        break;

                    case "close":
                        SafeClose();
                        break;

                    case "startDrag":
                        ReleaseCapture();
                        SendMessage(this.Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                        break;

                    case "copyText":
                        if (message.TryGetProperty("text", out var textProp))
                        {
                            var text = textProp.GetString();
                            if (!string.IsNullOrEmpty(text))
                            {
                                Clipboard.SetText(text);
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebMessage error: {ex.Message}");
            }
        }

        #endregion

        private static string EscapeJs(string text)
        {
            return text.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "");
        }
    }
}
