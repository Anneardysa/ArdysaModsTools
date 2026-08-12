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
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Services.Config;
using ArdysaModsTools.Core.Services.Localization;
using ArdysaModsTools.Core.Services.Update;
using ArdysaModsTools.Core.Services.Update.Models;
using ArdysaModsTools.UI.Forms;
using ArdysaModsTools.UI.Interfaces;

namespace ArdysaModsTools.Helpers
{
    public static class UIHelpers
    {
        #region Window Styling

        public static void SlideInFromRight(Form form, Rectangle targetBounds, int durationMs = 300)
        {
            var fps = 60;
            var interval = 1000 / fps;
            var steps = Math.Max(1, durationMs / interval);
            var start = new Point(Screen.FromControl(form).WorkingArea.Right, targetBounds.Y);
            var end = new Point(targetBounds.X, targetBounds.Y);
            var deltaX = (end.X - start.X) / (double)steps;
            var timer = new System.Windows.Forms.Timer { Interval = interval };
            int current = 0;
            form.StartPosition = FormStartPosition.Manual;
            form.Bounds = new Rectangle(start, targetBounds.Size);
            form.Show();
            timer.Tick += (s, e) =>
            {
                current++;
                var newX = (int)Math.Round(start.X + deltaX * current);
                form.Left = newX;
                if (current >= steps)
                {
                    timer.Stop();
                    form.Left = end.X;
                }
            };
            timer.Start();
        }

        #endregion

        #region URL Handling

        public static bool OpenUrl(string? url, Action<string>? errorCallback = null)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                errorCallback?.Invoke("URL is empty or null.");
                return false;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                errorCallback?.Invoke($"Invalid URL format: '{url}'");
                return false;
            }

            string scheme = uri.Scheme.ToLowerInvariant();
            if (scheme != Uri.UriSchemeHttp &&
                scheme != Uri.UriSchemeHttps &&
                scheme != "steam")
            {
                errorCallback?.Invoke($"Blocked opening URL with untrusted scheme '{uri.Scheme}': '{url}'");
                return false;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = uri.AbsoluteUri,
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex)
            {
                errorCallback?.Invoke($"Failed to open URL '{url}': {ex.Message}");
                return false;
            }
        }

        public static void OpenUrlWithErrorDialog(string? url, string urlName, Action<string>? errorCallback = null)
        {
            if (!OpenUrl(url, errorCallback))
            {
                ShowError(Loc.T("error.openLink", new { name = urlName }));
            }
        }


        private const string DiscordInviteUrl = "https://discord.gg/5xKg4fyumv";

        public static async Task ShowFeatureUnavailableAsync(
            IMainFormView view,
            string featureDisplayName,
            string message,
            Action<string>? log = null)
        {
            bool joinDiscord = await view.ShowShellConfirmAsync(
                eyebrow: Loc.T("feature.unavailable.title"),
                heading: featureDisplayName,
                body: message,
                confirmText: Loc.T("feature.unavailable.joinDiscord"),
                cancelText: Loc.T("common.close"),
                accent: "warn");

            if (joinDiscord)
                OpenUrlWithErrorDialog(DiscordInviteUrl, "Discord", log);
        }

        public static async Task ShowFeatureBlockedAsync(
            IMainFormView view,
            FeatureCheckResult result,
            UpdaterService? updater = null,
            Action<string>? log = null)
        {
            if (!result.IsOutdated)
            {
                await ShowFeatureUnavailableAsync(
                    view, result.FeatureDisplayName, result.BlockedMessage ?? "", log);
                return;
            }

            await ShowUpdateRequiredAsync(view, result, updater, log);
        }

        private static string DownloadPageUrl => $"{EnvironmentConfig.WebsiteBase}/#download";

        private static async Task ShowUpdateRequiredAsync(
            IMainFormView view,
            FeatureCheckResult result,
            UpdaterService? updater,
            Action<string>? log)
        {
            bool canUpdateInPlace = updater?.InstallationType == InstallationType.Installer;
            string current = AppVersion.Current.ToString();

            log?.Invoke(
                $"Feature '{result.FeatureDisplayName}' requires {result.RequiredVersion}; " +
                $"running {current}. Prompting to update.");

            bool proceed = await view.ShowShellConfirmAsync(
                eyebrow: Loc.T("update.required.title"),
                heading: result.FeatureDisplayName,
                body: result.BlockedMessage ?? "",
                note: Loc.T("update.required.note",
                    new { required = result.RequiredVersion, current }),
                confirmText: Loc.T(canUpdateInPlace
                    ? "update.required.action.installer"
                    : "update.required.action.portable"),
                cancelText: Loc.T("common.close"),
                accent: "warn");

            if (!proceed)
                return;

            try
            {
                var info = updater != null ? await updater.GetUpdateInfoAsync() : null;

                if (info?.IsUpdateAvailable == true && updater != null)
                {
                    var owner = view as IWin32Window ?? Form.ActiveForm;

                    UpdateAvailableDialogWebView.Show(
                        owner, info, updater.InstallationType, updater.Delta);
                    return;
                }
            }
            catch (Exception ex)
            {
                log?.Invoke($"Could not open the update dialog: {ex.Message}");
            }

            OpenUrlWithErrorDialog(DownloadPageUrl, "Download", log);
        }

        #endregion

        #region Thread-Safe UI Updates

        public static void SafeInvoke(this Control control, Action action)
        {
            if (control == null || control.IsDisposed)
                return;

            if (control.InvokeRequired)
            {
                try
                {
                    control.Invoke(action);
                }
                catch (ObjectDisposedException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }
            else
            {
                action();
            }
        }

        public static void SafeBeginInvoke(this Control control, Action action)
        {
            if (control == null || control.IsDisposed)
                return;

            if (control.InvokeRequired)
            {
                try
                {
                    control.BeginInvoke(action);
                }
                catch (ObjectDisposedException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }
            else
            {
                action();
            }
        }

        #endregion

        #region Standard Dialogs

        public static void ShowError(string message, string? title = null)
        {
            MessageBox.Show(message, title ?? Loc.T("common.error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static void ShowWarning(string message, string? title = null)
        {
            MessageBox.Show(message, title ?? Loc.T("common.warning"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public static void ShowInfo(string message, string? title = null)
        {
            MessageBox.Show(message, title ?? Loc.T("common.information"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static bool ShowConfirm(string message, string? title = null)
        {
            return MessageBox.Show(message, title ?? Loc.T("common.confirm"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        public static bool ShowConfirmWarning(string message, string? title = null)
        {
            return MessageBox.Show(message, title ?? Loc.T("common.warning"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
        }

        #endregion
    }
}

