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
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Services.Localization;
using Microsoft.Web.WebView2.WinForms;

namespace ArdysaModsTools.UI.Forms;

public sealed class ModsPackUpdateDialog : Form
{
    private WebView2? _webView;
    private bool _initialized;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCAPTION = 0x2;

    private ModsPackUpdateDialog()
    {
        SetupForm();
        this.Shown += async (s, e) => await InitializeAsync();
        Helpers.DpiLayout.AttachClamp(this);
    }

    private void SetupForm()
    {
        this.Text = "ModsPack Update";
        this.FormBorderStyle = FormBorderStyle.None;
        this.BackColor = Theme.Canvas;
        this.StartPosition = FormStartPosition.CenterParent;
        this.ShowInTaskbar = false;

        var screen = Screen.FromControl(this) ?? Screen.PrimaryScreen;
        var workArea = screen!.WorkingArea;
        int width = Math.Min(440, (int)(workArea.Width * 0.85));
        int height = Math.Min(380, (int)(workArea.Height * 0.85));
        this.Size = new System.Drawing.Size(width, height);

        _webView = new WebView2
        {
            Dock = DockStyle.Fill,
            DefaultBackgroundColor = Theme.Canvas
        };
        this.Controls.Add(_webView);

        this.KeyPreview = true;
        this.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
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
            _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            _webView.CoreWebView2.WindowCloseRequested += (s, e) => SafeClose(DialogResult.Cancel);

            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

            var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Html", "modspack_update.html");
            if (File.Exists(htmlPath))
            {
                var html = await File.ReadAllTextAsync(htmlPath);
                _webView.CoreWebView2.NavigateToString(Helpers.WebViewTheming.Apply(html));
            }
            else
            {
                throw new FileNotFoundException("modspack_update.html not found");
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
                case "updateNow":
                    SafeClose(DialogResult.Yes);
                    break;

                case "notNow":
                    SafeClose(DialogResult.Cancel);
                    break;

                case "startDrag":
                    ReleaseCapture();
                    SendMessage(this.Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ModsPackUpdateDialog WebMessage error: {ex.Message}");
        }
    }

    private void SafeClose(DialogResult result)
    {
        if (this.IsDisposed || !this.IsHandleCreated) return;

        this.BeginInvoke(new Action(() =>
        {
            DialogResult = result;
            this.Close();
        }));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _webView?.Dispose();
            _webView = null;
        }
        base.Dispose(disposing);
    }

    public static bool ShowUpdateDialog(IWin32Window? owner)
    {
        try
        {
            using var dialog = new ModsPackUpdateDialog();
            var result = dialog.ShowDialog(owner);

            if (result == DialogResult.Abort)
            {
                return ShowFallbackDialog(owner);
            }

            return result == DialogResult.Yes;
        }
        catch
        {
            return ShowFallbackDialog(owner);
        }
    }

    private static bool ShowFallbackDialog(IWin32Window? owner)
    {
        var result = MessageBox.Show(
            owner,
            Loc.T("modspack.update.body"),
            Loc.T("modspack.update.title"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        return result == DialogResult.Yes;
    }
}
