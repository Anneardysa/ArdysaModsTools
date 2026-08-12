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
using System.IO;
using System.Linq;
using System.Text;
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
    public sealed class ItemsGameMergeService : IItemsGameMergeService
    {
        private const string ItemsGameRelative = "scripts/items/items_game.txt";

        private readonly IGameItemsGameExtractor _itemsGameExtractor;
        private readonly IVpkExtractor _extractor;
        private readonly IVpkRecompiler _recompiler;
        private readonly IVpkReplacer _replacer;
        private readonly IAppLogger? _logger;

        public ItemsGameMergeService(
            IGameItemsGameExtractor itemsGameExtractor,
            IVpkExtractor extractor,
            IVpkRecompiler recompiler,
            IVpkReplacer replacer,
            IAppLogger? logger = null)
        {
            _itemsGameExtractor = itemsGameExtractor ?? throw new ArgumentNullException(nameof(itemsGameExtractor));
            _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
            _recompiler = recompiler ?? throw new ArgumentNullException(nameof(recompiler));
            _replacer = replacer ?? throw new ArgumentNullException(nameof(replacer));
            _logger = logger;
        }

        public async Task<ItemsGameMergeResult> MergeAsync(
            string? targetPath,
            IProgress<string>? status = null,
            IProgress<int>? percent = null,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(targetPath))
                return ItemsGameMergeResult.Fail("play.merge.failed", "no Dota 2 path set");

            string root = PathUtility.NormalizeTargetPath(targetPath);
            string gameVpk = Path.Combine(root, Native(DotaPaths.GameVpk));
            string modVpk = Path.Combine(root, Native(DotaPaths.ModsVpk));

            if (!File.Exists(modVpk))
                return new ItemsGameMergeResult { Outcome = ItemsGameMergeOutcome.NothingToMerge };

            if (!File.Exists(gameVpk))
                return ItemsGameMergeResult.Fail("play.merge.failed", $"game package missing at {gameVpk}");

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string hlExtractPath = Path.Combine(baseDir, "HLExtract.exe");
            string vpkToolPath = Path.Combine(baseDir, "vpk.exe");
            if (!File.Exists(hlExtractPath) || !File.Exists(vpkToolPath))
                return ItemsGameMergeResult.Fail("play.merge.toolsMissing",
                    $"HLExtract={File.Exists(hlExtractPath)}, vpk.exe={File.Exists(vpkToolPath)}");

            string tempRoot = Path.Combine(SafeTempPathHelper.GetSafeTempPath(), $"ArdysaMerge_{Guid.NewGuid():N}");
            string extractDir = Path.Combine(tempRoot, "extract");
            string buildDir = Path.Combine(tempRoot, "build");

            try
            {
                Directory.CreateDirectory(extractDir);
                Directory.CreateDirectory(buildDir);

                percent?.Report(0);
                status?.Report("play.merge.reading");

                string vanillaPath = Path.Combine(tempRoot, "vanilla_items_game.txt");
                if (!await _itemsGameExtractor.ExtractItemsGameAsync(gameVpk, vanillaPath, null, ct).ConfigureAwait(false))
                    return ItemsGameMergeResult.Fail("play.merge.readFailed", "could not read item data from the game package");

                ct.ThrowIfCancellationRequested();

                string vanillaSha = await AssetHashVerifier.ComputeSha256Async(vanillaPath, ct).ConfigureAwait(false);
                var record = await ItemsGameBaselineStore.ReadAsync(root, ct).ConfigureAwait(false);
                var modStamp = VpkStamp.Read(modVpk);

                bool recordApplies = record != null && modStamp != null && record.ModVpk == modStamp.Value;
                if (recordApplies &&
                    string.Equals(record!.VanillaItemsGameSha, vanillaSha, StringComparison.OrdinalIgnoreCase))
                {
                    await ItemsGameBaselineStore.RestampVanillaAsync(root, VpkStamp.Read(gameVpk) ?? default, ct)
                        .ConfigureAwait(false);
                    percent?.Report(100);
                    return new ItemsGameMergeResult { Outcome = ItemsGameMergeOutcome.AlreadyCurrent };
                }

                percent?.Report(5);
                status?.Report("play.merge.merging");

                string moddedProbe = Path.Combine(tempRoot, "mod_items_game.txt");
                if (!await _itemsGameExtractor.ExtractItemsGameAsync(modVpk, moddedProbe, null, ct).ConfigureAwait(false))
                {
                    _logger?.Log("[MERGE] Package carries no item data — nothing to merge.");
                    return new ItemsGameMergeResult { Outcome = ItemsGameMergeOutcome.NothingToMerge };
                }

                ct.ThrowIfCancellationRequested();
                percent?.Report(20);

                string vanillaText = await File.ReadAllTextAsync(vanillaPath, ct).ConfigureAwait(false);
                string moddedText = await File.ReadAllTextAsync(moddedProbe, ct).ConfigureAwait(false);

                var combinedPatchedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (recordApplies && record!.PatchedIds != null && record.PatchedIds.Count > 0)
                {
                    foreach (var id in record.PatchedIds)
                        if (!string.IsNullOrWhiteSpace(id)) combinedPatchedIds.Add(id);
                }

                var miscLog = MiscExtractionLog.Load(root);
                if (miscLog?.Selections != null && miscLog.Selections.Count > 0)
                {
                    var miscIds = AssetModifierService.ResolveItemIdsForSelections(miscLog.Selections);
                    foreach (var id in miscIds)
                        if (!string.IsNullOrWhiteSpace(id)) combinedPatchedIds.Add(id);
                }

                var heroLog = HeroExtractionLog.Load(root);
                if (!recordApplies || combinedPatchedIds.Count == 0 || (heroLog?.InstalledSets != null && heroLog.InstalledSets.Count > 0))
                {
                    var diffIds = ItemsGameBlockIndex.FindDifferingItemIds(vanillaText, moddedText);
                    foreach (var id in diffIds)
                        if (!string.IsNullOrWhiteSpace(id)) combinedPatchedIds.Add(id);
                }

                var patchedIds = combinedPatchedIds.Count > 0 ? (IReadOnlyCollection<string>)combinedPatchedIds : null;

                var merged = await Task.Run(() => ItemsGameMerger.Merge(vanillaText, moddedText, patchedIds), ct)
                    .ConfigureAwait(false);

                ct.ThrowIfCancellationRequested();
                percent?.Report(30);

                bool unchanged = await Task.Run(() => ItemsGameMerger.IsEquivalent(merged.Text, moddedText), ct)
                    .ConfigureAwait(false);
                if (unchanged)
                {
                    _logger?.Log("[MERGE] Package already matches the game's item data — nothing to rebuild.");
                    await ItemsGameBaselineStore.WritePendingAsync(root, gameVpk, vanillaPath, ct).ConfigureAwait(false);
                    await ItemsGameBaselineStore.CommitAsync(root, patchedIds, ct).ConfigureAwait(false);
                    percent?.Report(100);
                    return new ItemsGameMergeResult { Outcome = ItemsGameMergeOutcome.AlreadyCurrent };
                }

                _logger?.Log($"[MERGE] Rebuilding item data on the game's current version " +
                             $"({(patchedIds is { Count: > 0 } ? "using this package's build record" : "by comparison, no build record")}): " +
                             $"{merged.Applied} customisations kept, {merged.Dropped} dropped (items the game removed).");

                status?.Report("play.merge.unpacking");

                if (!await _extractor.ExtractAsync(hlExtractPath, modVpk, extractDir,
                        line => _logger?.LogDebug($"[MERGE] {line}"), ct, null, requireItemsGame: false)
                        .ConfigureAwait(false))
                    return ItemsGameMergeResult.Fail("play.merge.unpackFailed", $"could not unpack {modVpk}");

                ct.ThrowIfCancellationRequested();

                string moddedPath = Path.Combine(extractDir, Native(ItemsGameRelative));
                if (!File.Exists(moddedPath))
                    return ItemsGameMergeResult.Fail("play.merge.unpackFailed",
                        "item data was readable from the package but missing from its extraction");

                await File.WriteAllTextAsync(moddedPath, merged.Text, new UTF8Encoding(false), ct).ConfigureAwait(false);

                ct.ThrowIfCancellationRequested();
                percent?.Report(55);

                status?.Report("play.merge.repacking");

                string? newVpk = await _recompiler.RecompileAsync(vpkToolPath, extractDir, buildDir, tempRoot,
                    line => _logger?.LogDebug($"[MERGE] {line}"), ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(newVpk))
                    return ItemsGameMergeResult.Fail("play.merge.repackFailed", "vpk.exe produced no package");

                ct.ThrowIfCancellationRequested();
                percent?.Report(90);
                status?.Report("play.merge.installing");

                if (!await _replacer.ReplaceAsync(root, newVpk,
                        line => _logger?.Log($"[MERGE] {line}"), ct).ConfigureAwait(false))
                    return ItemsGameMergeResult.Fail("play.merge.installFailed", "package replacement failed");

                await ItemsGameBaselineStore.WritePendingAsync(root, gameVpk, vanillaPath, ct).ConfigureAwait(false);
                await ItemsGameBaselineStore.CommitAsync(root, patchedIds, ct).ConfigureAwait(false);

                percent?.Report(100);
                return new ItemsGameMergeResult
                {
                    Outcome = ItemsGameMergeOutcome.Merged,
                    Applied = merged.Applied,
                    Dropped = merged.Dropped
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.Log($"[MERGE] Repair failed: {ex.Message}");
                return ItemsGameMergeResult.Fail("play.merge.failed", ex.ToString());
            }
            finally
            {
                try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); }
                catch (Exception ex) { _logger?.LogDebug($"[MERGE] temp cleanup failed: {ex.Message}"); }

                LargeWorkMemory.Release();
            }
        }

        private static string Native(string relative) => relative.Replace('/', Path.DirectorySeparatorChar);
    }
}
