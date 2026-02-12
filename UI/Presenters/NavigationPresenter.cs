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
using ArdysaModsTools.Helpers;
using ArdysaModsTools.UI.Interfaces;
using ArdysaModsTools.UI.Forms;

namespace ArdysaModsTools.UI.Presenters
{
    /// <summary>
    /// Handles navigation, dialog showing, and connectivity checking.
    /// Extracted from MainFormPresenter for Single Responsibility Principle.
    /// </summary>
    public class NavigationPresenter : INavigationPresenter
    {
        #region Private Fields

        private readonly IMainFormView _view;
        private readonly Logger _logger;
        private readonly StatusService _status;
        
        private bool _patchDialogDismissedByUser;

        #endregion

        #region Properties

        /// <inheritdoc />
        public string? TargetPath { get; set; }

        /// <inheritdoc />
        public ModStatusInfo? CurrentStatus { get; set; }

        #endregion

        #region Events

        /// <inheritdoc />
        public event Func<Task>? StatusRefreshRequested;

        /// <inheritdoc />
        public event Func<Task>? PatchRequested;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new NavigationPresenter.
        /// </summary>
        /// <param name="view">The view interface for UI updates</param>
        /// <param name="logger">Logger instance for logging</param>
        public NavigationPresenter(IMainFormView view, Logger logger)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _status = new StatusService(_logger);
        }

        #endregion

        #region Form Navigation

        /// <inheritdoc />
        public async Task OpenMiscellaneousAsync()
        {
            if (string.IsNullOrEmpty(TargetPath)) return;

            // Check remote feature access control
            if (!await CheckFeatureAccessAsync(FeatureAccessService.MiscellaneousFeature))
                return;

            // Show Misc Form and get result
            var (result, generationResult) = _view.ShowMiscForm(TargetPath);
            
            if (generationResult != null)
            {
                LogGenerationResult(generationResult);
            }

            // Refresh status if valid result
            if (result == DialogResult.OK)
            {
                await RaiseStatusRefreshAsync();
                
                // If generation was successful, check if patching needed
                if (generationResult != null && generationResult.Success)
                {
                    // Delay slightly to let status settle
                    await Task.Delay(500);
                    await ShowPatchRequiredIfNeededAsync("Custom ModsPack (Misc) installed successfully!");
                }
            }
        }

        /// <inheritdoc />
        public async Task OpenHeroSelectionAsync()
        {
            if (string.IsNullOrEmpty(TargetPath))
            {
                return;
            }

            // Check remote feature access control
            if (!await CheckFeatureAccessAsync(FeatureAccessService.SkinSelectorFeature))
                return;

            // Check GitHub access before opening form
            var hasAccess = await CheckHeroesJsonAccessAsync();
            if (!hasAccess)
            {
                _view.ShowMessageBox(
                    "Unable to access this feature.\n\n" +
                    "Please check your internet connection and try again.\n" +
                    "The Select Hero feature requires online access to load hero data.",
                    "Connection Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                _logger.Log("Connection to server failed.");
                return;
            }

            // Show beta warning dialog
            var betaResult = _view.ShowMessageBox(
                "[BETA VERSION] - Skin Selector\n\n" +
                "This feature is currently in beta. Not all hero sets are available yet.\n\n" +
                "More hero sets will be added soon!\n\n" +
                "IMPORTANT:\n" +
                "Using this feature will remove your ModsPack.\n" +
                "Custom sets are built independently and are NOT linked to the standard ModsPack.\n\n" +
                "Do you want to continue?",
                "Beta Feature Warning",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (betaResult != DialogResult.Yes)
            {
                return;
            }

            // Try modern WebView2 gallery first, fallback to classic if it fails
            DialogResult dialogResult;
            ModGenerationResult? generationResult = null;
            
            var (res, gen) = _view.ShowHeroGallery();
            dialogResult = res;
            generationResult = gen;
            
            // If WebView2 failed (Abort), fallback to classic SelectHero
            if (dialogResult == DialogResult.Abort)
            {
                _logger.Log("WebView2 failed, falling back to classic SelectHero...");
                var (resClassic, genClassic) = _view.ShowClassicHeroSelector();
                dialogResult = resClassic;
                generationResult = genClassic;
            }
            
            // Log generation result
            if (generationResult != null)
            {
                LogGenerationResult(generationResult);
            }
            
            // Always refresh status after hero gallery closes (force refresh)
            await RaiseStatusRefreshAsync();
            
            // If generation was successful, check if patching needed
            if (dialogResult == DialogResult.OK)
            {
                await Task.Delay(500);
                
                // If NOT NeedUpdate (e.g. just replaced files without requiring patch), show success
                if (CurrentStatus?.Status == ModStatus.Ready)
                {
                    _view.ShowNotification("Success", 
                        "Custom ModsPack installed successfully!",
                        System.Windows.Forms.ToolTipIcon.Info, 3000);
                }
                else
                {
                    await ShowPatchRequiredIfNeededAsync("Custom ModsPack installed successfully!");
                }
            }
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public async Task ShowInstallDialogIfNeededAsync()
        {
            // Only show dialog if mods are not installed
            if (IsModFilePresent())
                return;

            if (_view.ShowInstallRequiredDialog())
            {
                // User clicked Install Now - this should be handled by parent coordinator
                // by wiring up events appropriately
                _logger.Log("User requested install from dialog");
            }
        }

        /// <inheritdoc />
        public async Task ShowPatchRequiredIfNeededAsync(string? successMessage = null, bool fromDetection = false)
        {
            if (string.IsNullOrEmpty(TargetPath))
                return;

            try
            {
                var statusInfo = await _status.GetDetailedStatusAsync(TargetPath);

                // If status is Ready, no action needed - also reset dismissed flag
                if (statusInfo.Status == ModStatus.Ready)
                {
                    _patchDialogDismissedByUser = false;
                    return;
                }

                // If from detection and user already dismissed with "Later", skip showing again
                if (fromDetection && _patchDialogDismissedByUser)
                {
                    return;
                }

                // If status is NeedUpdate or Disabled - show dialog
                if (statusInfo.Status == ModStatus.NeedUpdate ||
                    statusInfo.Status == ModStatus.Disabled)
                {
                    var message = successMessage ?? "Patch required to enable mods.";
                    
                    // If user clicked "Patch Now", execute patch via event
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
                        // User clicked "Later" - remember this choice for detection-triggered dialogs
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

        /// <inheritdoc />
        public async Task<bool> CheckHeroesJsonAccessAsync()
        {
            var client = HttpClientProvider.Client;
            
            // Use CdnConfig priority order: R2 → jsDelivr → GitHub Raw
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
                    // Try next CDN
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

        /// <summary>
        /// Checks if a feature is accessible via remote R2 config.
        /// Shows a message and returns false if the feature is disabled.
        /// Fail-open: returns true if config cannot be fetched.
        /// </summary>
        /// <param name="featureName">Feature name constant from FeatureAccessService.</param>
        /// <returns>True if access is allowed, false if blocked.</returns>
        private async Task<bool> CheckFeatureAccessAsync(string featureName)
        {
            try
            {
                // Do NOT use ConfigureAwait(false) here — we need to return to 
                // the UI thread to safely call _view.ShowMessageBox() below.
                var config = await FeatureAccessService.GetConfigAsync();
                var feature = featureName switch
                {
                    FeatureAccessService.SkinSelectorFeature => config.SkinSelector,
                    FeatureAccessService.MiscellaneousFeature => config.Miscellaneous,
                    _ => new FeatureAccess()
                };

                if (!feature.Enabled)
                {
                    // Map feature constant to friendly display name
                    var displayName = featureName switch
                    {
                        FeatureAccessService.SkinSelectorFeature => "Skin Selector",
                        FeatureAccessService.MiscellaneousFeature => "Miscellaneous",
                        _ => featureName
                    };

                    FeatureUnavailableDialog.Show(
                        _view as IWin32Window,
                        displayName,
                        feature.GetDisplayMessage());
                    _logger.Log($"[ACCESS] {featureName} is disabled by remote config");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                // Fail-open: if access check itself fails, allow the feature
                _logger.Log($"[ACCESS] Check failed for {featureName}: {ex.Message} — allowing access");
                return true;
            }
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
