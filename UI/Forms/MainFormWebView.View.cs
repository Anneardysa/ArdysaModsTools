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
    /// <summary>
    /// Partial class containing the dialog-launching and application-control members of
    /// <see cref="IMainFormView"/>. These spawn separate forms / use the shared
    /// <c>ProgressOperationRunner</c> and are behaviorally identical to the classic
    /// <see cref="MainForm"/> implementation — only the host differs.
    /// </summary>
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
            using var dialog = new StyledMessageDialog(title, message, type);
            return dialog.ShowDialog(this);
        }

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

        public bool? ShowInstallMethodDialog()
        {
            if (InvokeRequired)
            {
                return (bool?)Invoke(new Func<bool?>(() => ShowInstallMethodDialog()));
            }

            // WebView2 dialog first (matches main_shell); classic WinForms dialog is the fallback
            // when WebView2 init fails (signaled via DialogResult.Abort).
            using (var webDialog = new InstallMethodDialogWebView { StartPosition = FormStartPosition.CenterParent })
            {
                var result = webDialog.ShowDialog(this);
                if (result != DialogResult.Abort)
                {
                    if (result != DialogResult.OK)
                        return null;
                    return webDialog.SelectedMethod != InstallMethod.ManualInstall;
                }
            }

            using var dialog = new InstallMethodDialog();
            dialog.StartPosition = FormStartPosition.CenterParent;
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return null;
            return dialog.SelectedMethod != InstallMethod.ManualInstall;
        }

        public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info, int timeout = 3000)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ShowNotification(title, message, icon, timeout)));
                return;
            }

            _trayService?.ShowNotification(title, message, icon, timeout, forceShow: true);
        }

        #endregion

        #region IMainFormView - Progress Overlay

        public async Task ShowProgressOverlayAsync()
        {
            // Progress overlay is managed by ProgressOperationRunner (shown inline with operations).
            await Task.CompletedTask;
        }

        public void HideProgressOverlay()
        {
            // Progress overlay is managed by ProgressOperationRunner.
        }

        public async Task UpdateProgressAsync(int percent, string status, string? substatus = null)
        {
            // Progress updates are handled through ProgressOperationRunner callbacks.
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

            // WebView2 dialog first (matches main_shell); classic WinForms dialog is the fallback
            // when WebView2 init fails (signaled via DialogResult.Abort).
            using (var webDialog = new DisableOptionsDialogWebView { StartPosition = FormStartPosition.CenterParent })
            {
                var result = webDialog.ShowDialog(this);
                if (result != DialogResult.Abort)
                {
                    if (result != DialogResult.OK)
                        return (false, false);
                    return (true, webDialog.SelectedOption == DisableOptionsDialog.DisableOption.DeletePermanently);
                }
            }

            using var dialog = new DisableOptionsDialog();
            dialog.StartPosition = FormStartPosition.CenterParent;
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return (false, false);

            return (true, dialog.SelectedOption == DisableOptionsDialog.DisableOption.DeletePermanently);
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

        public bool ShowRestartAppDialog(string message)
        {
            if (InvokeRequired)
            {
                return (bool)Invoke(new Func<bool>(() => ShowRestartAppDialog(message)));
            }

            using var dialog = new RestartAppDialog(message);
            dialog.StartPosition = FormStartPosition.CenterParent;
            return dialog.ShowDialog(this) == DialogResult.OK;
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

        public (DialogResult Result, ModGenerationResult? GenerationResult) ShowClassicHeroSelector()
        {
            if (InvokeRequired)
            {
                return ((DialogResult, ModGenerationResult?))Invoke(
                    new Func<(DialogResult, ModGenerationResult?)>(ShowClassicHeroSelector));
            }

            using var classic = new UI.Forms.SelectHero(_configService);
            classic.StartPosition = FormStartPosition.CenterParent;
            var result = classic.ShowDialog(this);
            return (result, classic.GenerationResult);
        }

        public (DialogResult Result, ModGenerationResult? GenerationResult) ShowMiscForm(string? targetPath)
        {
            if (InvokeRequired)
            {
                return ((DialogResult, ModGenerationResult?))Invoke(
                    new Func<string?, (DialogResult, ModGenerationResult?)>(ShowMiscForm), targetPath);
            }

            if (string.IsNullOrEmpty(targetPath)) return (DialogResult.Cancel, null);

            // Try WebView2 version first
            using var webViewForm = new ArdysaModsTools.UI.Forms.MiscFormWebView(
                targetPath,
                _logger.Log,
                DisableAllButtons,
                EnableAllButtons);

            webViewForm.StartPosition = FormStartPosition.CenterParent;
            var result = webViewForm.ShowDialog(this);

            // If WebView2 initialization failed (Abort), fall back to classic WinForms
            if (result == DialogResult.Abort)
            {
                _logger.Log("WebView2 MiscForm failed to initialize. Using classic fallback.");

                using var classicForm = new ArdysaModsTools.MiscForm(
                    targetPath,
                    _logger.Log,
                    DisableAllButtons,
                    EnableAllButtons);

                classicForm.StartPosition = FormStartPosition.CenterParent;
                var classicResult = classicForm.ShowDialog(this);
                return (classicResult, classicForm.GenerationResult);
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
                    // Ignore errors, use default version info
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
