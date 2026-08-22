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
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Cdn;
using ArdysaModsTools.Core.Services.Update.Models;
using ArdysaModsTools.Helpers;

namespace ArdysaModsTools.Core.Services
{
    public static class ItemsGameBaselineStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public static async Task WritePendingAsync(
            string? targetPath,
            string? vanillaVpkPath,
            string? extractedItemsGamePath,
            CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(targetPath) ||
                    string.IsNullOrWhiteSpace(extractedItemsGamePath) ||
                    !File.Exists(extractedItemsGamePath))
                    return;

                targetPath = PathUtility.NormalizeTargetPath(targetPath);

                var vanillaStamp = VpkStamp.Read(vanillaVpkPath);
                if (vanillaStamp == null) return;

                string sha = await AssetHashVerifier.ComputeSha256Async(extractedItemsGamePath, ct)
                    .ConfigureAwait(false);

                var pending = new ItemsGameBaseline
                {
                    VanillaVpk = vanillaStamp.Value,
                    VanillaItemsGameSha = sha,
                    AppVersion = SafeAppVersion(),
                    BuiltUtc = DateTime.UtcNow
                };

                await WriteAsync(Path.Combine(targetPath, DotaPaths.ItemsGameBaselinePending), pending, ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) {  }
            catch (Exception ex)
            {
                FallbackLogger.LogFileOnly($"ItemsGameBaselineStore: WritePending failed: {ex.Message}");
            }
        }

        public static async Task CommitAsync(
            string? targetPath,
            IEnumerable<string>? patchedIds,
            CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(targetPath)) return;
                targetPath = PathUtility.NormalizeTargetPath(targetPath);

                string pendingPath = Path.Combine(targetPath, DotaPaths.ItemsGameBaselinePending);
                string baselinePath = Path.Combine(targetPath, DotaPaths.ItemsGameBaseline);

                var pending = await ReadFileAsync(pendingPath, ct).ConfigureAwait(false);
                var existing = await ReadFileAsync(baselinePath, ct).ConfigureAwait(false);

                var modStamp = ReadModVpkStamp(targetPath);
                if (modStamp == null) return;

                string gameVpkPath = Path.Combine(targetPath, DotaPaths.GameVpk.Replace('/', Path.DirectorySeparatorChar));
                var vanillaStamp = VpkStamp.Read(gameVpkPath);

                ItemsGameBaseline committed;

                if (pending != null)
                {
                    var mergedIds = new HashSet<string>(pending.PatchedIds ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
                    if (patchedIds != null)
                    {
                        foreach (var id in patchedIds)
                            if (!string.IsNullOrWhiteSpace(id)) mergedIds.Add(id);
                    }

                    committed = pending with
                    {
                        ModVpk = modStamp.Value,
                        PatchedIds = mergedIds.ToArray(),
                        BuiltUtc = DateTime.UtcNow
                    };
                }
                else if (existing != null)
                {
                    var mergedIds = new HashSet<string>(existing.PatchedIds ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
                    if (patchedIds != null)
                    {
                        foreach (var id in patchedIds)
                            if (!string.IsNullOrWhiteSpace(id)) mergedIds.Add(id);
                    }

                    committed = existing with
                    {
                        ModVpk = modStamp.Value,
                        PatchedIds = mergedIds.ToArray(),
                        BuiltUtc = DateTime.UtcNow
                    };
                }
                else
                {
                    var newIds = patchedIds?.Where(id => !string.IsNullOrWhiteSpace(id)).ToArray() ?? Array.Empty<string>();
                    committed = new ItemsGameBaseline
                    {
                        VanillaVpk = vanillaStamp ?? default,
                        VanillaItemsGameSha = "",
                        ModVpk = modStamp.Value,
                        PatchedIds = newIds,
                        AppVersion = SafeAppVersion(),
                        BuiltUtc = DateTime.UtcNow
                    };
                }

                await WriteAsync(baselinePath, committed, ct).ConfigureAwait(false);
                if (pending != null) TryDelete(pendingPath);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                FallbackLogger.LogFileOnly($"ItemsGameBaselineStore: Commit failed: {ex.Message}");
            }
        }

        private static VpkStamp? ReadModVpkStamp(string targetPath)
            => ProtectedVpkStore.GetActiveModVpkStamp(targetPath);

        public static async Task RebindAsync(string? targetPath, VpkStamp? expectedPreviousStamp,
            CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(targetPath)) return;
                targetPath = PathUtility.NormalizeTargetPath(targetPath);

                string path = Path.Combine(targetPath, DotaPaths.ItemsGameBaseline);
                var existing = await ReadFileAsync(path, ct).ConfigureAwait(false);
                if (existing == null) return;

                if (expectedPreviousStamp == null || existing.ModVpk != expectedPreviousStamp.Value)
                {
                    TryDelete(path);
                    return;
                }

                var modStamp = ReadModVpkStamp(targetPath);
                if (modStamp == null) return;

                await WriteAsync(path, existing with { ModVpk = modStamp.Value }, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                FallbackLogger.LogFileOnly($"ItemsGameBaselineStore: Rebind failed: {ex.Message}");
            }
        }

        public static async Task RebindAndMergePatchedIdsAsync(
            string? targetPath,
            VpkStamp? expectedPreviousStamp,
            IEnumerable<string>? newPatchedIds,
            IEnumerable<string>? unpatchedIds = null,
            CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(targetPath)) return;
                targetPath = PathUtility.NormalizeTargetPath(targetPath);

                string path = Path.Combine(targetPath, DotaPaths.ItemsGameBaseline);
                var existing = await ReadFileAsync(path, ct).ConfigureAwait(false);

                var modStamp = ReadModVpkStamp(targetPath);
                if (modStamp == null) return;

                var mergedIds = new HashSet<string>(existing?.PatchedIds ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
                if (newPatchedIds != null)
                {
                    foreach (var id in newPatchedIds)
                    {
                        if (!string.IsNullOrWhiteSpace(id))
                            mergedIds.Add(id);
                    }
                }

                if (unpatchedIds != null)
                {
                    foreach (var id in unpatchedIds)
                    {
                        if (!string.IsNullOrWhiteSpace(id))
                            mergedIds.Remove(id);
                    }
                }

                if (existing != null && (expectedPreviousStamp == null || existing.ModVpk != expectedPreviousStamp.Value))
                {
                    TryDelete(path);
                    return;
                }

                if (existing != null)
                {
                    await WriteAsync(path, existing with { ModVpk = modStamp.Value, PatchedIds = mergedIds.ToArray() }, ct).ConfigureAwait(false);
                }
                else
                {
                    string gameVpk = Path.Combine(targetPath, DotaPaths.GameVpk);
                    var vanillaStamp = File.Exists(gameVpk) ? (VpkStamp.Read(gameVpk) ?? default) : default;
                    var newRecord = new ItemsGameBaseline
                    {
                        BuiltUtc = DateTime.UtcNow,
                        VanillaVpk = vanillaStamp,
                        ModVpk = modStamp.Value,
                        VanillaItemsGameSha = string.Empty,
                        PatchedIds = mergedIds.ToArray(),
                        AppVersion = "1.0"
                    };
                    await WriteAsync(path, newRecord, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                FallbackLogger.LogFileOnly($"ItemsGameBaselineStore: RebindAndMergePatchedIds failed: {ex.Message}");
            }
        }

        public static Task<ItemsGameBaseline?> ReadAsync(string? targetPath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
                return Task.FromResult<ItemsGameBaseline?>(null);

            return ReadFileAsync(
                Path.Combine(PathUtility.NormalizeTargetPath(targetPath), DotaPaths.ItemsGameBaseline), ct);
        }

        public static async Task RestampVanillaAsync(string? targetPath, VpkStamp current, CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(targetPath)) return;
                targetPath = PathUtility.NormalizeTargetPath(targetPath);

                string path = Path.Combine(targetPath, DotaPaths.ItemsGameBaseline);
                var existing = await ReadFileAsync(path, ct).ConfigureAwait(false);
                if (existing == null) return;

                await WriteAsync(path, existing with { VanillaVpk = current }, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                FallbackLogger.LogFileOnly($"ItemsGameBaselineStore: Restamp failed: {ex.Message}");
            }
        }

        private static async Task<ItemsGameBaseline?> ReadFileAsync(string path, CancellationToken ct)
        {
            try
            {
                if (!File.Exists(path)) return null;

                string json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
                var parsed = JsonSerializer.Deserialize<ItemsGameBaseline>(json);

                if (parsed == null || string.IsNullOrWhiteSpace(parsed.VanillaItemsGameSha))
                    return null;

                return parsed;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                FallbackLogger.LogFileOnly($"ItemsGameBaselineStore: could not read {Path.GetFileName(path)}: {ex.Message}");
                return null;
            }
        }

        private static async Task WriteAsync(string path, ItemsGameBaseline record, CancellationToken ct)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(record, JsonOptions), ct)
                .ConfigureAwait(false);
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch {  }
        }

        private static string SafeAppVersion()
        {
            try { return AppVersion.Current.Version; } catch { return ""; }
        }
    }
}
