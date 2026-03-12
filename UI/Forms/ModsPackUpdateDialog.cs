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
using Microsoft.Web.WebView2.WinForms;

namespace ArdysaModsTools.UI.Forms;

/// <summary>
/// WebView2-based dialog shown on startup when a newer ModsPack is available.
/// Matches the dark theme of the application.
/// Returns DialogResult.Yes if the user chose to update, DialogResult.Cancel otherwise.
/// </summary>
public sealed class ModsPackUpdateDialog : Form
{
    private WebView2? _webView;
    private bool _initialized;

    // P/Invoke for window dragging
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
    }

    private void SetupForm()
    {
        this.Text = "ModsPack Update";
        this.FormBorderStyle = FormBorderStyle.None;
        this.BackColor = System.Drawing.Color.Black;
        this.StartPosition = FormStartPosition.CenterParent;
        this.ShowInTaskbar = false;

        // Compact dialog size, scale down on small monitors
        var screen = Screen.FromControl(this) ?? Screen.PrimaryScreen;
        var workArea = screen!.WorkingArea;
        int width = Math.Min(440, (int)(workArea.Width * 0.85));
        int height = Math.Min(380, (int)(workArea.Height * 0.85));
        this.Size = new System.Drawing.Size(width, height);

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
            string tempPath = Path.Combine(Path.GetTempPath(), "ArdysaModsTools.WebView2");
            var env = await CoreWebView2Environment.CreateAsync(null, tempPath);
            await _webView!.EnsureCoreWebView2Async(env);
            _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            _webView.CoreWebView2.WindowCloseRequested += (s, e) => SafeClose(DialogResult.Cancel);

            // Disable context menu and zoom
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

            // Load HTML
            var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Html", "modspack_update.html");
            if (File.Exists(htmlPath))
            {
                var html = await File.ReadAllTextAsync(htmlPath);
                _webView.CoreWebView2.NavigateToString(html);
            }
            else
            {
                throw new FileNotFoundException("modspack_update.html not found");
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
        }
        catch (Exception)
        {
            // Signal caller to use fallback
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

    /// <summary>
    /// Ensures form closes properly even if called from WebView context.
    /// </summary>
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

    /// <summary>
    /// Shows the ModsPack Update dialog.
    /// Returns true if the user chose to update, false otherwise.
    /// Falls back to MessageBox if WebView2 is unavailable.
    /// </summary>
    /// <param name="owner">Parent window for centering.</param>
    /// <returns>True if user wants to update; false if dismissed.</returns>
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

    /// <summary>
    /// Fallback MessageBox if WebView2 is unavailable.
    /// </summary>
    private static bool ShowFallbackDialog(IWin32Window? owner)
    {
        var result = MessageBox.Show(
            owner,
            "A newer ModsPack is available!\n\nWould you like to update now?",
            "ModsPack Update Available",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        return result == DialogResult.Yes;
    }
}
