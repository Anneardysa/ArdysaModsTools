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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Helpers;
using Microsoft.Web.WebView2.Core;

namespace ArdysaModsTools.Core.Services.App
{
    public sealed record AppRepairPaths
    {
        public string WebView2UserDataFolder { get; init; } = WebView2EnvironmentHelper.UserDataFolder;

        public string LocalAppDataDir { get; init; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArdysaModsTools");

        public string RoamingAppDataDir { get; init; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArdysaModsTools");

        public string InstallDir { get; init; } = AppContext.BaseDirectory;

        public static AppRepairPaths Default { get; } = new();
    }

    public sealed class AppRepairService : IAppRepairService
    {
        internal const string SentinelFileName = ".repair-reset-webview2";

        private const string WebView2BootstrapperUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

        private readonly AppRepairPaths _paths;
        private readonly IAppLogger? _logger;
        private readonly Func<Task> _probeWebView2Environment;

        public AppRepairService(AppRepairPaths? paths = null, IAppLogger? logger = null, Func<Task>? probeWebView2Environment = null)
        {
            _paths = paths ?? AppRepairPaths.Default;
            _logger = logger;
            _probeWebView2Environment = probeWebView2Environment ?? (async () => await WebView2EnvironmentHelper.CreateEnvironmentAsync().ConfigureAwait(false));
        }

        public async Task<AppRepairReport> ScanAsync(IProgress<AppRepairProgress>? progress = null, CancellationToken ct = default)
        {
            var checks = new List<AppCheck>();

            void Step(int percent, AppCheck check)
            {
                ct.ThrowIfCancellationRequested();
                checks.Add(check);
                progress?.Report(new AppRepairProgress { Percent = percent, MessageKey = check.TitleKey });
            }

            Step(20, CheckWebView2Runtime());
            Step(40, await CheckWebView2ProfileAsync(_paths).ConfigureAwait(false));
            Step(60, CheckWebView2Leftovers(_paths));
            Step(80, CheckAppDataWritable(_paths));
            Step(100, CheckInstallFolderWritable(_paths));

            return new AppRepairReport { Checks = checks };
        }

        public async Task<AppRepairReport> RepairAsync(IReadOnlyList<string> ids, IProgress<AppRepairProgress>? progress = null, CancellationToken ct = default)
        {
            var idSet = new HashSet<string>(ids, StringComparer.Ordinal);

            if (idSet.Contains(nameof(AppCheckId.WebView2Runtime)))
            {
                progress?.Report(new AppRepairProgress { Percent = 10, MessageKey = "settings.repair.check.webView2Runtime.title" });
                await InstallWebView2RuntimeAsync(ct).ConfigureAwait(false);
            }

            if (idSet.Contains(nameof(AppCheckId.WebView2Profile)))
            {
                progress?.Report(new AppRepairProgress { Percent = 40, MessageKey = "settings.repair.check.webView2Profile.title" });
                ScheduleWebView2ProfileReset(_paths);
            }

            if (idSet.Contains(nameof(AppCheckId.WebView2Leftovers)))
            {
                progress?.Report(new AppRepairProgress { Percent = 60, MessageKey = "settings.repair.check.webView2Leftovers.title" });
                DeleteLeftovers(_paths);
            }

            if (idSet.Contains(nameof(AppCheckId.AppDataWritable)))
            {
                progress?.Report(new AppRepairProgress { Percent = 80, MessageKey = "settings.repair.check.appDataWritable.title" });
                try
                {
                    Directory.CreateDirectory(_paths.LocalAppDataDir);
                    Directory.CreateDirectory(_paths.RoamingAppDataDir);
                }
                catch (Exception ex)
                {
                    _logger?.Log($"[AppRepair] AppDataWritable fix failed: {ex.Message}");
                }
            }


            progress?.Report(new AppRepairProgress { Percent = 95, MessageKey = "settings.repair.rescan" });
            return await ScanAsync(progress: null, ct).ConfigureAwait(false);
        }


        private static AppCheck CheckWebView2Runtime()
        {
            var detection = WebView2Runtime.Detect();
            string state;
            string detailKey;

            switch (detection.Source)
            {
                case WebView2Source.Loader:
                    state = "pass";
                    detailKey = "settings.repair.check.webView2Runtime.ok";
                    break;
                case WebView2Source.Registry:
                    state = "advisory";
                    detailKey = "settings.repair.check.webView2Runtime.advisory";
                    break;
                default:
                    state = "fail";
                    detailKey = "settings.repair.check.webView2Runtime.bad";
                    break;
            }

            bool notPass = state != "pass";
            return new AppCheck
            {
                Id = nameof(AppCheckId.WebView2Runtime),
                State = state,
                TitleKey = "settings.repair.check.webView2Runtime.title",
                DetailKey = detailKey,
                Diagnostic = detection.Version != null ? $"version {detection.Version}" : detection.Diagnostic,
                CanAutoFix = notPass,
                NeedsConfirm = notPass,
                AppliesOnRestart = false
            };
        }

        private async Task<AppCheck> CheckWebView2ProfileAsync(AppRepairPaths paths)
        {
            bool writable = TryProbeWrite(paths.WebView2UserDataFolder, out var diagnostic);
            bool ok = writable;

            if (writable)
            {
                try
                {
                    await _probeWebView2Environment().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    ok = false;
                    diagnostic = $"{ex.GetType().Name}: {ex.Message}";
                }
            }

            return new AppCheck
            {
                Id = nameof(AppCheckId.WebView2Profile),
                State = ok ? "pass" : "fail",
                TitleKey = "settings.repair.check.webView2Profile.title",
                DetailKey = ok ? "settings.repair.check.webView2Profile.ok" : "settings.repair.check.webView2Profile.bad",
                Diagnostic = diagnostic,
                CanAutoFix = !ok,
                NeedsConfirm = !ok,
                AppliesOnRestart = !ok
            };
        }

        private static AppCheck CheckWebView2Leftovers(AppRepairPaths paths)
        {
            int count = CountLeftovers(paths);
            bool hasLeftovers = count > 0;

            return new AppCheck
            {
                Id = nameof(AppCheckId.WebView2Leftovers),
                State = hasLeftovers ? "advisory" : "pass",
                TitleKey = "settings.repair.check.webView2Leftovers.title",
                DetailKey = hasLeftovers ? "settings.repair.check.webView2Leftovers.bad" : "settings.repair.check.webView2Leftovers.ok",
                DetailVars = hasLeftovers ? new { count } : null,
                Diagnostic = hasLeftovers ? $"{count} leftover folder(s) next to {paths.WebView2UserDataFolder}" : null,
                CanAutoFix = hasLeftovers,
                NeedsConfirm = false,
                AppliesOnRestart = false
            };
        }

        private static AppCheck CheckAppDataWritable(AppRepairPaths paths)
        {
            try
            {
                if (!TryProbeWrite(paths.LocalAppDataDir, out var localDiag))
                    return AppDataResult("fail", "settings.repair.check.appDataWritable.bad", localDiag);

                if (!TryProbeWrite(paths.RoamingAppDataDir, out var roamingDiag))
                    return AppDataResult("fail", "settings.repair.check.appDataWritable.bad", roamingDiag);

                return AppDataResult("pass", "settings.repair.check.appDataWritable.ok", null);
            }
            catch (Exception ex)
            {
                return AppDataResult("unknown", "settings.repair.check.unknown", $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        private static AppCheck AppDataResult(string state, string detailKey, string? diagnostic) => new()
        {
            Id = nameof(AppCheckId.AppDataWritable),
            State = state,
            TitleKey = "settings.repair.check.appDataWritable.title",
            DetailKey = detailKey,
            Diagnostic = diagnostic,
            CanAutoFix = state != "pass",
            NeedsConfirm = false,
            AppliesOnRestart = false
        };

        private static AppCheck CheckInstallFolderWritable(AppRepairPaths paths)
        {
            bool ok = TryProbeWrite(paths.InstallDir, out var diagnostic);
            if (ok)
            {
                return new AppCheck
                {
                    Id = nameof(AppCheckId.InstallFolderWritable),
                    State = "pass",
                    TitleKey = "settings.repair.check.installFolderWritable.title",
                    DetailKey = "settings.repair.check.installFolderWritable.ok"
                };
            }

            return new AppCheck
            {
                Id = nameof(AppCheckId.InstallFolderWritable),
                State = "advisory",
                TitleKey = "settings.repair.check.installFolderWritable.title",
                DetailKey = "settings.repair.check.installFolderWritable.bad",
                DetailVars = new { isProtected = AdminHelper.IsInProtectedPath() },
                Diagnostic = diagnostic,
                CanAutoFix = false,
                NeedsConfirm = false,
                AppliesOnRestart = false
            };
        }


        private void ScheduleWebView2ProfileReset(AppRepairPaths paths)
        {
            try
            {
                Directory.CreateDirectory(paths.LocalAppDataDir);
                File.WriteAllText(Path.Combine(paths.LocalAppDataDir, SentinelFileName), DateTime.UtcNow.ToString("O"));
            }
            catch (Exception ex)
            {
                _logger?.Log($"[AppRepair] Failed to schedule WebView2 profile reset: {ex.Message}");
            }
        }

        private static void DeleteLeftovers(AppRepairPaths paths)
        {
            string parent = Path.GetDirectoryName(paths.WebView2UserDataFolder) ?? "";
            string baseName = Path.GetFileName(paths.WebView2UserDataFolder);
            if (!Directory.Exists(parent)) return;

            foreach (var dir in Directory.GetDirectories(parent, $"{baseName}.corrupt-*"))
            {
                try { Directory.Delete(dir, recursive: true); }
                catch {  }
            }
        }

        private static int CountLeftovers(AppRepairPaths paths)
        {
            try
            {
                string parent = Path.GetDirectoryName(paths.WebView2UserDataFolder) ?? "";
                string baseName = Path.GetFileName(paths.WebView2UserDataFolder);
                if (!Directory.Exists(parent)) return 0;
                return Directory.GetDirectories(parent, $"{baseName}.corrupt-*").Length;
            }
            catch
            {
                return 0;
            }
        }

        public async Task<bool> InstallWebView2RuntimeAsync(CancellationToken ct = default)
        {
            string tempFile = Path.Combine(SafeTempPathHelper.GetSafeTempPath(), $"MicrosoftEdgeWebview2Setup_{Guid.NewGuid():N}.exe");
            try
            {
                using var response = await HttpClientProvider.Client
                    .GetAsync(WebView2BootstrapperUrl, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                string finalHost = response.RequestMessage?.RequestUri?.Host ?? "";
                if (!finalHost.EndsWith(".microsoft.com", StringComparison.OrdinalIgnoreCase))
                {
                    _logger?.Log($"[AppRepair] WebView2 bootstrapper redirected to untrusted host '{finalHost}' — aborting");
                    return false;
                }

                byte[] bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                if (bytes.Length < 100 * 1024 || bytes[0] != (byte)'M' || bytes[1] != (byte)'Z')
                {
                    _logger?.Log($"[AppRepair] WebView2 bootstrapper failed the size/header sanity check ({bytes.Length} bytes)");
                    return false;
                }

                await File.WriteAllBytesAsync(tempFile, bytes, ct).ConfigureAwait(false);

                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = tempFile,
                    Arguments = "/silent /install",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });

                if (process != null)
                    await process.WaitForExitAsync(ct).ConfigureAwait(false);

                return WebView2Runtime.Detect().IsInstalled;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.Log($"[AppRepair] WebView2 install failed: {ex.Message}");
                return false;
            }
            finally
            {
                try { if (File.Exists(tempFile)) File.Delete(tempFile); }
                catch {  }
            }
        }

        public static bool ApplyPendingRepairs() =>
            ApplyPendingRepairs(AppRepairPaths.Default, WebView2EnvironmentHelper.TryResetUserDataFolder);

        internal static bool ApplyPendingRepairs(AppRepairPaths paths, Func<bool> resetUserDataFolder)
        {
            try
            {
                string sentinel = Path.Combine(paths.LocalAppDataDir, SentinelFileName);
                if (!File.Exists(sentinel))
                    return false;

                bool reset = resetUserDataFolder();
                if (reset)
                {
                    try { File.Delete(sentinel); } catch {  }
                }
                else
                {
                    StartupLog.Append("[AppRepair] WebView2 profile reset failed (folder still in use) — sentinel kept for next launch");
                }
                return reset;
            }
            catch (Exception ex)
            {
                StartupLog.Append($"[AppRepair] ApplyPendingRepairs failed: {ex.Message}");
                return false;
            }
        }


        private static bool TryProbeWrite(string dir, out string? diagnostic)
        {
            diagnostic = null;
            try
            {
                Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, $".amt-write-probe-{Guid.NewGuid():N}");
                File.WriteAllText(probe, "probe");
                File.Delete(probe);
                return true;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                diagnostic = $"{ex.GetType().Name}: {ex.Message}";
                return false;
            }
        }
    }
}
