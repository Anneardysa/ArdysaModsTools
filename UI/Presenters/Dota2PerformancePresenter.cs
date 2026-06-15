using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.UI.Interfaces;

namespace ArdysaModsTools.UI.Presenters
{
    public class Dota2PerformancePresenter
    {
        private readonly IDota2PerformanceView _view;
        private readonly IAutoexecService _autoexecService;
        private readonly IAppLogger _logService;
        private readonly string? _gamePath;

        public Dota2PerformancePresenter(
            IDota2PerformanceView view,
            IAutoexecService autoexecService,
            IAppLogger logService,
            string? gamePath = null)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _autoexecService = autoexecService ?? throw new ArgumentNullException(nameof(autoexecService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _gamePath = gamePath;

            _view.OnViewShown += View_OnViewShown;
            _view.OnApplySettingsRequested += View_OnApplySettingsRequested;
            _view.OnExportCfgRequested += View_OnExportCfgRequested;
        }

        private async void View_OnViewShown(object? sender, EventArgs e)
        {
            try
            {
                var settings = await _autoexecService.LoadCurrentSettingsAsync(_gamePath);
                if (settings.Count > 0)
                {
                    var json = JsonSerializer.Serialize(settings);
                    await _view.LoadSettingsAsync(json);
                    await _view.ShowToastAsync($"Loaded {settings.Count} settings from autoexec.cfg", "success");
                }
                else
                {
                    // No saved cfg: the view starts on a recommended preset. Surface this as a
                    // persistent, high-contrast banner (an auto-dismissing toast was easy to miss).
                    await _view.ShowCfgBannerAsync(
                        "These are recommended preset values — nothing has been written yet. Click [ APPLY TO AUTOEXEC.CFG ] to create the file in your Dota 2 config folder.",
                        "warning");
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"[Dota2PerformancePresenter] Failed to load initial settings: {ex.Message}", ex);
                await _view.ShowToastAsync($"Error loading autoexec.cfg: {ex.Message}", "error");
            }
        }

        private async void View_OnApplySettingsRequested(object? sender, string jsonSettings)
        {
            if (string.IsNullOrEmpty(jsonSettings)) return;

            try
            {
                var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonSettings);
                if (settings == null || settings.Count == 0) return;

                await _autoexecService.ApplySettingsAsync(_gamePath, settings);

                // The file now exists — dismiss the "missing" banner and confirm.
                await _view.ShowCfgBannerAsync(string.Empty, "ok");
                await _view.ShowToastAsync("autoexec.cfg saved successfully!", "success");
                await _view.LoadSettingsAsync(jsonSettings); // Reload to reflect what was saved
            }
            catch (Exception ex)
            {
                _logService.LogError($"[Dota2PerformancePresenter] Failed to apply settings: {ex.Message}", ex);
                await _view.ShowToastAsync($"Error: {ex.Message}", "error");
            }
        }

        private async void View_OnExportCfgRequested(object? sender, string jsonSettings)
        {
            if (string.IsNullOrEmpty(jsonSettings)) return;

            try
            {
                var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonSettings);
                if (settings == null || settings.Count == 0) return;

                var exportPath = _view.PromptForExportPath();
                if (string.IsNullOrEmpty(exportPath)) return;

                await _autoexecService.ExportCfgAsync(exportPath, settings);
                await _view.ShowToastAsync($"Exported to {System.IO.Path.GetFileName(exportPath)}", "success");
            }
            catch (Exception ex)
            {
                _logService.LogError($"[Dota2PerformancePresenter] Failed to export cfg: {ex.Message}", ex);
                await _view.ShowToastAsync($"Export failed: {ex.Message}", "error");
            }
        }
    }
}
