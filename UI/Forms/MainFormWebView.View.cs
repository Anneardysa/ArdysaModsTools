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
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.UI;
using ArdysaModsTools.UI.Controls;
using ArdysaModsTools.UI.Forms;
using ArdysaModsTools.UI.Interfaces;

namespace ArdysaModsTools
{
    public partial class MainFormWebView : IMainFormView
    {
        #region IMainFormView - Dialogs

        public DialogResult ShowMessageBox(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            if (InvokeRequired)
            {
                return (DialogResult)Invoke(new Func<DialogResult>(() =>
                    ShowMessageBox(message, title, buttons, icon)));
            }
            return MessageBox.Show(this, message, title, buttons, icon);
        }

        public DialogResult ShowStyledMessage(string title, string message, StyledMessageType type)
        {
            if (InvokeRequired)
            {
                return (DialogResult)Invoke(new Func<DialogResult>(() =>
                    ShowStyledMessage(title, message, type)));
            }

            bool windowOnScreen = this.Visible && this.WindowState != FormWindowState.Minimized;
            if (_webReady && windowOnScreen && _webView?.CoreWebView2 != null)
            {
                ShowShellToast(title, message, MapStyledVariant(type), 5000);
                return DialogResult.OK;
            }

            using var dialog = new StyledMessageDialog(title, message, type);
            return dialog.ShowDialog(this);
        }

        public void ShowInstallFailureCard(string title, string body)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ShowInstallFailureCard(title, body)));
                return;
            }

            bool windowOnScreen = this.Visible && this.WindowState != FormWindowState.Minimized;
            if (_webReady && windowOnScreen && _webView?.CoreWebView2 != null)
            {
                var payload = JsonSerializer.Serialize(
                    new { title, body, lines = InstallReportLines() }, _jsonOptions);
                PostExec($"showInstallFailure({payload})");
                return;
            }

            ShowStyledMessage(title, body, StyledMessageType.Error);
        }

        public void ShowInstallCompleteCard(string title, string body)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ShowInstallCompleteCard(title, body)));
                return;
            }

            bool windowOnScreen = this.Visible && this.WindowState != FormWindowState.Minimized;
            if (_webReady && windowOnScreen && _webView?.CoreWebView2 != null)
            {
                var payload = JsonSerializer.Serialize(
                    new { title, body, lines = InstallReportLines() }, _jsonOptions);
                PostExec($"showInstallComplete({payload})");
                return;
            }

            ShowStyledMessage(title, body, StyledMessageType.Success);
        }

        private static object InstallReportLines() =>
            InstallReport.Snapshot().Select(l => new { t = l.Text, c = l.Category }).ToArray();

        private static string MapStyledVariant(StyledMessageType type) => type switch
        {
            StyledMessageType.Success => "success",
            StyledMessageType.Error => "error",
            StyledMessageType.Warning => "error",
            _ => "info"
        };

        public string? ShowFolderDialog(string title)
        {
            if (InvokeRequired)
            {
                return (string?)Invoke(new Func<string?>(() => ShowFolderDialog(title)));
            }

            using var dialog = new FolderBrowserDialog { Description = title };
            return dialog.ShowDialog(this) == DialogResult.OK ? dialog.SelectedPath : null;
        }

        public string? ShowFileDialog(string title, string filter)
        {
            if (InvokeRequired)
            {
                return (string?)Invoke(new Func<string?>(() => ShowFileDialog(title, filter)));
            }

            using var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = filter
            };
            return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
        }

        public string? PendingManualVpkPath { get; private set; }

        public bool? ShowInstallMethodDialog()
        {
            if (InvokeRequired)
            {
                return (bool?)Invoke(new Func<bool?>(() => ShowInstallMethodDialog()));
            }

            PendingManualVpkPath = null;

            using var webDialog = new InstallMethodDialogWebView { StartPosition = FormStartPosition.CenterParent };
            if (webDialog.ShowDialog(this) != DialogResult.OK)
                return null;
            PendingManualVpkPath = webDialog.SelectedVpkPath;
            return webDialog.SelectedMethod != InstallMethod.ManualInstall;
        }

        public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info, int timeout = 3000)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ShowNotification(title, message, icon, timeout)));
                return;
            }

            bool windowOnScreen = this.Visible && this.WindowState != FormWindowState.Minimized;
            if (_webReady && windowOnScreen && _webView?.CoreWebView2 != null)
            {
                ShowShellToast(title, message, MapNotificationVariant(icon), timeout);
                return;
            }

            _trayService?.ShowNotification(title, message, icon, timeout, forceShow: true);
        }

        private static string MapNotificationVariant(ToolTipIcon icon) => icon switch
        {
            ToolTipIcon.Error => "error",
            ToolTipIcon.Warning => "error",
            _ => "info"
        };

        public Task<bool> ShowShellConfirmAsync(
            string eyebrow,
            string heading,
            string body,
            string note = "",
            string confirmText = "Continue",
            string cancelText = "Cancel",
            int countdownSeconds = 0,
            string accent = "")
        {
            if (InvokeRequired)
            {
                return (Task<bool>)Invoke(new Func<Task<bool>>(() =>
                    ShowShellConfirmAsync(eyebrow, heading, body, note, confirmText, cancelText, countdownSeconds, accent)));
            }

            if (!_webReady || _webView.CoreWebView2 == null)
            {
                string msg = heading;
                if (!string.IsNullOrEmpty(body)) msg += "\n\n" + body;
                if (!string.IsNullOrEmpty(note)) msg += "\n\n" + ArdysaModsTools.Core.Services.Localization.Loc.T("common.important") + "\n" + note;
                var fallback = MessageBox.Show(this, msg, eyebrow, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                return Task.FromResult(fallback == DialogResult.Yes);
            }

            int id;
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_confirmLock)
            {
                id = ++_confirmSeq;
                _confirmWaiters[id] = tcs;
            }

            var payload = JsonSerializer.Serialize(new
            {
                id,
                eyebrow,
                heading,
                body,
                note,
                confirmText,
                cancelText,
                countdown = countdownSeconds,
                accent
            }, _jsonOptions);
            Exec($"showShellConfirm({payload})");
            return tcs.Task;
        }

        public void ShowShellToast(string title, string message, string variant = "success", int timeout = 4000)
        {
            if (!_webReady)
            {
                var icon = variant == "error" ? ToolTipIcon.Error : ToolTipIcon.Info;
                ShowNotification(title, message, icon, timeout);
                return;
            }

            var payload = JsonSerializer.Serialize(new { title, message, variant, timeout }, _jsonOptions);
            PostExec($"showToast({payload})");
        }

        #endregion

        #region IMainFormView - Progress Overlay

        public async Task ShowProgressOverlayAsync()
        {
            await Task.CompletedTask;
        }

        public void HideProgressOverlay()
        {
        }

        public async Task UpdateProgressAsync(int percent, string status, string? substatus = null)
        {
            await Task.CompletedTask;
        }

        #endregion

        #region IMainFormView - Form State

        bool IMainFormView.IsVisible => Visible && !IsDisposed;

        public void InvokeOnUIThread(Action action)
        {
            if (InvokeRequired)
                BeginInvoke(action);
            else
                action();
        }

        public void CloseForm()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(CloseForm));
                return;
            }
            Close();
        }

        #endregion

        #region IMainFormView - Extended Dialogs

        public (bool ShouldProceed, bool DeletePermanently) ShowDisableOptionsDialog()
        {
            if (InvokeRequired)
            {
                return ((bool, bool))Invoke(new Func<(bool, bool)>(ShowDisableOptionsDialog));
            }

            using var webDialog = new DisableOptionsDialogWebView { StartPosition = FormStartPosition.CenterParent };
            if (webDialog.ShowDialog(this) != DialogResult.OK)
                return (false, false);
            return (true, webDialog.DeletePermanently);
        }

        public bool ShowPatchRequiredDialog(string message)
        {
            if (InvokeRequired)
            {
                return (bool)Invoke(new Func<bool>(() => ShowPatchRequiredDialog(message)));
            }

            using var dialog = new PatchRequiredDialog(message);
            dialog.StartPosition = FormStartPosition.CenterParent;
            dialog.ShowDialog(this);
            return dialog.ShouldPatch;
        }

        public bool ShowInstallRequiredDialog()
        {
            if (InvokeRequired)
            {
                return (bool)Invoke(new Func<bool>(ShowInstallRequiredDialog));
            }

            using var dialog = new InstallRequiredDialog();
            dialog.StartPosition = FormStartPosition.CenterParent;
            return dialog.ShowDialog(this) == DialogResult.OK && dialog.ShouldInstall;
        }

        public void ShowSupportDialog()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(ShowSupportDialog));
                return;
            }

            using var dialog = new SupportDialog();
            dialog.StartPosition = FormStartPosition.CenterParent;
            dialog.ShowDialog(this);
        }

        public (DialogResult Result, ModGenerationResult? GenerationResult) ShowHeroGallery()
        {
            if (InvokeRequired)
            {
                return ((DialogResult, ModGenerationResult?))Invoke(
                    new Func<(DialogResult, ModGenerationResult?)>(ShowHeroGallery));
            }

            using var heroForm = new HeroGalleryForm(_configService);
            heroForm.StartPosition = FormStartPosition.CenterParent;
            var result = heroForm.ShowDialog(this);
            return (result, heroForm.GenerationResult);
        }


        public (DialogResult Result, ModGenerationResult? GenerationResult) ShowMiscForm(string? targetPath)
        {
            if (InvokeRequired)
            {
                return ((DialogResult, ModGenerationResult?))Invoke(
                    new Func<string?, (DialogResult, ModGenerationResult?)>(ShowMiscForm), targetPath);
            }

            if (string.IsNullOrEmpty(targetPath)) return (DialogResult.Cancel, null);

            using var webViewForm = new ArdysaModsTools.UI.Forms.MiscFormWebView(
                targetPath,
                _logger.Log,
                DisableAllButtons,
                EnableAllButtons);

            webViewForm.StartPosition = FormStartPosition.CenterParent;
            var result = webViewForm.ShowDialog(this);

            if (result == DialogResult.Abort)
            {
                _logger.Log("WebView2 MiscForm failed to initialize.");
                ShowStyledMessage(
                    "Miscellaneous Unavailable",
                    "The Miscellaneous page requires the Microsoft Edge WebView2 runtime, which could not be initialized.\n\n" +
                    "Please install or repair the WebView2 runtime, then try again.",
                    StyledMessageType.Warning);
                return (DialogResult.Cancel, null);
            }

            return (result, webViewForm.GenerationResult);
        }

        public async void ShowStatusDetails(ModStatusInfo status, Func<Task> patchAction)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ShowStatusDetails(status, patchAction)));
                return;
            }

            var versionInfo = new DotaVersionInfo();

            if (_versionService != null && !string.IsNullOrEmpty(targetPath))
            {
                try
                {
                    var timeoutTask = Task.Delay(1000);
                    var versionTask = Task.Run(() => _versionService.GetVersionInfoAsync(targetPath));

                    if (await Task.WhenAny(versionTask, timeoutTask) == versionTask
                        && versionTask.IsCompletedSuccessfully)
                    {
                        versionInfo = await versionTask;
                    }
                }
                catch
                {
                }
            }

            StatusDetailsDialogWebView.Show(this, status, versionInfo, () => patchAction());
        }

        #endregion

        #region IMainFormView - Application Control

        string? IMainFormView.TargetPath
        {
            get => targetPath;
            set => targetPath = value;
        }

        public void ShowPathFoundBanner(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            Js("pathFound", $"showPathFound({J(path)})");
        }

        public void RestartApplication()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(RestartApplication));
                return;
            }

            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath))
            {
                Process.Start(exePath);
                Application.Exit();
            }
        }

        public async Task<OperationResult> RunWithProgressOverlayAsync(
            string initialStatus,
            Func<ProgressContext, Task<OperationResult>> operation,
            bool hideDownloadSpeed = false,
            bool showPreview = false)
        {
            return await ProgressOperationRunner.RunAsync(
                this,
                initialStatus,
                async (context) =>
                {
                    var progressContext = new ProgressContext
                    {
                        Progress = context.Progress,
                        Status = context.Status,
                        Speed = context.Speed,
                        Token = context.Token
                    };
                    return await operation(progressContext);
                },
                hideDownloadSpeed,
                showPreview);
        }

        public string AppPath => Path.GetDirectoryName(Process.GetCurrentProcess().MainModule!.FileName)!;

        #endregion
    }
}
