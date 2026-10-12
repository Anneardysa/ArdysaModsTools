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
using ArdysaModsTools.Core.Exceptions;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Localization;
using ArdysaModsTools.Helpers;

namespace ArdysaModsTools.Core.Services.Utilities
{
    public sealed class HeroBadgeService : IHeroBadgeService
    {
        public const string ConfigKey = "HeroBadge_State";

        private const long MinSourceBytes = 1_024;

        private static readonly TimeSpan HlExtractTimeout = TimeSpan.FromMinutes(5);

        private readonly IConfigService _config;
        private readonly IVpkExtractor _extractor;
        private readonly IVpkRecompiler _recompiler;
        private readonly IVpkReplacer _replacer;
        private readonly IAppLogger? _logger;
        private readonly Func<bool> _isGameRunning;

        public HeroBadgeService(
            IConfigService config,
            IVpkExtractor extractor,
            IVpkRecompiler recompiler,
            IVpkReplacer replacer,
            IAppLogger? logger = null,
            Func<bool>? isGameRunning = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
            _recompiler = recompiler ?? throw new ArgumentNullException(nameof(recompiler));
            _replacer = replacer ?? throw new ArgumentNullException(nameof(replacer));
            _logger = logger;
            _isGameRunning = isGameRunning ?? (() => ProcessChecker.IsProcessRunning("dota2"));
        }

        public HeroBadgeState LoadState(string? targetPath = null)
        {
            try
            {
                var json = _config.GetValue<string>(ConfigKey, string.Empty);
                if (string.IsNullOrWhiteSpace(json)) return new HeroBadgeState(false, null);

                var request = JsonSerializer.Deserialize<HeroBadgeRequest>(json);
                if (request is null) return new HeroBadgeState(false, null);

                return new HeroBadgeState(MarkerStillInPackage(targetPath), request);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"HeroBadge: could not read saved state ({ex.Message}).");
                return new HeroBadgeState(false, null);
            }
        }

        private bool MarkerStillInPackage(string? targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath)) return true;
            try
            {
                string modVpk = Path.Combine(PathUtility.NormalizeTargetPath(targetPath), Native(DotaPaths.ModsVpk));
                if (!File.Exists(modVpk)) return true;

                var entries = RankChangerService.ReadVpkEntries(modVpk);
                return entries.Count == 0 || entries.Contains(HeroBadgePlan.MarkerEntry);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"HeroBadge: could not read the package to confirm state ({ex.Message}).");
                return true;
            }
        }

        public Task<OperationResult> ApplyAsync(string targetPath, HeroBadgeRequest request,
            IProgress<RankChangerProgress>? progress = null, CancellationToken ct = default)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));

            HeroBadgeEffects fx;
            try
            {
                fx = HeroBadgePlan.Resolve(request);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                _logger?.LogDebug($"HeroBadge: rejected request — {ex.Message}");
                return Task.FromResult(OperationResult.Fail(Loc.T("badge.error.invalidSelection")));
            }
            catch (InvalidOperationException)
            {
                return Task.FromResult(OperationResult.Fail(Loc.T("badge.error.nothingSelected")));
            }

            return RunAsync(targetPath, fx, request, progress, ct);
        }

        public Task<OperationResult> ResetAsync(string targetPath,
            IProgress<RankChangerProgress>? progress = null, CancellationToken ct = default)
            => RunAsync(targetPath, null, null, progress, ct);

        private async Task<OperationResult> RunAsync(string targetPath, HeroBadgeEffects? fx,
            HeroBadgeRequest? request, IProgress<RankChangerProgress>? progress, CancellationToken ct)
        {
            OperationResult Failed(string reason, string? code = null, object? vars = null, string? detail = null)
            {
                if (detail != null) _logger?.LogError($"HeroBadge: {detail}");
                return new OperationResult
                {
                    Success = false,
                    Message = FailureMessage(fx is null, reason, vars),
                    ErrorCode = code
                };
            }

            if (string.IsNullOrWhiteSpace(targetPath) || !Directory.Exists(targetPath))
                return Failed("noPath");

            if (_isGameRunning())
            {
                return Failed("dotaRunning", detail: "refused to run while Dota 2 is running.");
            }

            string dotaRoot = PathUtility.NormalizeTargetPath(targetPath);
            string gameVpk = Path.Combine(dotaRoot, Native(DotaPaths.GameVpk));
            string modVpk = Path.Combine(dotaRoot, Native(DotaPaths.ModsVpk));

            if (!File.Exists(gameVpk))
                return Failed("gameFiles", ErrorCodes.VPK_FILE_NOT_FOUND, detail: $"game package missing at {gameVpk}");
            if (!File.Exists(modVpk))
                return Failed("notInstalled", ErrorCodes.VPK_FILE_NOT_FOUND, detail: $"main mod package missing at {modVpk}");

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string hlExtract = Path.Combine(baseDir, "HLExtract.exe");
            string vpkTool = Path.Combine(baseDir, "vpk.exe");
            if (!File.Exists(hlExtract) || !File.Exists(vpkTool))
                return Failed("tools", ErrorCodes.VPK_TOOL_NOT_FOUND,
                    detail: $"tools missing (HLExtract={File.Exists(hlExtract)}, vpk.exe={File.Exists(vpkTool)})");

            string tempRoot = SafeTempPathHelper.CreateSafeTempDirectory("ArdysaBadge");
            string extractDir = Path.Combine(tempRoot, "extract");
            string buildDir = Path.Combine(tempRoot, "build");

            try
            {
                Report(progress, RankChangerStage.Start, 5);
                Directory.CreateDirectory(extractDir);
                Directory.CreateDirectory(buildDir);

                IReadOnlyDictionary<string, string> sources = new Dictionary<string, string>();
                var patchedSheets = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                if (fx != null)
                {
                    Report(progress, RankChangerStage.Processing, 15);
                    var sheets = HeroBadgeCssPatcher.StylesheetsFor(fx);
                    var needed = (fx.Tier is int tier ? HeroBadgePlan.Copies(tier, fx.Scope).Select(c => c.Source) : Enumerable.Empty<string>())
                        .Concat(sheets)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    var extracted = await ExtractFromGameAsync(hlExtract, gameVpk, needed, Path.Combine(tempRoot, "game"), ct)
                        .ConfigureAwait(false);
                    if (extracted is null)
                        return Failed("readFailed", ErrorCodes.VPK_EXTRACT_FAILED, detail: "Valve's badge files did not come out of the game package.");
                    sources = extracted;

                    try
                    {
                        foreach (var sheet in sheets)
                        {
                            byte[] original = await File.ReadAllBytesAsync(sources[sheet], ct).ConfigureAwait(false);
                            byte[]? patched = HeroBadgeCssPatcher.Patch(sheet, original, fx);
                            if (patched != null) patchedSheets[sheet] = patched;
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        return Failed("gameUpdated", detail: $"CSS patching failed ({ex.Message})");
                    }
                }

                ct.ThrowIfCancellationRequested();

                Report(progress, RankChangerStage.Processing, 35);
                if (!await _extractor.ExtractAsync(hlExtract, modVpk, extractDir,
                        line => _logger?.LogDebug($"[HeroBadge] {line}"), ct, null, requireItemsGame: false)
                        .ConfigureAwait(false))
                {
                    return Failed("readFailed", ErrorCodes.VPK_EXTRACT_FAILED, detail: "the main mod package could not be unpacked.");
                }

                if (await GameItemsGameExtractorService.RepairLineEndingsInPlaceAsync(
                        Path.Combine(extractDir, "scripts", "items", "items_game.txt"), ct).ConfigureAwait(false))
                    _logger?.Log("HeroBadge: the package's item data had CRLF line endings, which the game rejects; rewrote it to LF.");

                ct.ThrowIfCancellationRequested();

                Report(progress, RankChangerStage.Processing, 55);
                if (!File.Exists(Path.Combine(extractDir, Native(HeroBadgePlan.MarkerEntry))))
                {
                    if (fx is null)
                    {
                        SaveState(null);
                        Report(progress, RankChangerStage.Finish, 100);
                        return OperationResult.Ok(0);
                    }

                    var foreign = ForeignFiles(extractDir);
                    if (foreign.Count > 0)
                        return Failed("conflict", vars: new { file = foreign[0] },
                            detail: $"package already carries hero badge files it did not write: {string.Join(", ", foreign)}");
                }

                var expected = await StageAsync(extractDir, sources, fx, patchedSheets, dotaRoot, _logger, ct)
                    .ConfigureAwait(false);
                if (expected is null)
                    return Failed("sourceMissing", ErrorCodes.VPK_EXTRACT_FAILED, detail: "a copy source was missing or too small.");

                ct.ThrowIfCancellationRequested();

                Report(progress, RankChangerStage.Compiling, 70);
                string? newVpk = await _recompiler.RecompileAsync(vpkTool, extractDir, buildDir, tempRoot,
                    line => _logger?.LogDebug($"[HeroBadge] {line}"), ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(newVpk) || !File.Exists(newVpk))
                    return Failed("rebuild", ErrorCodes.VPK_RECOMPILE_FAILED, detail: "vpk.exe produced no package.");

                if (!RankChangerService.EnsurePanoramaProtectedInVpk(newVpk, expected, out var gateError))
                    return Failed("rebuild", ErrorCodes.VPK_RECOMPILE_FAILED, detail: $"package gate failed: {gateError}");

                ct.ThrowIfCancellationRequested();
                Report(progress, RankChangerStage.Compiling, 90);

                RankChangerService.SweepLoosePanorama(dotaRoot, _logger);
                if (!await _replacer.ReplaceAsync(dotaRoot, newVpk,
                        line => _logger?.Log($"[HeroBadge] {line}"), ct).ConfigureAwait(false))
                {
                    return Failed("install", detail: "the rebuilt package could not be swapped in.");
                }
                RankChangerService.SweepLoosePanorama(dotaRoot, _logger);

                SaveState(request);
                Report(progress, RankChangerStage.Finish, 100);
                return OperationResult.Ok(expected.Count);
            }
            catch (OperationCanceledException)
            {
                return OperationResult.Canceled();
            }
            catch (Exception ex)
            {
                _logger?.LogError("HeroBadge: run failed", ex);
                return Failed("unknown");
            }
            finally
            {
                if (!SafeTempPathHelper.TryDeleteWorkDir(tempRoot))
                    _logger?.LogDebug($"HeroBadge: could not fully clean up temp directory '{tempRoot}'.");
            }
        }

        internal static IReadOnlyList<string> ForeignFiles(string extractDir) =>
            HeroBadgePlan.OwnedPaths()
                .Where(p => File.Exists(Path.Combine(extractDir, Native(p))))
                .ToList();

        internal static async Task<IReadOnlyList<string>?> StageAsync(string extractDir,
            IReadOnlyDictionary<string, string> sources, HeroBadgeEffects? fx,
            IReadOnlyDictionary<string, byte[]> patchedSheets, string? dotaRoot, IAppLogger? logger, CancellationToken ct)
        {
            foreach (var owned in HeroBadgePlan.OwnedPaths())
            {
                string p = Path.Combine(extractDir, Native(owned));
                if (File.Exists(p)) File.Delete(p);
            }

            var written = new List<string>();
            if (fx is null) return written;

            async Task WriteAsync(string vpkPath, byte[] bytes)
            {
                string dest = Path.Combine(extractDir, Native(vpkPath));
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                await File.WriteAllBytesAsync(dest, bytes, ct).ConfigureAwait(false);
                written.Add(vpkPath);
            }

            if (fx.Tier is int tier)
            {
                foreach (var (source, dest) in HeroBadgePlan.Copies(tier, fx.Scope))
                {
                    ct.ThrowIfCancellationRequested();
                    if (!sources.TryGetValue(source, out var local) || !File.Exists(local) ||
                        new FileInfo(local).Length < MinSourceBytes)
                    {
                        logger?.LogDebug($"HeroBadge: source missing or too small — {source}");
                        return null;
                    }
                    await WriteAsync(dest, await File.ReadAllBytesAsync(local, ct).ConfigureAwait(false)).ConfigureAwait(false);
                }
            }

            byte[] blank = VtexGenerator.GenerateEmptyPipTexture();
            if (fx.HideBadge)
                foreach (var path in HeroBadgePlan.Blanked(fx.Scope))
                    await WriteAsync(path, blank).ConfigureAwait(false);

            if (fx.Level == HeroBadgeLevel.Custom)
            {
                await WriteAsync(HeroBadgePlan.LargeLevelTexture,
                    VtexGenerator.GenerateHeroLevelTexture(fx.CustomLevel, small: false, dotaRoot)).ConfigureAwait(false);
                await WriteAsync(HeroBadgePlan.SmallLevelTexture,
                    VtexGenerator.GenerateHeroLevelTexture(fx.CustomLevel, small: true, dotaRoot)).ConfigureAwait(false);
            }

            if (fx.ProgressPercent is int percent)
            {
                var (dark, light) = HeroBadgePlan.ProgressColors(fx.BarTier);
                await WriteAsync(HeroBadgePlan.ProgressTexture,
                    VtexGenerator.GenerateHeroProgressTexture(percent, dark, light)).ConfigureAwait(false);

                if (fx.ShowsXp)
                {
                    var (have, need) = HeroBadgePlan.Xp(fx.CustomLevel, percent);
                    await WriteAsync(HeroBadgePlan.XpTexture,
                        VtexGenerator.GenerateHeroXpTexture(have, need, HeroBadgePlan.XpColor(fx.BarTier), dotaRoot)).ConfigureAwait(false);
                }
            }

            foreach (var (sheet, bytes) in patchedSheets)
                await WriteAsync(sheet, bytes).ConfigureAwait(false);

            await WriteAsync(HeroBadgePlan.MarkerEntry, blank).ConfigureAwait(false);
            return written;
        }

        private async Task<IReadOnlyDictionary<string, string>?> ExtractFromGameAsync(string hlExtract, string gameVpk,
            IReadOnlyList<string> paths, string workDir, CancellationToken ct)
        {
            if (paths.Count == 0) return new Dictionary<string, string>();

            int attempt = 0;
            foreach (var prefix in new[] { "root/", "" })
            {
                ct.ThrowIfCancellationRequested();
                string dir = Path.Combine(workDir, (attempt++).ToString());
                Directory.CreateDirectory(dir);

                string items = string.Join(" ", paths.Select(p => $"-e \"{prefix}{p}\""));
                if (!await RankChangerService.RunToolAsync(hlExtract, $"-p \"{gameVpk}\" -d \"{dir}\" {items} -m -s",
                        HlExtractTimeout, _logger, ct).ConfigureAwait(false))
                    continue;

                var found = LocateExtracted(dir, paths);
                if (found != null) return found;
            }

            _logger?.LogDebug($"HeroBadge: could not extract {paths.Count} file(s) from the game package.");
            return null;
        }

        internal static IReadOnlyDictionary<string, string>? LocateExtracted(string dir, IReadOnlyList<string> paths)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in paths)
            {
                var hits = Directory.GetFiles(dir, Path.GetFileName(path), SearchOption.AllDirectories);
                if (hits.Length != 1) return null;
                map[path] = hits[0];
            }
            return map;
        }

        private void SaveState(HeroBadgeRequest? request)
        {
            try
            {
                _config.SetValue(ConfigKey, request is null ? string.Empty : JsonSerializer.Serialize(request));
                _config.Save();
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"HeroBadge: could not save state ({ex.Message}).");
            }
        }

        internal static string FailureMessage(bool reset, string reason, object? vars = null)
        {
            string headline = Loc.T(reset ? "badge.fail.reset" : "badge.fail.apply");
            string why = vars is null ? Loc.T($"badge.reason.{reason}") : Loc.T($"badge.reason.{reason}", vars);
            return $"{headline} {why}";
        }

        private static string Native(string relative) => relative.Replace('/', Path.DirectorySeparatorChar);

        private static void Report(IProgress<RankChangerProgress>? progress, RankChangerStage stage, int percent)
            => progress?.Report(new RankChangerProgress(stage, percent));
    }
}
