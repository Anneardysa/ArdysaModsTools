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
            var modStamp = ProtectedVpkStore.GetActiveModVpkStamp(root);

            if (modStamp == null || !File.Exists(modVpk))
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

            var modStamp = ProtectedVpkStore.GetActiveModVpkStamp(root);
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

                    var diff = await TryDiffAsync(root, vanillaCopy, workDir, ct).ConfigureAwait(false);
                    string diag = $"vanilla items_game.txt is {currentSha}, package was built from {baseline.VanillaItemsGameSha}"
                                + (diff.HasValue ? $"; +{diff.Value.Added} / -{diff.Value.Removed} / ~{diff.Value.Changed} ids" : "");
                    _logger?.Log($"[SYNC] Package is stale: {diag}");

                    return diff is { Added: > 0 }
                        ? Stale("verify.sync.failLegacy", new { added = diff.Value.Added }, diag)
                        : Stale("verify.sync.fail", null, diag);
                }

                var legacy = await TryDiffAsync(root, vanillaCopy, workDir, ct).ConfigureAwait(false);
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
            string root, string vanillaCopy, string workDir, CancellationToken ct)
        {
            string modCopy = Path.Combine(workDir, "mod_items_game.txt");
            if (!await _extractor.ExtractModItemsGameAsync(root, modCopy, null, ct).ConfigureAwait(false))
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

        public async Task<SyncDetailsReport> GetSyncDetailsReportAsync(string? targetPath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(targetPath) || !Directory.Exists(targetPath))
            {
                return new SyncDetailsReport
                {
                    IsStale = false,
                    Summary = "No Dota 2 directory selected",
                    Items = Array.Empty<SyncItemDetail>()
                };
            }

            string root = PathUtility.NormalizeTargetPath(targetPath);
            string gameVpk = Path.Combine(root, ToNative(DotaPaths.GameVpk));
            string modVpk = Path.Combine(root, ToNative(DotaPaths.ModsVpk));

            if (!File.Exists(gameVpk))
            {
                return new SyncDetailsReport
                {
                    IsStale = false,
                    Summary = "Game package pak01_dir.vpk not found",
                    Items = Array.Empty<SyncItemDetail>()
                };
            }

            if (!File.Exists(modVpk))
            {
                return new SyncDetailsReport
                {
                    IsStale = false,
                    Summary = "Mod package not installed",
                    Items = Array.Empty<SyncItemDetail>()
                };
            }

            string workDir = Path.Combine(SafeTempPathHelper.GetSafeTempPath(), $"ArdysaSyncReport_{Guid.NewGuid():N}");
            try
            {
                Directory.CreateDirectory(workDir);
                string vanillaCopy = Path.Combine(workDir, "vanilla_items_game.txt");
                string modCopy = Path.Combine(workDir, "mod_items_game.txt");

                bool gotVanilla = await _extractor.ExtractItemsGameAsync(gameVpk, vanillaCopy, null, ct).ConfigureAwait(false);
                bool gotMod = await _extractor.ExtractModItemsGameAsync(root, modCopy, null, ct).ConfigureAwait(false);

                if (!gotVanilla || !gotMod)
                {
                    return new SyncDetailsReport
                    {
                        IsStale = _current.State == ItemsGameSyncState.Stale,
                        Summary = "Could not extract items_game.txt from packages",
                        Items = Array.Empty<SyncItemDetail>()
                    };
                }

                string vanillaText = await File.ReadAllTextAsync(vanillaCopy, ct).ConfigureAwait(false);
                string modText = await File.ReadAllTextAsync(modCopy, ct).ConfigureAwait(false);

                var (items, addedCount, modifiedCount, errorCount) = BuildSyncItems(vanillaText, modText);

                bool isStale = addedCount > 0 || _current.State == ItemsGameSyncState.Stale;
                string summary = $"{addedCount} new in game, {modifiedCount} modified in mods, {errorCount} errors";

                return new SyncDetailsReport
                {
                    IsStale = isStale,
                    AddedCount = addedCount,
                    ModifiedCount = modifiedCount,
                    ErrorCount = errorCount,
                    Summary = summary,
                    Items = items
                };
            }
            catch (Exception ex)
            {
                _logger?.Log($"[SYNC] Failed to build detailed sync report: {ex.Message}");
                return new SyncDetailsReport
                {
                    IsStale = _current.State == ItemsGameSyncState.Stale,
                    Summary = $"Sync report error: {ex.Message}",
                    Items = Array.Empty<SyncItemDetail>()
                };
            }
            finally
            {
                try { if (Directory.Exists(workDir)) Directory.Delete(workDir, true); } catch { }
                LargeWorkMemory.Release();
            }
        }

        private static (List<SyncItemDetail> Items, int AddedCount, int ModifiedCount, int ErrorCount) BuildSyncItems(string vanillaText, string modText)
        {
            var vanillaSpans = ItemsGameBlockIndex.IndexSpans(vanillaText);
            var modSpans = ItemsGameBlockIndex.IndexSpans(modText);

            var items = new List<SyncItemDetail>();
            int addedCount = 0;
            int modifiedCount = 0;
            int errorCount = 0;

            foreach (var (id, vRange) in vanillaSpans)
            {
                if (!modSpans.ContainsKey(id))
                {
                    addedCount++;
                    string block = vanillaText.Substring(vRange.Start, vRange.Length);
                    string name = ExtractBlockProperty(block, "name");
                    string slot = ExtractBlockProperty(block, "item_slot");

                    if (string.IsNullOrEmpty(name) && HeroDefaultItemRegistry.TryGetItem(id, out var defaultItem))
                    {
                        name = defaultItem.TechnicalName;
                    }

                    string category = DetermineCategory(id, name, slot, block);

                    items.Add(new SyncItemDetail
                    {
                        Id = id,
                        Name = string.IsNullOrEmpty(name) ? $"Item #{id}" : name,
                        Category = category,
                        Status = "new",
                        Description = "New item added in Dota 2 update (missing in mod package)"
                    });
                }
            }

            foreach (var (id, mRange) in modSpans)
            {
                if (vanillaSpans.TryGetValue(id, out var vRange))
                {
                    var mSpan = modText.AsSpan(mRange.Start, mRange.Length);
                    var vSpan = vanillaText.AsSpan(vRange.Start, vRange.Length);

                    if (!ItemsGameBlockIndex.CanonicalEquals(mSpan, vSpan))
                    {
                        modifiedCount++;
                        string mBlock = modText.Substring(mRange.Start, mRange.Length);
                        string name = ExtractBlockProperty(mBlock, "name");
                        string slot = ExtractBlockProperty(mBlock, "item_slot");

                        if (string.IsNullOrEmpty(name) && HeroDefaultItemRegistry.TryGetItem(id, out var defaultItem))
                        {
                            name = defaultItem.TechnicalName;
                        }

                        string category = DetermineCategory(id, name, slot, mBlock);

                        items.Add(new SyncItemDetail
                        {
                            Id = id,
                            Name = string.IsNullOrEmpty(name) ? $"Modified #{id}" : name,
                            Category = category,
                            Status = "modified",
                            Description = "Custom cosmetic definition applied by mods"
                        });
                    }
                }
            }

            return (items, addedCount, modifiedCount, errorCount);
        }

        private static string ExtractBlockProperty(string block, string propertyName)
        {
            var match = System.Text.RegularExpressions.Regex.Match(block, $@"""{propertyName}""\s+""([^""]*)""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : "";
        }

        private static string ExtractUsedByHero(string block)
        {
            var match = System.Text.RegularExpressions.Regex.Match(block, @"""used_by_heroes""\s*\{\s*""([^""]+)""\s+""[0-9]+""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : "";
        }

        private static string DetermineCategory(string id, string name, string slot, string block = "")
        {
            if (HeroDefaultItemRegistry.TryGetItem(id, out var heroDefault))
            {
                return string.IsNullOrEmpty(heroDefault.SlotDisplayName) || heroDefault.SlotDisplayName == "Default"
                    ? heroDefault.HeroName
                    : $"{heroDefault.HeroName} [{heroDefault.SlotDisplayName}]";
            }

            if (id == "555") return "Weather";
            if (id == "590") return "Terrain";
            if (id == "588") return "Music";
            if (id == "587") return "HUD";
            if (id == "595") return "Courier";
            if (id == "596") return "Ward";
            if (id == "11173" || id == "586") return "Announcer";
            if (id == "801" || id == "962") return "Roshan";
            if (id == "202") return "Cursor";
            if (id == "12970") return "Versus";
            if (id == "660" || id == "661") return "Creeps";
            if (id == "677" || id == "678") return "Towers";
            if (id == "34462" || id == "34463") return "Siege";

            if (!string.IsNullOrEmpty(block))
            {
                string heroId = ExtractUsedByHero(block);
                if (!string.IsNullOrEmpty(heroId))
                {
                    string heroName = HeroDefaultItemRegistry.FormatHeroName(heroId);
                    string slotDisplay = HeroDefaultItemRegistry.FormatSlotDisplayName(slot);
                    return string.IsNullOrEmpty(slotDisplay) || slotDisplay == "Default"
                        ? heroName
                        : $"{heroName} [{slotDisplay}]";
                }
            }

            if (!string.IsNullOrEmpty(slot))
            {
                return HeroDefaultItemRegistry.FormatSlotDisplayName(slot);
            }

            return "Item Definition";
        }

        private static string ToNative(string relative) => relative.Replace('/', Path.DirectorySeparatorChar);
    }
}
