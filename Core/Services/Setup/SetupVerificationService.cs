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
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using Microsoft.Win32;

namespace ArdysaModsTools.Core.Services
{
    public sealed class SetupVerificationService : ISetupVerificationService
    {
        private readonly IAppLogger? _logger;

        private readonly IItemsGameSyncService? _itemsGameSync;

        private const string LayersKey = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";

        private const string RunAsAdminToken = "RUNASADMIN";

        private const string GameInfoSignatureMarker = "gameinfo_branchspecific.gi~SHA1:";

        public SetupVerificationService(IAppLogger? logger = null, IItemsGameSyncService? itemsGameSync = null)
        {
            _logger = logger;
            _itemsGameSync = itemsGameSync;
        }

        public async Task<SetupVerificationResult> VerifyAsync(string? targetPath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
                return SetupVerificationResult.Empty;

            var checks = new List<SetupCheck>
            {
                await CheckSignatureMatchesGameInfoAsync(targetPath, ct).ConfigureAwait(false),
                await CheckSearchPathsMountedAsync(targetPath, ct).ConfigureAwait(false),
                CheckNotForcedToRunAsAdmin(targetPath),
                CheckItemsGameInSync()
            };

            var result = new SetupVerificationResult { Checks = checks };

            foreach (var failed in checks.Where(c => c.State == SetupCheckState.Fail))
                _logger?.LogDebug($"[VERIFY] {failed.Id} FAILED: {failed.Diagnostic}");

            return result;
        }

        #region Check 1 — signature ↔ gameinfo

        private async Task<SetupCheck> CheckSignatureMatchesGameInfoAsync(string targetPath, CancellationToken ct)
        {
            const SetupCheckId id = SetupCheckId.SignatureMatchesGameInfo;

            try
            {
                string signaturesPath = Path.Combine(targetPath, DotaPaths.Signatures);
                string gameInfoPath = Path.Combine(targetPath, DotaPaths.GameInfo);

                if (!File.Exists(signaturesPath) || !File.Exists(gameInfoPath))
                    return Unknown(id, "verify.signature.unknown", "signatures or gameinfo missing");

                string signatures = await ReadTextSharedAsync(signaturesPath, ct).ConfigureAwait(false);
                string? recorded = ExtractRecordedGameInfoSha1(signatures);

                if (recorded == null)
                {
                    return Unknown(id, "verify.signature.unknown", "no gameinfo SHA1 recorded in dota.signatures");
                }

                string actual = await ComputeSha1Async(gameInfoPath, ct).ConfigureAwait(false);

                if (string.Equals(recorded, actual, StringComparison.OrdinalIgnoreCase))
                    return Pass(id, "verify.signature.pass");

                return new SetupCheck
                {
                    Id = id,
                    State = SetupCheckState.Fail,
                    DetailKey = "verify.signature.fail",
                    Diagnostic = $"dota.signatures records {recorded}, gameinfo_branchspecific.gi is {actual}",
                    FailStatus = ModStatus.NeedUpdate
                };
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return Unknown(id, "verify.signature.unknown", ex.Message);
            }
        }

        internal static string? ExtractRecordedGameInfoSha1(string? signaturesText)
        {
            if (string.IsNullOrEmpty(signaturesText))
                return null;

            int digest = signaturesText.LastIndexOf("DIGEST:", StringComparison.Ordinal);
            if (digest >= 0)
            {
                string? appended = LastHashAfter(signaturesText, digest);
                if (appended != null)
                    return appended;
            }

            return LastHashAfter(signaturesText, 0);
        }

        private static string? LastHashAfter(string text, int from)
        {
            int marker = text.LastIndexOf(GameInfoSignatureMarker, StringComparison.OrdinalIgnoreCase);

            while (marker >= from)
            {
                int start = marker + GameInfoSignatureMarker.Length;
                if (start + 40 <= text.Length)
                {
                    string candidate = text.Substring(start, 40);
                    if (candidate.All(Uri.IsHexDigit))
                        return candidate;
                }

                if (marker == 0)
                    break;

                marker = text.LastIndexOf(GameInfoSignatureMarker, marker - 1, StringComparison.OrdinalIgnoreCase);
            }

            return null;
        }

        #endregion

        #region Check 2 — search paths

        private async Task<SetupCheck> CheckSearchPathsMountedAsync(string targetPath, CancellationToken ct)
        {
            const SetupCheckId id = SetupCheckId.SearchPathsMounted;

            try
            {
                string gameInfoPath = Path.Combine(targetPath, DotaPaths.GameInfo);
                if (!File.Exists(gameInfoPath))
                    return Unknown(id, "verify.paths.unknown", "gameinfo_branchspecific.gi missing");

                string text = await ReadTextSharedAsync(gameInfoPath, ct).ConfigureAwait(false);

                bool main = ProtectedVpkStore.MountsSearchPath(text, "_ArdysaMods");
                bool protectedPath = ProtectedVpkStore.IsMountedBy(text);

                if (main && protectedPath)
                    return Pass(id, "verify.paths.pass");

                var missing = new List<string>();
                if (!main) missing.Add("main");
                if (!protectedPath) missing.Add("protected");

                return new SetupCheck
                {
                    Id = id,
                    State = SetupCheckState.Fail,
                    DetailKey = "verify.paths.fail",
                    Diagnostic = $"gameinfo_branchspecific.gi does not mount: {string.Join(", ", missing)}",
                    FailStatus = ModStatus.NeedUpdate
                };
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return Unknown(id, "verify.paths.unknown", ex.Message);
            }
        }

        #endregion

        #region Check 3 — forced elevation

        private SetupCheck CheckNotForcedToRunAsAdmin(string targetPath)
        {
            const SetupCheckId id = SetupCheckId.NotForcedToRunAsAdmin;

            var diagnostics = new List<string>();
            var elevatedApps = new List<string>();
            bool canAutoFix = false;
            bool flagProbeRan = false;
            bool processProbeRan = false;

            try
            {
                var targets = GetElevationTargets(targetPath);
                var flagged = FindForcedAdminEntries(targets);
                flagProbeRan = true;

                if (flagged.Count > 0)
                {
                    canAutoFix = flagged.All(e => e.PerUser);
                    elevatedApps.AddRange(flagged.Select(e => Path.GetFileName(e.ExePath)));
                    diagnostics.AddRange(flagged.Select(e =>
                        $"{(e.PerUser ? "HKCU" : "HKLM")}\\…\\Layers [{e.ExePath}] = {e.Data}"));
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add($"compatibility-flag probe failed: {ex.Message}");
            }

            try
            {
                foreach (var (process, pid) in FindElevatedGameProcesses())
                {
                    elevatedApps.Add(process + ".exe");
                    diagnostics.Add($"{process}.exe (pid {pid}) is running elevated");
                }
                processProbeRan = true;
            }
            catch (Exception ex)
            {
                diagnostics.Add($"running-process probe failed: {ex.Message}");
            }

            var apps = elevatedApps.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var state = apps.Count > 0
                ? SetupCheckState.Advisory
                : (flagProbeRan || processProbeRan) ? SetupCheckState.Pass : SetupCheckState.Unknown;

            return new SetupCheck
            {
                Id = id,
                State = state,
                DetailKey = state switch
                {
                    SetupCheckState.Advisory => "verify.admin.detected",
                    SetupCheckState.Pass => "verify.admin.clean",
                    _ => "verify.admin.unknown"
                },
                DetailVars = apps.Count > 0 ? new { apps = string.Join(", ", apps) } : null,
                Diagnostic = diagnostics.Count > 0 ? string.Join("; ", diagnostics) : null,
                CanAutoFix = canAutoFix,
                HasOwnDialog = true
            };
        }

        private static IEnumerable<(string process, int pid)> FindElevatedGameProcesses()
        {
            foreach (var name in new[] { "steam", "dota2" })
            {
                Process[] running;
                try { running = Process.GetProcessesByName(name); }
                catch { continue; }

                foreach (var proc in running)
                {
                    try
                    {
                        if (IsProcessElevated(proc.Id))
                            yield return (name, proc.Id);
                    }
                    finally
                    {
                        proc.Dispose();
                    }
                }
            }
        }

        private static bool IsProcessElevated(int pid)
        {
            IntPtr process = NativeMethods.OpenProcess(
                NativeMethods.ProcessQueryLimitedInformation, false, pid);
            if (process == IntPtr.Zero)
                return false;

            try
            {
                if (!NativeMethods.OpenProcessToken(process, NativeMethods.TokenQuery, out IntPtr token))
                    return false;

                try
                {
                    return NativeMethods.GetTokenInformation(
                               token, NativeMethods.TokenElevation, out uint elevated,
                               sizeof(uint), out _)
                           && elevated != 0;
                }
                finally
                {
                    NativeMethods.CloseHandle(token);
                }
            }
            finally
            {
                NativeMethods.CloseHandle(process);
            }
        }

        private static class NativeMethods
        {
            internal const uint ProcessQueryLimitedInformation = 0x1000;
            internal const uint TokenQuery = 0x0008;

            internal const int TokenElevation = 20;

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CloseHandle(IntPtr handle);

            [DllImport("advapi32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool OpenProcessToken(IntPtr process, uint desiredAccess, out IntPtr token);

            [DllImport("advapi32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool GetTokenInformation(
                IntPtr token, int informationClass, out uint information, uint length, out uint returnLength);
        }

        private readonly record struct ForcedAdminEntry(string ExePath, string Data, bool PerUser);

        private static List<string> GetElevationTargets(string targetPath)
        {
            var targets = new List<string>();

            string dota = Path.Combine(targetPath, DotaPaths.Dota2Exe.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(dota))
                targets.Add(dota);

            if (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamExe", null) is string steamExe
                && !string.IsNullOrWhiteSpace(steamExe)
                && File.Exists(steamExe))
            {
                targets.Add(steamExe);
            }

            return targets;
        }

        private static List<ForcedAdminEntry> FindForcedAdminEntries(IReadOnlyCollection<string> targets)
        {
            var normalizedTargets = targets
                .Select(NormalizePath)
                .Where(p => p.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var found = new List<ForcedAdminEntry>();

            foreach (var (hive, perUser) in new[] { (Registry.CurrentUser, true), (Registry.LocalMachine, false) })
            {
                try
                {
                    using var key = hive.OpenSubKey(LayersKey, writable: false);
                    if (key == null)
                        continue;

                    foreach (var valueName in key.GetValueNames())
                    {
                        if (!normalizedTargets.Contains(NormalizePath(valueName)))
                            continue;

                        string data = key.GetValue(valueName)?.ToString() ?? string.Empty;
                        if (HasRunAsAdmin(data))
                            found.Add(new ForcedAdminEntry(valueName, data, perUser));
                    }
                }
                catch
                {
                }
            }

            return found;
        }

        internal static bool HasRunAsAdmin(string? layerData) =>
            !string.IsNullOrEmpty(layerData)
            && layerData.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(t => string.Equals(t, RunAsAdminToken, StringComparison.OrdinalIgnoreCase));

        internal static string? RemoveRunAsAdmin(string? layerData)
        {
            if (string.IsNullOrEmpty(layerData))
                return null;

            var kept = layerData
                .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => !string.Equals(t, RunAsAdminToken, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (kept.Count == 0 || kept.All(t => t == "~"))
                return null;

            return string.Join(" ", kept);
        }

        private static string NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            try
            {
                return Path.GetFullPath(path.Trim().Trim('"')).TrimEnd(Path.DirectorySeparatorChar);
            }
            catch
            {
                return path.Trim();
            }
        }

        #endregion

        #region Check 4 — package ↔ game item data

        private SetupCheck CheckItemsGameInSync()
        {
            const SetupCheckId id = SetupCheckId.ItemsGameInSync;

            var verdict = _itemsGameSync?.Current;
            if (verdict == null)
                return Unknown(id, "verify.sync.unknown", "package sync service not available");

            return verdict.State switch
            {
                ItemsGameSyncState.InSync => new SetupCheck
                {
                    Id = id,
                    State = SetupCheckState.Pass,
                    DetailKey = verdict.DetailKey,
                    HasOwnDialog = true
                },

                ItemsGameSyncState.Stale => new SetupCheck
                {
                    Id = id,
                    State = SetupCheckState.Fail,
                    DetailKey = verdict.DetailKey,
                    DetailVars = verdict.DetailVars,
                    Diagnostic = verdict.Diagnostic,
                    HasOwnDialog = true,
                    FailStatus = ModStatus.NeedUpdate,
                    FailAction = RecommendedAction.Play
                },

                _ => new SetupCheck
                {
                    Id = id,
                    State = SetupCheckState.Unknown,
                    DetailKey = verdict.DetailKey,
                    Diagnostic = verdict.Diagnostic,
                    HasOwnDialog = true
                }
            };
        }

        #endregion

        #region One-click fix

        public Task<(int cleared, string? error)> TryClearForcedAdminAsync(
            string? targetPath, CancellationToken ct = default)
        {
            return Task.Run<(int, string?)>(() =>
            {
                if (string.IsNullOrWhiteSpace(targetPath))
                    return (0, "No Dota 2 folder is attached.");

                try
                {
                    var targets = GetElevationTargets(targetPath);
                    var flagged = FindForcedAdminEntries(targets).Where(e => e.PerUser).ToList();
                    if (flagged.Count == 0)
                        return (0, null);

                    ct.ThrowIfCancellationRequested();

                    using var key = Registry.CurrentUser.OpenSubKey(LayersKey, writable: true);
                    if (key == null)
                        return (0, "The compatibility settings key could not be opened.");

                    int cleared = 0;
                    foreach (var entry in flagged)
                    {
                        string? remaining = RemoveRunAsAdmin(entry.Data);
                        if (remaining == null)
                            key.DeleteValue(entry.ExePath, throwOnMissingValue: false);
                        else
                            key.SetValue(entry.ExePath, remaining, RegistryValueKind.String);

                        cleared++;
                        _logger?.LogDebug($"[VERIFY] Cleared RUNASADMIN for {entry.ExePath} (was '{entry.Data}')");
                    }

                    return (cleared, null);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    _logger?.LogDebug($"[VERIFY] TryClearForcedAdminAsync failed: {ex.Message}");
                    return (0, ex.Message);
                }
            }, ct);
        }

        #endregion

        #region Helpers

        private static SetupCheck Pass(SetupCheckId id, string detailKey) =>
            new() { Id = id, State = SetupCheckState.Pass, DetailKey = detailKey };

        private static SetupCheck Unknown(SetupCheckId id, string detailKey, string? diagnostic) =>
            new() { Id = id, State = SetupCheckState.Unknown, DetailKey = detailKey, Diagnostic = diagnostic };

        private static async Task<string> ReadTextSharedAsync(string path, CancellationToken ct)
        {
            await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            return await reader.ReadToEndAsync(ct).ConfigureAwait(false);
        }

        private static async Task<string> ComputeSha1Async(string path, CancellationToken ct)
        {
            await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sha1 = SHA1.Create();
            return Convert.ToHexString(await sha1.ComputeHashAsync(fs, ct).ConfigureAwait(false));
        }

        #endregion
    }
}
