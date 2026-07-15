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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Config;
using ArdysaModsTools.Core.Services.Localization;
using ArdysaModsTools.Helpers;
using ArdysaModsTools.UI.Interfaces;
using ArdysaModsTools.UI.Forms;

namespace ArdysaModsTools.UI.Presenters
{
    public class NavigationPresenter : INavigationPresenter
    {
        #region Private Fields

        private readonly IMainFormView _view;
        private readonly Logger _logger;
        private readonly IStatusService _status;
        
        private bool _patchDialogDismissedByUser;

        #endregion

        #region Properties

        public string? TargetPath { get; set; }

        public ModStatusInfo? CurrentStatus { get; set; }

        #endregion

        #region Events

        public event Func<Task>? StatusRefreshRequested;

        public event Func<Task>? PatchRequested;

        #endregion

        #region Constructor

        public NavigationPresenter(IMainFormView view, Logger logger, IStatusService statusService)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _status = statusService ?? throw new ArgumentNullException(nameof(statusService));
        }

        #endregion

        #region Form Navigation

        public async Task OpenMiscellaneousAsync()
        {
            if (string.IsNullOrEmpty(TargetPath)) return;

            if (!await CheckFeatureAccessAsync(FeatureAccessService.MiscellaneousFeature))
                return;

            var (result, generationResult) = _view.ShowMiscForm(TargetPath);
            
            if (generationResult != null)
            {
                LogGenerationResult(generationResult);
            }

            if (result == DialogResult.OK)
            {
                await RaiseStatusRefreshAsync();
                
                if (generationResult != null && generationResult.Success)
                {
                    await Task.Delay(500);
                    await ShowPatchRequiredIfNeededAsync(Loc.T("nav.install.successMisc"));
                }
            }
        }

        public async Task OpenHeroSelectionAsync()
        {
            if (string.IsNullOrEmpty(TargetPath))
            {
                return;
            }

            if (!await CheckFeatureAccessAsync(FeatureAccessService.SkinSelectorFeature))
                return;

            var hasAccess = await CheckHeroesJsonAccessAsync();
            if (!hasAccess)
            {
                _view.ShowMessageBox(
                    Loc.T("nav.connectionError.body"),
                    Loc.T("nav.connectionError.title"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                _logger.Log("Connection to server failed.");
                return;
            }

            var proceed = await _view.ShowShellConfirmAsync(
                Loc.T("nav.beta.eyebrow"),
                Loc.T("nav.beta.heading"),
                Loc.T("nav.beta.body"),
                Loc.T("nav.beta.note"),
                Loc.T("common.continue"),
                Loc.T("common.cancel"),
                countdownSeconds: 5);

            if (!proceed)
            {
                return;
            }

            DialogResult dialogResult;
            ModGenerationResult? generationResult = null;
            
            var (res, gen) = _view.ShowHeroGallery();
            dialogResult = res;
            generationResult = gen;
            
            if (dialogResult == DialogResult.Abort)
            {
                _logger.Log("WebView2 failed to initialize.");
                _view.ShowStyledMessage(
                    "Skin Selector Unavailable",
                    "The Skin Selector requires the Microsoft Edge WebView2 runtime, which could not be initialized.\n\n" +
                    "Please install or repair the WebView2 runtime, then try again.",
                    StyledMessageType.Warning);
                dialogResult = DialogResult.Cancel;
            }
            
            if (generationResult != null)
            {
                LogGenerationResult(generationResult);
            }
            
            await RaiseStatusRefreshAsync();
            
            if (dialogResult == DialogResult.OK)
            {
                await Task.Delay(500);
                
                if (CurrentStatus?.Status == ModStatus.Ready)
                {
                    _view.ShowNotification(Loc.T("common.success"),
                        Loc.T("nav.install.success"),
                        System.Windows.Forms.ToolTipIcon.Info, 3000);
                }
                else
                {
                    await ShowPatchRequiredIfNeededAsync(Loc.T("nav.install.success"));
                }
            }
        }

        public void ShowStatusDetails()
        {
            if (CurrentStatus == null) return;

            _view.ShowStatusDetails(CurrentStatus, async () =>
            {
                if (PatchRequested != null)
                {
                    await PatchRequested.Invoke();
                }
            });
        }

        #endregion

        #region Dialog Navigation

        public async Task ShowInstallDialogIfNeededAsync()
        {
            if (IsModFilePresent())
                return;

            if (_view.ShowInstallRequiredDialog())
            {
                _logger.Log("User requested install from dialog");
            }
        }

        public async Task ShowPatchRequiredIfNeededAsync(string? successMessage = null, bool fromDetection = false)
        {
            if (string.IsNullOrEmpty(TargetPath))
                return;

            try
            {
                var statusInfo = await _status.GetDetailedStatusAsync(TargetPath);

                if (statusInfo.Status == ModStatus.Ready)
                {
                    _patchDialogDismissedByUser = false;
                    return;
                }

                if (fromDetection && _patchDialogDismissedByUser)
                {
                    return;
                }

                if (statusInfo.Status == ModStatus.NeedUpdate ||
                    statusInfo.Status == ModStatus.Disabled)
                {
                    var message = successMessage ?? Loc.T("nav.patchRequired.default");
                    
                    if (_view.ShowPatchRequiredDialog(message))
                    {
                        _patchDialogDismissedByUser = false;
                        if (PatchRequested != null)
                        {
                            await PatchRequested.Invoke();
                        }
                    }
                    else
                    {
                        _patchDialogDismissedByUser = true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"[STATUS] Post-install check failed: {ex.Message}");
            }
        }

        #endregion

        #region Connectivity

        public async Task<bool> CheckHeroesJsonAccessAsync()
        {
            var client = HttpClientProvider.Client;
            
            foreach (var baseUrl in CdnConfig.GetCdnBaseUrls())
            {
                var url = $"{baseUrl}/Assets/heroes.json";
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    using var request = new HttpRequestMessage(HttpMethod.Head, url);
                    using var response = await client.SendAsync(request, cts.Token);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.Log($"[NET] Connected via {new Uri(baseUrl).Host}");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log($"[NET] Failed {new Uri(baseUrl).Host}: {ex.Message}");
                }
            }

            return false;
        }

        #endregion

        #region Private Helpers

        private bool IsModFilePresent()
        {
            if (string.IsNullOrEmpty(TargetPath))
                return false;

            var vpkPath = System.IO.Path.Combine(TargetPath, "game", "_ArdysaMods", "pak01_dir.vpk");
            return System.IO.File.Exists(vpkPath);
        }

        private async Task<bool> CheckFeatureAccessAsync(string featureName)
        {
            var result = await FeatureAccessService.CheckFeatureAsync(featureName);

            if (result.IsDevModeBypass)
            {
                _logger.Log($"[DEV] Bypassed feature gate: {result.FeatureDisplayName}");
                return true;
            }

            if (!result.IsAllowed)
            {
                await UIHelpers.ShowFeatureUnavailableAsync(
                    _view, result.FeatureDisplayName, result.BlockedMessage!, _logger.Log);
                return false;
            }

            return true;
        }

        private void LogGenerationResult(ModGenerationResult result)
        {
            if (result.Success)
            {
                _logger.Log($"[GEN] Success: {result.OptionsCount} items generated");
            }
            else
            {
                _logger.Log($"[GEN] Failed: {result.ErrorMessage}");
            }
        }

        private async Task RaiseStatusRefreshAsync()
        {
            if (StatusRefreshRequested != null)
            {
                await StatusRefreshRequested.Invoke();
            }
        }

        #endregion
    }
}
