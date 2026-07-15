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
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;

namespace ArdysaModsTools.UI.Interfaces
{
    public interface IMainFormView
    {
        #region Status Updates

        void SetModsStatus(bool isActive, string statusText);

        void SetModsStatusDetailed(ModStatusInfo statusInfo);

        void ShowCheckingState();

        void SetVersion(string version);

        #endregion

        #region Button States

        void EnableAllButtons();

        void EnableDetectionButtonsOnly();

        void DisableAllButtons();

        void SetButtonEnabled(string buttonName, bool enabled);

        #endregion

        #region Dialogs

        DialogResult ShowMessageBox(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon);

        DialogResult ShowStyledMessage(string title, string message, Forms.StyledMessageType type);

        void ShowInstallFailureCard(string title, string body);

        void ShowInstallCompleteCard(string title, string body);

        string? ShowFolderDialog(string title);

        string? ShowFileDialog(string title, string filter);

        bool? ShowInstallMethodDialog();

        string? PendingManualVpkPath { get; }

        void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info, int timeout = 3000);

        Task<bool> ShowShellConfirmAsync(
            string eyebrow,
            string heading,
            string body,
            string note = "",
            string confirmText = "Continue",
            string cancelText = "Cancel",
            int countdownSeconds = 0,
            string accent = "");

        void ShowShellToast(string title, string message, string variant = "success", int timeout = 4000);

        #endregion

        #region Progress Overlay

        Task ShowProgressOverlayAsync();

        void HideProgressOverlay();

        Task UpdateProgressAsync(int percent, string status, string? substatus = null);

        #endregion

        #region Logging

        void Log(string message);

        void ClearLog();

        #endregion

        #region Form State

        bool IsVisible { get; }

        void InvokeOnUIThread(Action action);

        void CloseForm();

        #endregion

        #region Extended Dialogs

        (bool ShouldProceed, bool DeletePermanently) ShowDisableOptionsDialog();

        bool ShowPatchRequiredDialog(string message);

        bool ShowInstallRequiredDialog();

        void ShowSupportDialog();

        (DialogResult Result, ModGenerationResult? GenerationResult) ShowHeroGallery();

        (DialogResult Result, ModGenerationResult? GenerationResult) ShowMiscForm(string? targetPath);

        void ShowStatusDetails(ModStatusInfo status, Func<Task> patchAction);

        #endregion

        #region Extended Status Updates

        void UpdatePatchButtonStatus(ModStatus? status, bool isError = false);

        void ShowPatchMenu();

        void UpdateButtonsForStatus(ModStatusInfo statusInfo);

        void SetPatchDetectedStatus();

        void SetDotaRunningState(bool isRunning);

        #endregion

        #region Application Control

        string? TargetPath { get; set; }

        void ShowPathFoundBanner(string path);

        void RestartApplication();

        Task<OperationResult> RunWithProgressOverlayAsync(
            string initialStatus,
            Func<ProgressContext, Task<OperationResult>> operation,
            bool hideDownloadSpeed = false,
            bool showPreview = false);

        string AppPath { get; }

        #endregion
    }
}

