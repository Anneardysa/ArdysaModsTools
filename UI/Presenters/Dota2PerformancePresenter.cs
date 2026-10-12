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
        private readonly IMapCleanupService _mapCleanup;
        private readonly IAppLogger _logService;
        private readonly string? _gamePath;

        internal const string FoliageKey = "amt_remove_foliage";
        internal const string PropsKey = "amt_remove_props";

        public Dota2PerformancePresenter(
            IDota2PerformanceView view,
            IAutoexecService autoexecService,
            IMapCleanupService mapCleanup,
            IAppLogger logService,
            string? gamePath = null)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _autoexecService = autoexecService ?? throw new ArgumentNullException(nameof(autoexecService));
            _mapCleanup = mapCleanup ?? throw new ArgumentNullException(nameof(mapCleanup));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _gamePath = gamePath;

            _view.OnViewShown += View_OnViewShown;
            _view.OnApplySettingsRequested += View_OnApplySettingsRequested;
            _view.OnExportCfgRequested += View_OnExportCfgRequested;
            _view.OnDeleteCfgRequested += View_OnDeleteCfgRequested;
        }

        internal static (Dictionary<string, string> Cvars, HashSet<MapCleanupGroup> Cleanup) SplitSettings(Dictionary<string, string> settings)
        {
            var cvars = new Dictionary<string, string>(settings, StringComparer.OrdinalIgnoreCase);
            var cleanup = new HashSet<MapCleanupGroup>();
            if (cvars.Remove(FoliageKey, out var foliage) && foliage != "0") cleanup.Add(MapCleanupGroup.Foliage);
            if (cvars.Remove(PropsKey, out var props) && props != "0") cleanup.Add(MapCleanupGroup.Props);
            return (cvars, cleanup);
        }

        private static void AddCleanupKeys(Dictionary<string, string> settings, MapCleanupStatus status)
        {
            settings[FoliageKey] = status.Foliage ? "1" : "0";
            settings[PropsKey] = status.Props ? "1" : "0";
        }

        private async Task<MapCleanupStatus?> PushMapCleanupStatusAsync(bool sync = false)
        {
            try
            {
                var status = await _mapCleanup.GetStatusAsync(_gamePath);
                await _view.SetMapCleanupStatusAsync(JsonSerializer.Serialize(new { foliage = status.Foliage, props = status.Props, mounted = status.Mounted, sync }));
                return status;
            }
            catch (Exception ex)
            {
                _logService.LogError($"[Dota2PerformancePresenter] Failed to read map cleanup status: {ex.Message}", ex);
                await _view.SetMapCleanupStatusAsync(JsonSerializer.Serialize(new { mounted = true, sync = false }));
                return null;
            }
        }

        private async Task<string?> ApplyCleanupAsync(HashSet<MapCleanupGroup> enabled)
        {
            try
            {
                var status = await _mapCleanup.GetStatusAsync(_gamePath);
                if (status.Foliage == enabled.Contains(MapCleanupGroup.Foliage) && status.Props == enabled.Contains(MapCleanupGroup.Props))
                    return null;

                await _view.ShowToastAsync(Loc.T("perf.toast.cleanupWorking"), "info");
                await _mapCleanup.SetEnabledAsync(_gamePath, enabled);
                return null;
            }
            catch (Exception ex)
            {
                _logService.LogError($"[Dota2PerformancePresenter] Map cleanup failed: {ex.Message}", ex);
                return Loc.T("perf.toast.cleanupFailed", new { error = ex.Message });
            }
        }

        private async void View_OnDeleteCfgRequested(object? sender, EventArgs e)
        {
            try
            {
                var deleted = await _autoexecService.DeleteCfgAsync(_gamePath);
                var before = await _mapCleanup.GetStatusAsync(_gamePath);
                var cleanupError = await ApplyCleanupAsync(new HashSet<MapCleanupGroup>());

                if (cleanupError != null)
                    await _view.ShowToastAsync(cleanupError, "error");
                else if (deleted || before.Foliage || before.Props)
                    await _view.ShowToastAsync(Loc.T("perf.toast.removed"), "success");
                else
                    await _view.ShowToastAsync(Loc.T("perf.toast.notFound"), "info");

                if (deleted)
                    await _view.ShowCfgBannerAsync(Loc.T("perf.banner.recommended"), "warning");
            }
            catch (Exception ex)
            {
                _logService.LogError($"[Dota2PerformancePresenter] Failed to delete autoexec.cfg: {ex.Message}", ex);
                await _view.ShowToastAsync(Loc.T("perf.toast.removeFailed", new { error = ex.Message }), "error");
            }
            finally
            {
                await PushMapCleanupStatusAsync(sync: true);
            }
        }

        private async void View_OnViewShown(object? sender, EventArgs e)
        {
            var status = await PushMapCleanupStatusAsync();
            try
            {
                var settings = await _autoexecService.LoadCurrentSettingsAsync(_gamePath);
                if (settings.Count > 0)
                {
                    int count = settings.Count;
                    if (status != null) AddCleanupKeys(settings, status);
                    await _view.LoadSettingsAsync(JsonSerializer.Serialize(settings));
                    await _view.ShowToastAsync(Loc.T("perf.toast.loaded", new { count }), "success");
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

                var (cvars, cleanup) = SplitSettings(settings);
                await _autoexecService.ApplySettingsAsync(_gamePath, cvars);
                var cleanupError = await ApplyCleanupAsync(cleanup);

                await _view.ShowCfgBannerAsync(string.Empty, "ok");
                await _view.ShowToastAsync(cleanupError ?? Loc.T("perf.toast.saved"), cleanupError == null ? "success" : "error");

                var reload = new Dictionary<string, string>(cvars, StringComparer.OrdinalIgnoreCase);
                AddCleanupKeys(reload, await _mapCleanup.GetStatusAsync(_gamePath));
                await _view.LoadSettingsAsync(JsonSerializer.Serialize(reload));
            }
            catch (Exception ex)
            {
                _logService.LogError($"[Dota2PerformancePresenter] Failed to apply settings: {ex.Message}", ex);
                await _view.ShowToastAsync(Loc.T("perf.toast.saveFailed", new { error = ex.Message }), "error");
            }
            finally
            {
                await PushMapCleanupStatusAsync();
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

                await _autoexecService.ExportCfgAsync(exportPath, SplitSettings(settings).Cvars);
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
