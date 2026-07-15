using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Services.Localization;
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
            _view.OnDeleteCfgRequested += View_OnDeleteCfgRequested;
        }

        private async void View_OnDeleteCfgRequested(object? sender, EventArgs e)
        {
            try
            {
                var deleted = await _autoexecService.DeleteCfgAsync(_gamePath);
                if (deleted)
                {
                    await _view.ShowToastAsync(Loc.T("perf.toast.removed"), "success");
                    await _view.ShowCfgBannerAsync(Loc.T("perf.banner.recommended"), "warning");
                }
                else
                {
                    await _view.ShowToastAsync(Loc.T("perf.toast.notFound"), "info");
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"[Dota2PerformancePresenter] Failed to delete autoexec.cfg: {ex.Message}", ex);
                await _view.ShowToastAsync(Loc.T("perf.toast.removeFailed", new { error = ex.Message }), "error");
            }
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
                    await _view.ShowToastAsync(Loc.T("perf.toast.loaded", new { count = settings.Count }), "success");
                }
                else
                {
                    await _view.ShowCfgBannerAsync(Loc.T("perf.banner.recommended"), "warning");
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"[Dota2PerformancePresenter] Failed to load initial settings: {ex.Message}", ex);
                await _view.ShowToastAsync(Loc.T("perf.toast.loadFailed", new { error = ex.Message }), "error");
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

                await _view.ShowCfgBannerAsync(string.Empty, "ok");
                await _view.ShowToastAsync(Loc.T("perf.toast.saved"), "success");
                await _view.LoadSettingsAsync(jsonSettings);
            }
            catch (Exception ex)
            {
                _logService.LogError($"[Dota2PerformancePresenter] Failed to apply settings: {ex.Message}", ex);
                await _view.ShowToastAsync(Loc.T("perf.toast.saveFailed", new { error = ex.Message }), "error");
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
                await _view.ShowToastAsync(Loc.T("perf.toast.exported", new { file = System.IO.Path.GetFileName(exportPath) }), "success");
            }
            catch (Exception ex)
            {
                _logService.LogError($"[Dota2PerformancePresenter] Failed to export cfg: {ex.Message}", ex);
                await _view.ShowToastAsync(Loc.T("perf.toast.exportFailed", new { error = ex.Message }), "error");
            }
        }
    }
}
