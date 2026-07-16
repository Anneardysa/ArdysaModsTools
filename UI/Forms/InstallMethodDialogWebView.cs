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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Localization;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ArdysaModsTools.UI.Forms
{
    public enum InstallMethod
    {
        None,
        AutoInstall,
        ManualInstall
    }

    public sealed class InstallMethodDialogWebView : Form
    {
        private WebView2? _webView;

        private DropOverlay? _dropOverlay;

        public InstallMethod SelectedMethod { get; private set; } = InstallMethod.None;

        public string? SelectedVpkPath { get; private set; }

        private bool _manualActive;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        public InstallMethodDialogWebView()
        {
            SetupForm();
            this.Shown += async (s, e) => await InitializeAsync();
        }

        private void SetupForm()
        {
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = ChooseSize;
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Theme.Canvas;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            Name = "InstallMethodDialogWebView";
            Text = "Install ModsPack";

            Opacity = 0d;

            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = Theme.Canvas
            };
            Controls.Add(_webView);

            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                    e.Handled = true;
                }
            };

            ResumeLayout(false);
        }

        private static readonly System.Drawing.Size ChooseSize = new(440, 348);
        private static readonly System.Drawing.Size ManualSize = new(520, 560);

        private void ResizeKeepingCenter(System.Drawing.Size baseClient)
        {
            double scale = Helpers.DpiLayout.CurrentUiScale;
            var client = new System.Drawing.Size(
                (int)Math.Round(baseClient.Width * scale),
                (int)Math.Round(baseClient.Height * scale));
            if (ClientSize == client) return;
            var center = new System.Drawing.Point(Left + Width / 2, Top + Height / 2);
            ClientSize = client;
            Location = new System.Drawing.Point(center.X - Width / 2, center.Y - Height / 2);
            Helpers.DpiLayout.ClampToWorkingArea(this);
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
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                _webView.AllowExternalDrop = false;

                _dropOverlay = new DropOverlay { Visible = false, AllowDrop = true, BackColor = Theme.Canvas };
                _dropOverlay.DragEnter += OnDragEnter;
                _dropOverlay.DragLeave += OnDragLeave;
                _dropOverlay.DragDrop += OnDragDrop;
                Controls.Add(_dropOverlay);
                _dropOverlay.BringToFront();

                var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Html", "install_method.html");
                if (!File.Exists(htmlPath))
                    throw new FileNotFoundException("install_method.html not found", htmlPath);

                var html = await File.ReadAllTextAsync(htmlPath);

                var tcs = new TaskCompletionSource<bool>();
                void handler(object? s, CoreWebView2NavigationCompletedEventArgs e)
                {
                    _webView.CoreWebView2.NavigationCompleted -= handler;
                    tcs.TrySetResult(e.IsSuccess);
                }
                _webView.CoreWebView2.NavigationCompleted += handler;
                _webView.CoreWebView2.NavigateToString(Helpers.WebViewTheming.Apply(html));
                await Task.WhenAny(tcs.Task, Task.Delay(8000));

                if (Loc.Service != null)
                    await _webView.CoreWebView2.ExecuteScriptAsync(WebViewLocalizer.BuildBootstrapScript(Loc.Service));

                await PushDownloadLinksAsync(ModsDownloadService.GetBundled());
                _ = RefreshDownloadLinksFromR2Async();

                Opacity = 1d;
            }
            catch (Exception)
            {
                DialogResult = DialogResult.Abort;
                Close();
            }
        }

        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var message = JsonSerializer.Deserialize<JsonElement>(e.WebMessageAsJson);
                var type = message.GetProperty("type").GetString();

                switch (type)
                {
                    case "auto":
                        SelectedMethod = InstallMethod.AutoInstall;
                        DialogResult = DialogResult.OK;
                        Close();
                        break;

                    case "manual":
                        _manualActive = true;
                        BeginInvoke(new Action(async () =>
                        {
                            ResizeKeepingCenter(ManualSize);
                            await PositionDropOverlayAsync();
                        }));
                        break;

                    case "back":
                        _manualActive = false;
                        if (_dropOverlay != null) _dropOverlay.Visible = false;
                        BeginInvoke(new Action(() => ResizeKeepingCenter(ChooseSize)));
                        break;

                    case "browse":
                        BeginInvoke(new Action(BrowseForVpk));
                        break;

                    case "open":
                        OpenExternal(message.TryGetProperty("url", out var u) ? u.GetString() : null);
                        break;

                    case "close":
                        DialogResult = DialogResult.Cancel;
                        Close();
                        break;

                    case "startDrag":
                        ReleaseCapture();
                        SendMessage(this.Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebMessage error: {ex.Message}");
            }
        }

        private void OpenExternal(string? url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url!, UseShellExecute = true }); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"OpenExternal failed: {ex.Message}"); }
            }
        }

        private async Task PushDownloadLinksAsync(ModsDownloadConfig config)
        {
            if (_webView?.CoreWebView2 == null) return;
            var json = JsonSerializer.Serialize(new { mega = config.Mega, mediafire = config.Mediafire });
            await _webView.CoreWebView2.ExecuteScriptAsync($"window.setDownloadLinks && window.setDownloadLinks({json})");
        }

        private async Task RefreshDownloadLinksFromR2Async()
        {
            try
            {
                var config = await new ModsDownloadService().GetConfigAsync();
                if (!IsDisposed)
                    await PushDownloadLinksAsync(config);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Download-link refresh failed: {ex.Message}"); }
        }

        private void BrowseForVpk()
        {
            using var dlg = new OpenFileDialog
            {
                Title = Loc.T("mods.fileDialog.vpkTitle"),
                Filter = "VPK File (*.vpk)|*.vpk"
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
                CompleteWithVpk(dlg.FileName);
        }

        private void CompleteWithVpk(string vpkPath)
        {
            SelectedVpkPath = vpkPath;
            SelectedMethod = InstallMethod.ManualInstall;
            DialogResult = DialogResult.OK;
            Close();
        }

        private async Task PositionDropOverlayAsync()
        {
            if (_dropOverlay == null || _webView?.CoreWebView2 == null) return;

            try
            {
                await Task.Delay(80);

                if (IsDisposed || _dropOverlay.IsDisposed || _webView?.CoreWebView2 == null) return;

                var raw = await _webView.CoreWebView2.ExecuteScriptAsync(
                    "(function(){var e=document.getElementById('dropzone');if(!e)return null;" +
                    "var r=e.getBoundingClientRect();return JSON.stringify(" +
                    "{x:Math.round(r.left),y:Math.round(r.top),w:Math.round(r.width),h:Math.round(r.height)});})()");
                if (IsDisposed || _dropOverlay.IsDisposed) return;
                if (string.IsNullOrEmpty(raw) || raw == "null") return;

                var inner = JsonSerializer.Deserialize<string>(raw);
                if (inner == null) return;
                var r = JsonSerializer.Deserialize<JsonElement>(inner);

                double scale = Helpers.DpiLayout.CurrentUiScale;
                _dropOverlay.Bounds = new System.Drawing.Rectangle(
                    (int)Math.Round(r.GetProperty("x").GetInt32() * scale),
                    (int)Math.Round(r.GetProperty("y").GetInt32() * scale),
                    (int)Math.Round(r.GetProperty("w").GetInt32() * scale),
                    (int)Math.Round(r.GetProperty("h").GetInt32() * scale));
                _dropOverlay.Visible = _manualActive;
                _dropOverlay.BringToFront();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PositionDropOverlay failed: {ex.Message}");
            }
        }

        private static string? VpkFromDrag(DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) != true)
                return null;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            if (files.Length != 1)
                return null;
            return files[0].EndsWith(".vpk", StringComparison.OrdinalIgnoreCase) ? files[0] : null;
        }

        private void OnDragEnter(object? sender, DragEventArgs e)
        {
            bool accept = _manualActive && VpkFromDrag(e) != null;
            e.Effect = accept ? DragDropEffects.Copy : DragDropEffects.None;
            if (accept)
                _ = _webView?.CoreWebView2?.ExecuteScriptAsync("setDrag(true)");
        }

        private void OnDragLeave(object? sender, EventArgs e)
        {
            _ = _webView?.CoreWebView2?.ExecuteScriptAsync("setDrag(false)");
        }

        private void OnDragDrop(object? sender, DragEventArgs e)
        {
            _ = _webView?.CoreWebView2?.ExecuteScriptAsync("setDrag(false)");
            if (!_manualActive)
                return;
            var path = VpkFromDrag(e);
            if (path != null)
                CompleteWithVpk(path);
        }

        private sealed class DropOverlay : Panel
        {
            [DllImport("user32.dll")]
            private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

            [DllImport("user32.dll", SetLastError = true)]
            private static extern bool ChangeWindowMessageFilterEx(IntPtr hwnd, uint msg, uint action, IntPtr changeInfo);

            private const int WS_EX_LAYERED = 0x80000;
            private const uint LWA_ALPHA = 0x2;

            private const uint MSGFLT_ALLOW = 1;
            private const uint WM_DROPFILES = 0x0233;
            private const uint WM_COPYDATA = 0x004A;
            private const uint WM_COPYGLOBALDATA = 0x0049;

            protected override CreateParams CreateParams
            {
                get
                {
                    var cp = base.CreateParams;
                    cp.ExStyle |= WS_EX_LAYERED;
                    return cp;
                }
            }

            protected override void OnHandleCreated(EventArgs e)
            {
                base.OnHandleCreated(e);
                SetLayeredWindowAttributes(Handle, 0, 1, LWA_ALPHA);

                foreach (var msg in new[] { WM_DROPFILES, WM_COPYDATA, WM_COPYGLOBALDATA })
                {
                    try { ChangeWindowMessageFilterEx(Handle, msg, MSGFLT_ALLOW, IntPtr.Zero); }
                    catch {  }
                }
            }
        }
    }
}
