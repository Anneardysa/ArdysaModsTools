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
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Exceptions;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Localization;
using ArdysaModsTools.Helpers;

namespace ArdysaModsTools.Core.Services.Utilities
{
    public sealed class RankChangerService : IRankChangerService
    {
        public const string ConfigKey = "RankChanger_State";

        private const long MinIconBytes = 1_024;

        private const string UiRankBadgeStyle = "ui_rank_badge.vcss_c";
        private const string ProfileStatsStyle = "dashboard_page_profile_stats_shared.vcss_c";
        private const string PickPhaseStyle = "dota_hud_hero_picking_player.vcss_c";

        private const string MarkerEntry = "panorama/images/amtm.vtex_c";

        private static readonly string[] GeneratedTextures =
        {
            "amtm.vtex_c", "amtc.vtex_c", "amtr.vtex_c", "amtp.vtex_c",
            "amt_m.vtex_c", "amt_c.vtex_c", "amt_r.vtex_c",
            "hb_m.vtex_c", "hb_c.vtex_c", "hb_r.vtex_c"
        };

        private static readonly TimeSpan HlExtractTimeout = TimeSpan.FromMinutes(5);

        private readonly IConfigService _config;
        private readonly IVpkExtractor _extractor;
        private readonly IVpkRecompiler _recompiler;
        private readonly IVpkReplacer _replacer;
        private readonly IAppLogger? _logger;
        private readonly Func<bool> _isGameRunning;
        private readonly ISteamAppStateService _steamAppState;

        public RankChangerService(
            IConfigService config,
            IVpkExtractor extractor,
            IVpkRecompiler recompiler,
            IVpkReplacer replacer,
            IAppLogger? logger = null,
            Func<bool>? isGameRunning = null,
            ISteamAppStateService? steamAppState = null)
        {
            _steamAppState = steamAppState ?? new SteamAppStateService(logger);
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
            _recompiler = recompiler ?? throw new ArgumentNullException(nameof(recompiler));
            _replacer = replacer ?? throw new ArgumentNullException(nameof(replacer));
            _logger = logger;
            _isGameRunning = isGameRunning ?? (() => ProcessChecker.IsProcessRunning("dota2"));
        }

        public RankChangerState LoadState(string? targetPath = null)
        {
            try
            {
                var json = _config.GetValue<string>(ConfigKey, string.Empty);
                if (string.IsNullOrWhiteSpace(json)) return new RankChangerState(false, null);

                var request = JsonSerializer.Deserialize<RankChangerRequest>(json);
                if (request is null) return new RankChangerState(false, null);

                if (!OverrideStillInPackage(targetPath))
                    return new RankChangerState(false, request);

                return new RankChangerState(true, request);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"RankChanger: could not read saved state ({ex.Message}).");
                return new RankChangerState(false, null);
            }
        }

        private bool OverrideStillInPackage(string? targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath)) return true;
            try
            {
                string modVpk = Path.Combine(PathUtility.NormalizeTargetPath(targetPath), Native(DotaPaths.ModsVpk));
                if (!File.Exists(modVpk)) return true;

                var entries = ReadVpkEntries(modVpk);
                return entries.Count == 0 || entries.Contains(MarkerEntry);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"RankChanger: could not read the package to confirm state ({ex.Message}).");
                return true;
            }
        }

        public Task<OperationResult> ApplyAsync(string targetPath, RankChangerRequest request,
            IProgress<RankChangerProgress>? progress = null, CancellationToken ct = default)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));

            IReadOnlyList<RankOverrideEntry> entries;
            try
            {
                entries = RankOverridePlan.Build(request);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                _logger?.LogDebug($"RankChanger: rejected request — {ex.Message}");
                return Task.FromResult(OperationResult.Fail(Loc.T("rank.error.invalidSelection")));
            }

            if (entries.Count == 0)
                return Task.FromResult(OperationResult.Fail(Loc.T("rank.error.nothingSelected")));

            return RunAsync(targetPath, entries, saveState: request, progress, ct);
        }

        public Task<OperationResult> ResetAsync(string targetPath,
            IProgress<RankChangerProgress>? progress = null, CancellationToken ct = default)
            => RunAsync(targetPath, Array.Empty<RankOverrideEntry>(), saveState: null, progress, ct);

        private async Task<OperationResult> RunAsync(string targetPath,
            IReadOnlyList<RankOverrideEntry> entries, RankChangerRequest? saveState,
            IProgress<RankChangerProgress>? progress, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(targetPath) || !Directory.Exists(targetPath))
                return OperationResult.Fail(Loc.T("rank.error.noPath"));

            if (_isGameRunning())
            {
                _logger?.LogDebug("RankChanger: refused to run while Dota 2 is running.");
                return OperationResult.Fail(Loc.T("shell.warning.closeDota"));
            }

            string dotaRoot = PathUtility.NormalizeTargetPath(targetPath);
            string gameVpk = Path.Combine(dotaRoot, Native(DotaPaths.GameVpk));
            string modVpk = Path.Combine(dotaRoot, Native(DotaPaths.ModsVpk));

            if (!File.Exists(gameVpk))
            {
                _logger?.LogDebug($"RankChanger: game VPK missing at {gameVpk}");
                return new OperationResult
                {
                    Success = false,
                    Message = Loc.T("rank.error.gameFilesMissing"),
                    ErrorCode = ErrorCodes.VPK_FILE_NOT_FOUND
                };
            }

            if (!File.Exists(modVpk))
            {
                _logger?.LogDebug($"RankChanger: main mod package missing at {modVpk}");
                return new OperationResult
                {
                    Success = false,
                    Message = Loc.T("rank.error.notInstalled"),
                    ErrorCode = ErrorCodes.VPK_FILE_NOT_FOUND
                };
            }

            string sourceModVpk = modVpk;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string hlExtract = Path.Combine(baseDir, "HLExtract.exe");
            string vpkTool = Path.Combine(baseDir, "vpk.exe");
            if (!File.Exists(hlExtract) || !File.Exists(vpkTool))
            {
                _logger?.LogDebug($"RankChanger: tools missing (HLExtract={File.Exists(hlExtract)}, vpk.exe={File.Exists(vpkTool)})");
                return new OperationResult
                {
                    Success = false,
                    Message = Loc.T("rank.error.toolsMissing"),
                    ErrorCode = ErrorCodes.VPK_TOOL_NOT_FOUND
                };
            }

            string tempRoot = SafeTempPathHelper.CreateSafeTempDirectory("ArdysaRank");
            string iconsDir = Path.Combine(tempRoot, "icons");
            string stylesDir = Path.Combine(tempRoot, "styles");
            string extractDir = Path.Combine(tempRoot, "extract");
            string buildDir = Path.Combine(tempRoot, "build");

            try
            {
                Report(progress, RankChangerStage.Start, 5);
                Directory.CreateDirectory(extractDir);
                Directory.CreateDirectory(buildDir);

                string? iconSource = null;
                byte[]? patchedUiBadge = null;
                byte[]? patchedProfileStats = null;
                byte[]? patchedPickPhase = null;
                string? pickLabel = null;

                if (entries.Count > 0 && saveState != null)
                {
                    Report(progress, RankChangerStage.Processing, 15);
                    iconSource = await ExtractIconsAsync(gameVpk, iconsDir, ct).ConfigureAwait(false);
                    if (iconSource is null)
                    {
                        return new OperationResult
                        {
                            Success = false,
                            Message = Loc.T("rank.error.extractFailed"),
                            ErrorCode = ErrorCodes.VPK_EXTRACT_FAILED
                        };
                    }

                    ct.ThrowIfCancellationRequested();

                    Directory.CreateDirectory(stylesDir);
                    var (badgePath, statsPath, pickPath) = await ExtractStylesAsync(gameVpk, stylesDir, ct).ConfigureAwait(false);
                    if (badgePath is null || statsPath is null)
                    {
                        _logger?.LogDebug("RankChanger: stylesheet extraction failed");
                        return new OperationResult
                        {
                            Success = false,
                            Message = Loc.T("rank.error.extractFailed"),
                            ErrorCode = ErrorCodes.VPK_EXTRACT_FAILED
                        };
                    }

                    try
                    {
                        byte[] origBadge = await File.ReadAllBytesAsync(badgePath, ct).ConfigureAwait(false);
                        byte[] origStats = await File.ReadAllBytesAsync(statsPath, ct).ConfigureAwait(false);

                        int? numberOnlyOnTier = saveState.ReplaceEveryRank ? null : saveState.CurrentTier;

                        patchedUiBadge = PanoramaCssPatcher.PatchUiRankBadge(origBadge, numberOnlyOnTier);
                        patchedProfileStats = PanoramaCssPatcher.PatchProfileStats(origStats);
                    }
                    catch (InvalidOperationException ex)
                    {
                        _logger?.LogError($"RankChanger: CSS patching failed ({ex.Message})", ex);
                        return OperationResult.Fail(Loc.T("rank.error.gameUpdated"));
                    }

                    patchedPickPhase = TryPatchPickPhase(
                        pickPath is null ? null : await File.ReadAllBytesAsync(pickPath, ct).ConfigureAwait(false),
                        saveState, _logger);
                    if (patchedPickPhase != null)
                        pickLabel = (await ReadRankNamesAsync(gameVpk, dotaRoot, Path.Combine(tempRoot, "loc"), ct)
                            .ConfigureAwait(false)).Label(saveState);
                }

                ct.ThrowIfCancellationRequested();

                Report(progress, RankChangerStage.Processing, 35);
                if (!await _extractor.ExtractAsync(hlExtract, sourceModVpk, extractDir,
                        line => _logger?.LogDebug($"[RankChanger] {line}"), ct, null, requireItemsGame: false)
                        .ConfigureAwait(false))
                {
                    return new OperationResult
                    {
                        Success = false,
                        Message = Loc.T("rank.error.extractFailed"),
                        ErrorCode = ErrorCodes.VPK_EXTRACT_FAILED
                    };
                }

                if (await GameItemsGameExtractorService.RepairLineEndingsInPlaceAsync(
                        Path.Combine(extractDir, "scripts", "items", "items_game.txt"), ct).ConfigureAwait(false))
                    _logger?.Log("RankChanger: the package's item data had CRLF line endings, which the game rejects; rewrote it to LF.");

                ct.ThrowIfCancellationRequested();

                Report(progress, RankChangerStage.Processing, 55);
                var expectedVpkEntries = await StageOverridesAsync(extractDir, iconSource, entries, saveState,
                    patchedUiBadge, patchedProfileStats, patchedPickPhase, pickLabel, dotaRoot, _logger, ct).ConfigureAwait(false);
                if (expectedVpkEntries is null)
                {
                    return new OperationResult
                    {
                        Success = false,
                        Message = Loc.T("rank.error.iconMissing"),
                        ErrorCode = ErrorCodes.VPK_EXTRACT_FAILED
                    };
                }

                ct.ThrowIfCancellationRequested();

                Report(progress, RankChangerStage.Compiling, 70);
                string? newVpk = await _recompiler.RecompileAsync(vpkTool, extractDir, buildDir, tempRoot,
                    line => _logger?.LogDebug($"[RankChanger] {line}"), ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(newVpk) || !File.Exists(newVpk))
                {
                    return new OperationResult
                    {
                        Success = false,
                        Message = Loc.T("rank.error.repackFailed"),
                        ErrorCode = ErrorCodes.VPK_RECOMPILE_FAILED
                    };
                }

                if (!EnsurePanoramaProtectedInVpk(newVpk, expectedVpkEntries, out var protectionError))
                {
                    _logger?.LogError($"RankChanger: Protected VPK guard failed — {protectionError}");
                    return OperationResult.Fail(Loc.T("rank.error.applyFailed", new { error = protectionError }));
                }

                ct.ThrowIfCancellationRequested();
                Report(progress, RankChangerStage.Compiling, 90);

                SweepLoosePanoramaFiles(dotaRoot);

                if (!await _replacer.ReplaceAsync(dotaRoot, newVpk,
                        line => _logger?.Log($"[RankChanger] {line}"), ct).ConfigureAwait(false))
                {
                    return OperationResult.Fail(Loc.T("rank.error.installFailed"));
                }

                SweepLoosePanoramaFiles(dotaRoot);
                SaveState(saveState);
                Report(progress, RankChangerStage.Finish, 100);
                return OperationResult.Ok(entries.Count);
            }
            catch (OperationCanceledException)
            {
                return OperationResult.Canceled();
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[RankChanger] {ex.Message}", ex);
                return OperationResult.Fail(Loc.T("rank.error.applyFailed", new { error = ex.Message }));
            }
            finally
            {
                if (!SafeTempPathHelper.TryDeleteWorkDir(tempRoot))
                    _logger?.LogDebug($"RankChanger: could not fully clean up temp directory '{tempRoot}'.");
            }
        }

        internal static async Task<IReadOnlyList<string>?> StageOverridesAsync(string extractDir,
            string? iconSource, IReadOnlyList<RankOverrideEntry> entries, RankChangerRequest? request,
            byte[]? patchedUiBadge, byte[]? patchedProfileStats, byte[]? patchedPickPhase, string? pickLabel,
            string dotaRoot, IAppLogger? logger, CancellationToken ct)
        {
            string iconRoot = Path.Combine(extractDir, Native(RankOverridePlan.IconFolder));
            string imagesRoot = Path.Combine(extractDir, "panorama", "images");
            string stylesRoot = Path.Combine(extractDir, "panorama", "styles");
            string pickCss = Path.Combine(extractDir, Native(PanoramaCssPatcher.PickPhaseSheet));
            if (File.Exists(pickCss)) File.Delete(pickCss);

            if (Directory.Exists(iconRoot)) Directory.Delete(iconRoot, recursive: true);

            foreach (var oldFile in GeneratedTextures)
            {
                string p = Path.Combine(imagesRoot, oldFile);
                if (File.Exists(p)) File.Delete(p);
            }

            string badgeCss = Path.Combine(stylesRoot, UiRankBadgeStyle);
            string statsCss = Path.Combine(stylesRoot, ProfileStatsStyle);
            if (File.Exists(badgeCss)) File.Delete(badgeCss);
            if (File.Exists(statsCss)) File.Delete(statsCss);

            var expected = new List<string>();
            if (entries.Count == 0 || request is null) return expected;

            if (iconSource is null || patchedUiBadge is null || patchedProfileStats is null)
                throw new ArgumentException("An Apply needs the extracted icons and both patched stylesheets.");

            Directory.CreateDirectory(iconRoot);
            Directory.CreateDirectory(imagesRoot);
            Directory.CreateDirectory(stylesRoot);

            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();

                string source = Path.Combine(iconSource, Native(entry.SourceRelative));
                if (!File.Exists(source) || new FileInfo(source).Length < MinIconBytes)
                {
                    logger?.LogDebug($"RankChanger: source icon missing or too small — {entry.SourceRelative}");
                    return null;
                }

                string dest = Path.Combine(iconRoot, Native(entry.DestRelative));
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(source, dest, overwrite: true);
                expected.Add($"{RankOverridePlan.IconFolder}/{entry.DestRelative}");
            }

            if (RankOverridePlan.WipesPips(request))
            {
                byte[] emptyPip = VtexGenerator.GenerateEmptyPipTexture();
                for (int pip = RankOverridePlan.MinStars; pip <= RankOverridePlan.MaxStars; pip++)
                {
                    string name = RankOverridePlan.Stars(pip);
                    await File.WriteAllBytesAsync(Path.Combine(iconRoot, name), emptyPip, ct).ConfigureAwait(false);
                    expected.Add($"{RankOverridePlan.IconFolder}/{name}");
                }
            }

            bool isImmortalLb = request.TargetTier == RankOverridePlan.MaxTier && request.TargetLeaderboard > 0;
            var textures = new (string Name, byte[] Bytes)[]
            {
                ("amtm.vtex_c", VtexGenerator.GenerateMmrTexture(request.ProfileMmr, dotaRoot)),
                ("amtc.vtex_c", VtexGenerator.GenerateConfidenceTexture(request.RankConfidence)),
                ("amtr.vtex_c", VtexGenerator.GenerateLeaderboardRankTexture(request.LeaderboardRank, isImmortalLb, dotaRoot)),
            };
            foreach (var (name, bytes) in textures)
            {
                await File.WriteAllBytesAsync(Path.Combine(imagesRoot, name), bytes, ct).ConfigureAwait(false);
                expected.Add($"panorama/images/{name}");
            }

            await File.WriteAllBytesAsync(badgeCss, patchedUiBadge, ct).ConfigureAwait(false);
            await File.WriteAllBytesAsync(statsCss, patchedProfileStats, ct).ConfigureAwait(false);
            expected.Add($"panorama/styles/{UiRankBadgeStyle}");
            expected.Add($"panorama/styles/{ProfileStatsStyle}");

            if (patchedPickPhase != null && pickLabel != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(pickCss)!);
                await File.WriteAllBytesAsync(pickCss, patchedPickPhase, ct).ConfigureAwait(false);
                await File.WriteAllBytesAsync(Path.Combine(extractDir, Native(PanoramaCssPatcher.PickLabelTexture)),
                    VtexGenerator.GeneratePickRankTexture(pickLabel, dotaRoot), ct).ConfigureAwait(false);
                expected.Add(PanoramaCssPatcher.PickPhaseSheet);
                expected.Add(PanoramaCssPatcher.PickLabelTexture);
            }

            return expected;
        }

        private static string Native(string relative) => relative.Replace('/', Path.DirectorySeparatorChar);

        private static void Report(IProgress<RankChangerProgress>? progress, RankChangerStage stage, int percent)
            => progress?.Report(new RankChangerProgress(stage, percent));

        public void SweepLoosePanoramaFiles(string dotaRoot) => SweepLoosePanorama(dotaRoot, _logger);

        internal static void SweepLoosePanorama(string dotaRoot, IAppLogger? logger)
        {
            try
            {
                string loosePanorama = Path.Combine(dotaRoot, "game", "_ArdysaMods", "panorama");
                if (Directory.Exists(loosePanorama))
                {
                    Directory.Delete(loosePanorama, recursive: true);
                    logger?.LogDebug("RankChanger: purged loose panorama directory from disk.");
                }
            }
            catch (Exception ex)
            {
                logger?.LogDebug($"RankChanger: loose panorama sweep error — {ex.Message}");
            }
        }

        public static bool EnsurePanoramaProtectedInVpk(string vpkPath, IReadOnlyList<string> expectedEntries, out string error)
        {
            if (string.IsNullOrWhiteSpace(vpkPath) || !File.Exists(vpkPath))
            {
                error = "VPK file does not exist.";
                return false;
            }

            if (!VpkPackageValidator.TryValidate(vpkPath, out error))
            {
                return false;
            }

            if (!VpkSignatureSection.IsApplied(vpkPath))
            {
                if (!VpkSignatureSection.Apply(vpkPath, out var sigErr))
                {
                    error = $"Package signature protection missing and could not be applied: {sigErr}";
                    return false;
                }
            }

            if (expectedEntries == null || expectedEntries.Count == 0)
            {
                error = string.Empty;
                return true;
            }

            try
            {
                var containedEntries = ReadVpkEntries(vpkPath);
                foreach (var expected in expectedEntries)
                {
                    string normalized = expected.Replace('\\', '/').TrimStart('/');
                    if (!containedEntries.Contains(normalized))
                    {
                        error = $"Protected VPK is missing required panorama entry: {normalized}";
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                error = $"Failed to verify VPK entries: {ex.Message}";
                return false;
            }

            error = string.Empty;
            return true;
        }

        internal static HashSet<string> ReadVpkEntries(string vpkPath)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var fs = new FileStream(vpkPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: true);

            if (fs.Length < 12) return set;
            if (br.ReadUInt32() != 0x55AA1234) return set;
            uint version = br.ReadUInt32();
            uint treeSize = br.ReadUInt32();
            if (version == 2)
            {
                br.ReadUInt32();
                br.ReadUInt32();
                br.ReadUInt32();
                br.ReadUInt32();
            }

            byte[] tree = br.ReadBytes((int)treeSize);
            int pos = 0;

            while (pos < tree.Length)
            {
                string ext = ReadCString(tree, ref pos);
                if (string.IsNullOrEmpty(ext)) break;

                while (pos < tree.Length)
                {
                    string dir = ReadCString(tree, ref pos);
                    if (string.IsNullOrEmpty(dir)) break;

                    while (pos < tree.Length)
                    {
                        string name = ReadCString(tree, ref pos);
                        if (string.IsNullOrEmpty(name)) break;

                        if (pos + 18 > tree.Length) break;
                        ushort preloadBytes = (ushort)(tree[pos + 4] | (tree[pos + 5] << 8));
                        pos += 18;
                        pos += preloadBytes;

                        string fullPath = dir == " " ? $"{name}.{ext}" : $"{dir}/{name}.{ext}";
                        set.Add(fullPath);
                    }
                }
            }

            return set;
        }

        private static string ReadCString(byte[] bytes, ref int pos)
        {
            int start = pos;
            while (pos < bytes.Length && bytes[pos] != 0) pos++;
            string result = Encoding.UTF8.GetString(bytes, start, pos - start);
            if (pos < bytes.Length) pos++;
            return result;
        }

        private void SaveState(RankChangerRequest? request)
        {
            try
            {
                _config.SetValue(ConfigKey, request is null ? string.Empty : JsonSerializer.Serialize(request));
                _config.Save();
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"RankChanger: could not save state ({ex.Message}).");
            }
        }

        private async Task<string?> ExtractIconsAsync(string vpkPath, string tempRoot, CancellationToken ct)
        {
            string hlExtract = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HLExtract.exe");
            if (!File.Exists(hlExtract)) return null;

            Directory.CreateDirectory(tempRoot);

            foreach (var entry in new[] { $"root/{RankOverridePlan.IconFolder}", RankOverridePlan.IconFolder })
            {
                ct.ThrowIfCancellationRequested();

                if (!await RunHlExtractAsync(hlExtract, $"-p \"{vpkPath}\" -d \"{tempRoot}\" -e \"{entry}\" -s", ct).ConfigureAwait(false))
                    continue;

                string? probe = Directory
                    .EnumerateFiles(tempRoot, "rank0_psd.vtex_c", SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (probe != null) return Path.GetDirectoryName(probe);
            }

            return null;
        }

        private async Task<(string? badgePath, string? statsPath, string? pickPath)> ExtractStylesAsync(string vpkPath, string tempRoot, CancellationToken ct)
        {
            string hlExtract = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HLExtract.exe");
            if (!File.Exists(hlExtract)) return (null, null, null);

            Directory.CreateDirectory(tempRoot);

            foreach (var prefix in new[] { "root/panorama/styles/", "panorama/styles/" })
            {
                ct.ThrowIfCancellationRequested();

                string args = $"-p \"{vpkPath}\" -d \"{tempRoot}\" -e \"{prefix}{UiRankBadgeStyle}\" -e \"{prefix}{ProfileStatsStyle}\" -e \"{prefix}hud/{PickPhaseStyle}\" -m -s";
                if (!await RunHlExtractAsync(hlExtract, args, ct).ConfigureAwait(false))
                    continue;

                string? badge = Directory.EnumerateFiles(tempRoot, UiRankBadgeStyle, SearchOption.AllDirectories).FirstOrDefault();
                string? stats = Directory.EnumerateFiles(tempRoot, ProfileStatsStyle, SearchOption.AllDirectories).FirstOrDefault();
                string? pick = Directory.EnumerateFiles(tempRoot, PickPhaseStyle, SearchOption.AllDirectories).FirstOrDefault();

                if (badge != null && stats != null)
                    return (badge, stats, pick);
            }

            return (null, null, null);
        }

        internal static byte[]? TryPatchPickPhase(byte[]? original, RankChangerRequest request, IAppLogger? logger)
        {
            try
            {
                if (original is null || original.Length == 0)
                    throw new InvalidOperationException("the stylesheet was not extracted");

                return PanoramaCssPatcher.PatchHeroPickingPlayer(original,
                    onlyLocalPlayer: !request.ReplaceEveryRank);
            }
            catch (InvalidOperationException ex)
            {
                logger?.LogDebug($"RankChanger: picking-phase patch skipped ({ex.Message})");
                logger?.Log("RankChanger: the rank shown under your hero during picking could not be changed for this Dota version; the medals were still changed.");
                return null;
            }
        }

        private async Task<RankTierNames> ReadRankNamesAsync(string gameVpk, string dotaRoot, string tempRoot, CancellationToken ct)
        {
            string language = _steamAppState.Read(dotaRoot).Language ?? "english";
            string name = $"dota_{language}.txt";
            string hlExtract = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HLExtract.exe");
            Directory.CreateDirectory(tempRoot);

            foreach (var prefix in new[] { "root/resource/localization/", "resource/localization/" })
            {
                ct.ThrowIfCancellationRequested();
                if (!await RunHlExtractAsync(hlExtract, $"-p \"{gameVpk}\" -d \"{tempRoot}\" -e \"{prefix}{name}\" -m -s", ct).ConfigureAwait(false))
                    continue;

                string? file = Directory.EnumerateFiles(tempRoot, name, SearchOption.AllDirectories).FirstOrDefault();
                if (file != null)
                    return RankTierNames.Parse(await File.ReadAllTextAsync(file, ct).ConfigureAwait(false));
            }

            _logger?.LogDebug($"RankChanger: {name} not readable; the picking label uses English.");
            return RankTierNames.English;
        }

        internal static IEnumerable<string> OwnedPaths() =>
            GeneratedTextures.Select(t => $"panorama/images/{t}")
                .Concat(new[] { PanoramaCssPatcher.UiRankBadgeSheet, PanoramaCssPatcher.ProfileStatsSheet, PanoramaCssPatcher.PickPhaseSheet });

        private Task<bool> RunHlExtractAsync(string hlExtractPath, string arguments, CancellationToken ct)
            => RunToolAsync(hlExtractPath, arguments, HlExtractTimeout, _logger, ct);

        internal static async Task<bool> RunToolAsync(string exePath, string arguments, TimeSpan timeout,
            IAppLogger? logger, CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                logger?.LogDebug($"RankChanger: could not start {Path.GetFileName(exePath)} ({ex.Message})");
                return false;
            }

            var stdOut = process.StandardOutput.ReadToEndAsync();
            var stdErr = process.StandardError.ReadToEndAsync();

            using var bounded = CancellationTokenSource.CreateLinkedTokenSource(ct);
            bounded.CancelAfter(timeout);
            try
            {
                await process.WaitForExitAsync(bounded.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch {  }
                ct.ThrowIfCancellationRequested();
                logger?.LogDebug($"RankChanger: {Path.GetFileName(exePath)} timed out after {timeout.TotalSeconds:0}s.");
                return false;
            }

            string err = await stdErr.ConfigureAwait(false);
            await stdOut.ConfigureAwait(false);
            if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(err))
                logger?.LogDebug($"RankChanger: {Path.GetFileName(exePath)} exit {process.ExitCode}: {err.Trim()}");

            return process.ExitCode == 0;
        }
    }
}
