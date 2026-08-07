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
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Cdn;
using ArdysaModsTools.Helpers;

namespace ArdysaModsTools.Core.Services
{
    public sealed class ItemsGameSyncService : IItemsGameSyncService
    {
        private readonly IGameItemsGameExtractor _extractor;
        private readonly IAppLogger? _logger;

        private readonly SemaphoreSlim _gate = new(1, 1);

        private volatile ItemsGameSyncVerdict _current = ItemsGameSyncVerdict.Cold;

        private VpkStamp? _evaluatedVanilla;
        private VpkStamp? _evaluatedMod;
        private string? _evaluatedPath;

        public ItemsGameSyncService(IGameItemsGameExtractor extractor, IAppLogger? logger = null)
        {
            _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
            _logger = logger;
        }

        public ItemsGameSyncVerdict Current => _current;

        public event Action<ItemsGameSyncVerdict>? Changed;

        public async Task<ItemsGameSyncVerdict> RefreshAsync(string? targetPath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
                return Publish(Unknown("verify.sync.unknown", "no Dota 2 path set"));

            string root = PathUtility.NormalizeTargetPath(targetPath);
            string gameVpk = Path.Combine(root, ToNative(DotaPaths.GameVpk));
            string modVpk = Path.Combine(root, ToNative(DotaPaths.ModsVpk));

            var vanillaStamp = VpkStamp.Read(gameVpk);
            var modStamp = VpkStamp.Read(modVpk);

            if (modStamp == null)
            {
                return PublishTransient(Unknown("verify.sync.unknown", "mod package not installed"));
            }

            if (vanillaStamp == null)
                return PublishTransient(Unknown("verify.sync.unknown", $"game package unreadable at {gameVpk}"));

            if (_evaluatedVanilla == vanillaStamp && _evaluatedMod == modStamp &&
                string.Equals(_evaluatedPath, root, StringComparison.OrdinalIgnoreCase))
                return _current;

            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_evaluatedVanilla == vanillaStamp && _evaluatedMod == modStamp &&
                    string.Equals(_evaluatedPath, root, StringComparison.OrdinalIgnoreCase))
                    return _current;

                var verdict = await EvaluateAsync(root, gameVpk, modVpk, vanillaStamp.Value, ct)
                    .ConfigureAwait(false);

                _evaluatedVanilla = vanillaStamp;
                _evaluatedMod = modStamp;
                _evaluatedPath = root;

                return Publish(verdict);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.Log($"[SYNC] Package sync check failed: {ex.Message}");
                return Publish(Unknown("verify.sync.unknown", ex.Message));
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task<ItemsGameSyncVerdict> EvaluateAsync(
            string root, string gameVpk, string modVpk, VpkStamp vanillaStamp, CancellationToken ct)
        {
            var baseline = await ItemsGameBaselineStore.ReadAsync(root, ct).ConfigureAwait(false);

            var modStamp = VpkStamp.Read(modVpk);
            bool recordApplies = baseline != null && modStamp != null && baseline.ModVpk == modStamp.Value;

            if (recordApplies && baseline!.VanillaVpk == vanillaStamp)
                return InSync();

            string workDir = Path.Combine(SafeTempPathHelper.GetSafeTempPath(), $"ArdysaSync_{Guid.NewGuid():N}");

            try
            {
                Directory.CreateDirectory(workDir);

                string vanillaCopy = Path.Combine(workDir, "vanilla_items_game.txt");
                if (!await _extractor.ExtractItemsGameAsync(gameVpk, vanillaCopy, null, ct).ConfigureAwait(false))
                    return Unknown("verify.sync.unknown", "could not read item data from the game package");

                ct.ThrowIfCancellationRequested();

                if (recordApplies)
                {
                    string currentSha = await AssetHashVerifier.ComputeSha256Async(vanillaCopy, ct).ConfigureAwait(false);

                    if (string.Equals(currentSha, baseline!.VanillaItemsGameSha, StringComparison.OrdinalIgnoreCase))
                    {
                        await ItemsGameBaselineStore.RestampVanillaAsync(root, vanillaStamp, ct).ConfigureAwait(false);
                        _logger?.LogDebug("[SYNC] Game package changed but its item data is unchanged — restamped.");
                        return InSync();
                    }

                    var diff = await TryDiffAsync(modVpk, vanillaCopy, workDir, ct).ConfigureAwait(false);
                    string diag = $"vanilla items_game.txt is {currentSha}, package was built from {baseline.VanillaItemsGameSha}"
                                + (diff.HasValue ? $"; +{diff.Value.Added} / -{diff.Value.Removed} / ~{diff.Value.Changed} ids" : "");
                    _logger?.Log($"[SYNC] Package is stale: {diag}");

                    return diff is { Added: > 0 }
                        ? Stale("verify.sync.failLegacy", new { added = diff.Value.Added }, diag)
                        : Stale("verify.sync.fail", null, diag);
                }

                var legacy = await TryDiffAsync(modVpk, vanillaCopy, workDir, ct).ConfigureAwait(false);
                if (legacy == null)
                    return Unknown("verify.sync.noPackage", "mod package carries no item data");

                var d = legacy.Value;

                if (d.HasIdDelta)
                {
                    string diag = $"no build record; +{d.Added} / -{d.Removed} ids"
                                + (d.AddedIds.Count > 0 ? $" (e.g. {string.Join(", ", d.AddedIds)})" : "");
                    _logger?.Log($"[SYNC] Package is stale: {diag}");

                    return d.Added > 0
                        ? Stale("verify.sync.failLegacy", new { added = d.Added }, diag)
                        : Stale("verify.sync.fail", null, diag);
                }

                if (d.Changed > 0)
                {
                    return Unknown("verify.sync.unknownLegacy",
                        $"no build record; {d.Changed} blocks differ, indistinguishable from mod edits");
                }

                return InSync();
            }
            finally
            {
                try { if (Directory.Exists(workDir)) Directory.Delete(workDir, true); } catch {  }

                LargeWorkMemory.Release();
            }
        }

        private async Task<ItemsGameBlockIndex.IdDiff?> TryDiffAsync(
            string modVpk, string vanillaCopy, string workDir, CancellationToken ct)
        {
            string modCopy = Path.Combine(workDir, "mod_items_game.txt");
            if (!await _extractor.ExtractItemsGameAsync(modVpk, modCopy, null, ct).ConfigureAwait(false))
                return null;

            ct.ThrowIfCancellationRequested();

            var moddedIndex = await BuildIndexAsync(modCopy, ct).ConfigureAwait(false);
            var vanillaIndex = await BuildIndexAsync(vanillaCopy, ct).ConfigureAwait(false);

            return ItemsGameBlockIndex.Compare(vanillaIndex, moddedIndex);
        }

        private static async Task<Dictionary<string, string>> BuildIndexAsync(string path, CancellationToken ct)
        {
            string raw = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            var index = ItemsGameBlockIndex.Build(raw);
            return index;
        }

        private ItemsGameSyncVerdict PublishTransient(ItemsGameSyncVerdict verdict)
        {
            _evaluatedVanilla = null;
            _evaluatedMod = null;
            _evaluatedPath = null;
            return Publish(verdict);
        }

        private ItemsGameSyncVerdict Publish(ItemsGameSyncVerdict verdict)
        {
            var previous = _current;
            _current = verdict;

            if (previous.State != verdict.State)
            {
                try { Changed?.Invoke(verdict); }
                catch (Exception ex) { _logger?.Log($"[SYNC] Changed handler threw: {ex.Message}"); }
            }

            return verdict;
        }

        private static ItemsGameSyncVerdict InSync() => new()
        {
            State = ItemsGameSyncState.InSync,
            DetailKey = "verify.sync.pass"
        };

        private static ItemsGameSyncVerdict Stale(string detailKey, object? vars, string diagnostic) => new()
        {
            State = ItemsGameSyncState.Stale,
            DetailKey = detailKey,
            DetailVars = vars,
            Diagnostic = diagnostic
        };

        private static ItemsGameSyncVerdict Unknown(string detailKey, string diagnostic) => new()
        {
            State = ItemsGameSyncState.Unknown,
            DetailKey = detailKey,
            Diagnostic = diagnostic
        };

        private static string ToNative(string relative) => relative.Replace('/', Path.DirectorySeparatorChar);
    }
}
