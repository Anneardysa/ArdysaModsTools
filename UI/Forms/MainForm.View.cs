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
    /// Partial class containing IMainFormView interface implementation.
    /// Separates UI interface methods from the main form logic.
    /// </summary>
    public partial class MainForm : IMainFormView
    {
        #region IMainFormView - Status Updates

        /// <summary>
        /// Updates the mod status indicator (dot and text).
        /// </summary>
        public void SetModsStatus(bool isActive, string statusText)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => SetModsStatus(isActive, statusText)));
                return;
            }

            statusModsDotLabel.BackColor = isActive 
                ? System.Drawing.Color.FromArgb(0, 255, 100) 
                : System.Drawing.Color.FromArgb(255, 80, 80);
            statusModsTextLabel.Text = statusText;
            statusModsTextLabel.ForeColor = statusModsDotLabel.BackColor;
        }

        /// <summary>
        /// Updates the version label.
        /// </summary>
        public void SetVersion(string version)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => SetVersion(version)));
                return;
            }
            versionLabel.Text = version;
        }

        #endregion

        #region IMainFormView - Button States

        /// <summary>
        /// Sets whether a specific button is enabled.
        /// </summary>
        public void SetButtonEnabled(string buttonName, bool enabled)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => SetButtonEnabled(buttonName, enabled)));
                return;
            }

            Control? button = buttonName.ToLowerInvariant() switch
            {
                "autodetect" => autoDetectButton,
                "manualdetect" => manualDetectButton,
                "install" => installButton,
                "disable" => disableButton,
                "updatepatcher" or "patch" => updatePatcherButton,
                "miscellaneous" or "misc" => miscellaneousButton,
                "selecthero" or "hero" => btn_OpenSelectHero,
                _ => null
            };

            if (button != null)
                button.Enabled = enabled;
        }

        #endregion

        #region IMainFormView - Dialogs

        /// <summary>
        /// Shows a message box to the user.
        /// </summary>
        public DialogResult ShowMessageBox(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            if (InvokeRequired)
            {
                return (DialogResult)Invoke(new Func<DialogResult>(() => 
                    ShowMessageBox(message, title, buttons, icon)));
            }
            return MessageBox.Show(this, message, title, buttons, icon);
        }

        /// <summary>
        /// Shows a modern styled message dialog.
        /// </summary>
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

        /// <summary>
        /// Shows a folder selection dialog.
        /// </summary>
        public string? ShowFolderDialog(string title)
        {
            if (InvokeRequired)
            {
                return (string?)Invoke(new Func<string?>(() => ShowFolderDialog(title)));
            }

            using var dialog = new FolderBrowserDialog { Description = title };
            return dialog.ShowDialog(this) == DialogResult.OK ? dialog.SelectedPath : null;
        }

        /// <summary>
        /// Shows a file selection dialog.
        /// </summary>
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

        /// <summary>
        /// Shows the install method selection dialog.
        /// </summary>
        public bool? ShowInstallMethodDialog()
        {
            if (InvokeRequired)
            {
                return (bool?)Invoke(new Func<bool?>(() => ShowInstallMethodDialog()));
            }

            using var dialog = new InstallMethodDialog();
            dialog.StartPosition = FormStartPosition.CenterParent;
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return null;
            return dialog.SelectedMethod != InstallMethod.ManualInstall;
        }

        #endregion

        #region IMainFormView - Progress Overlay

        /// <summary>
        /// Shows the progress overlay.
        /// </summary>
        public async Task ShowProgressOverlayAsync()
        {
            // Progress overlay is managed by ProgressOperationRunner
            // This is a no-op as progress overlays are shown inline with operations
            await Task.CompletedTask;
        }

        /// <summary>
        /// Hides the progress overlay.
        /// </summary>
        public void HideProgressOverlay()
        {
            // Progress overlay is managed by ProgressOperationRunner
            // This is a no-op as progress overlays are hidden automatically
        }

        /// <summary>
        /// Updates progress overlay status.
        /// </summary>
        public async Task UpdateProgressAsync(int percent, string status, string? substatus = null)
        {
            // Progress updates are handled through ProgressOperationRunner callbacks
            // This is provided for interface compatibility
            await Task.CompletedTask;
        }

        #endregion

        #region IMainFormView - Logging

        /// <summary>
        /// Logs a message to the console.
        /// </summary>
        public void Log(string message)
        {
            _logger.Log(message);
        }

        /// <summary>
        /// Clears the console log.
        /// </summary>
        public void ClearLog()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(ClearLog));
                return;
            }
            mainConsoleBox.Clear();
        }

        #endregion

        #region IMainFormView - Form State

        /// <summary>
        /// Gets whether the form is currently visible.
        /// </summary>
        bool IMainFormView.IsVisible => Visible && !IsDisposed;

        /// <summary>
        /// Invokes an action on the UI thread if required.
        /// </summary>
        public void InvokeOnUIThread(Action action)
        {
            if (InvokeRequired)
            {
                BeginInvoke(action);
            }
            else
            {
                action();
            }
        }

        /// <summary>
        /// Closes the form.
        /// </summary>
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

        /// <summary>
        /// Shows the disable options dialog and returns user selection.
        /// </summary>
        public (bool ShouldProceed, bool DeletePermanently) ShowDisableOptionsDialog()
        {
            if (InvokeRequired)
            {
                return ((bool, bool))Invoke(new Func<(bool, bool)>(ShowDisableOptionsDialog));
            }

            using var dialog = new DisableOptionsDialog();
            dialog.StartPosition = FormStartPosition.CenterParent;
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return (false, false);
            
            return (true, dialog.SelectedOption == DisableOptionsDialog.DisableOption.DeletePermanently);
        }

        /// <summary>
        /// Shows the patch required dialog after installation.
        /// Uses native WinForms dialog for maximum reliability.
        /// </summary>
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

        /// <summary>
        /// Shows the install required dialog when mods are not installed.
        /// </summary>
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

        /// <summary>
        /// Shows the restart app dialog.
        /// </summary>
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

        /// <summary>
        /// Shows the support dialog.
        /// </summary>
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

        /// <summary>
        /// Shows the hero gallery and returns the generation result.
        /// </summary>
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

        /// <summary>
        /// Shows the miscellaneous form and returns the generation result.
        /// Uses WebView2 version first, falls back to classic WinForms if WebView2 fails.
        /// </summary>
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

        /// <summary>
        /// Shows the status details form.
        /// </summary>
        public async void ShowStatusDetails(ModStatusInfo status, Func<Task> patchAction)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ShowStatusDetails(status, patchAction)));
                return;
            }

            // StatusDetailsForm requires ModStatusInfo and DotaVersionInfo
            // Get version info asynchronously
            var versionInfo = new DotaVersionInfo();
            
            // Try to get cached version info from version service
            if (_versionService != null && !string.IsNullOrEmpty(targetPath))
            {
                try
                {
                    // Use Task.Run to avoid deadlock on UI thread
                    var timeoutTask = Task.Delay(1000);
                    var versionTask = Task.Run(() => _versionService.GetVersionInfoAsync(targetPath));
                    
                    if (await Task.WhenAny(versionTask, timeoutTask).ConfigureAwait(false) == versionTask 
                        && versionTask.IsCompletedSuccessfully)
                    {
                        versionInfo = await versionTask.ConfigureAwait(false);
                    }
                }
                catch
                {
                    // Ignore errors, use default version info
                }
            }
            
            using var dialog = new StatusDetailsForm(status, versionInfo, () => patchAction());
            dialog.StartPosition = FormStartPosition.CenterParent;
            dialog.ShowDialog(this);
        }

        #endregion

        #region IMainFormView - Extended Status Updates

        // ShowPatchMenu is implemented in MainForm.cs

        #endregion

        #region IMainFormView - Application Control

        /// <summary>
        /// Gets or sets the current target path (Dota 2 installation path).
        /// </summary>
        string? IMainFormView.TargetPath
        {
            get => targetPath;
            set => targetPath = value;
        }

        /// <summary>
        /// Restarts the application.
        /// </summary>
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

        /// <summary>
        /// Runs an operation with progress overlay.
        /// </summary>
        public async Task<OperationResult> RunWithProgressOverlayAsync(
            string initialStatus,
            Func<ProgressContext, Task<OperationResult>> operation,
            bool hideDownloadSpeed = false)
        {
            // Adapter to convert ProgressContext to ProgressOperationRunnerContext
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
                hideDownloadSpeed);
        }

        /// <summary>
        /// Gets the application path (directory where the exe is located).
        /// </summary>
        public string AppPath => Path.GetDirectoryName(Process.GetCurrentProcess().MainModule!.FileName)!;

        #endregion
    }
}
