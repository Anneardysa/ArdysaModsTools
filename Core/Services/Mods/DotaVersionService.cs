using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ArdysaModsTools.Core.Services
{
    /// <summary>
    /// Detailed Dota 2 version and patch information.
    /// </summary>
    public record DotaVersionInfo
    {
        /// <summary>Dota 2 version string (e.g., "7.35c").</summary>
        public string DotaVersion { get; init; } = "Unknown";
        
        /// <summary>Build number from steam.inf.</summary>
        public string BuildNumber { get; init; } = "Unknown";
        
        /// <summary>Current DIGEST from dota.signatures.</summary>
        public string CurrentDigest { get; init; } = "";
        
        /// <summary>Previously cached DIGEST (from our last patch).</summary>
        public string? CachedDigest { get; init; }
        
        /// <summary>Hash of gameinfo file content.</summary>
        public string GameInfoHash { get; init; } = "";
        
        /// <summary>Cached gameinfo hash from last patch.</summary>
        public string? CachedGameInfoHash { get; init; }
        
        /// <summary>Whether _ArdysaMods entry exists in gameinfo.</summary>
        public bool GameInfoHasModEntry { get; init; }
        
        /// <summary>When we last successfully patched.</summary>
        public DateTime? LastPatchedDate { get; init; }
        
        /// <summary>Version we last patched for.</summary>
        public string? LastPatchedVersion { get; init; }
        
        /// <summary>Whether the DIGEST has changed since last patch.</summary>
        public bool DigestChanged => !string.Equals(CurrentDigest, CachedDigest, StringComparison.OrdinalIgnoreCase);
        
        /// <summary>Whether gameinfo was modified (external change).</summary>
        public bool GameInfoChanged => !GameInfoHasModEntry || 
            (!string.IsNullOrEmpty(CachedGameInfoHash) && CachedGameInfoHash != GameInfoHash);
        
        /// <summary>Whether any changes require re-patching.</summary>
        public bool NeedsRepatch => DigestChanged || !GameInfoHasModEntry;
    }

    /// <summary>
    /// Service for tracking Dota 2 version and detecting changes.
    /// </summary>
    public class DotaVersionService
    {
        private readonly ILogger? _logger;
        
        // File paths (relative to Dota 2 folder)
        private const string SteamInfPath = "game/dota/steam.inf";
        private const string SignaturesPath = "game/bin/win64/dota.signatures";
        private const string GameInfoPath = "game/dota/gameinfo_branchspecific.gi";
        private const string VersionCachePath = "game/_ArdysaMods/_temp/version_cache.txt";
        private const string VersionJsonPath = "game/_ArdysaMods/_temp/version.json";
        
        private const string ModMarker = "_ArdysaMods";

        public DotaVersionService(ILogger? logger = null)
        {
            _logger = logger; // Logger is optional for DI compatibility
        }

        /// <summary>
        /// Get detailed version information for the Dota 2 installation.
        /// </summary>
        public async Task<DotaVersionInfo> GetVersionInfoAsync(string targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return new DotaVersionInfo();
            }

            try
            {
                // Read Dota version from steam.inf
                var (version, build) = await ReadSteamInfAsync(Path.Combine(targetPath, SteamInfPath));
                
                // Read current DIGEST from signatures
                string currentDigest = await ReadDigestAsync(Path.Combine(targetPath, SignaturesPath));
                
                // Read and analyze gameinfo
                string gameInfoPath = Path.Combine(targetPath, GameInfoPath);
                string gameInfoContent = File.Exists(gameInfoPath) 
                    ? await File.ReadAllTextAsync(gameInfoPath) 
                    : "";
                string gameInfoHash = ComputeHash(gameInfoContent);
                bool hasModEntry = gameInfoContent.Contains(ModMarker, StringComparison.OrdinalIgnoreCase);
                
                // Read cached version info
                var cached = await ReadVersionCacheAsync(Path.Combine(targetPath, VersionCachePath));
                
                return new DotaVersionInfo
                {
                    DotaVersion = version,
                    BuildNumber = build,
                    CurrentDigest = currentDigest,
                    CachedDigest = cached.Digest,
                    GameInfoHash = gameInfoHash,
                    CachedGameInfoHash = cached.GameInfoHash,
                    GameInfoHasModEntry = hasModEntry,
                    LastPatchedDate = cached.PatchDate,
                    LastPatchedVersion = cached.Version
                };
            }
            catch (Exception ex)
            {
                _logger?.Log($"[VERSION] Error reading version info: {ex.Message}");
                return new DotaVersionInfo();
            }
        }

        /// <summary>
        /// Quick check if re-patching is needed without returning full version info.
        /// Optimized for watcher callback to minimize overhead.
        /// </summary>
        public async Task<bool> QuickNeedsPatchCheckAsync(string targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
                return false;
                
            var info = await GetVersionInfoAsync(targetPath);
            return info.NeedsRepatch;
        }

        /// <summary>
        /// Save current version info to cache after successful patch.
        /// </summary>
        public async Task SaveVersionCacheAsync(string targetPath, DotaVersionInfo info)
        {
            try
            {
                string cachePath = Path.Combine(targetPath, VersionCachePath);
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
                
                var lines = new[]
                {
                    $"Version={info.DotaVersion}",
                    $"Build={info.BuildNumber}",
                    $"Digest={info.CurrentDigest}",
                    $"GameInfoHash={info.GameInfoHash}",
                    $"PatchDate={DateTime.Now:O}"
                };
                
                await File.WriteAllLinesAsync(cachePath, lines);
            }
            catch (Exception ex)
            {
                _logger?.Log($"[VERSION] Failed to save cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Save current steam.inf metadata to version.json after patching.
        /// Used to detect if Dota 2 was updated since last patch.
        /// </summary>
        public async Task SavePatchedVersionJsonAsync(string targetPath)
        {
            try
            {
                string steamInfPath = Path.Combine(targetPath, SteamInfPath);
                var (version, build) = await ReadSteamInfAsync(steamInfPath);
                
                string jsonPath = Path.Combine(targetPath, VersionJsonPath);
                Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
                
                var json = System.Text.Json.JsonSerializer.Serialize(new
                {
                    VersionDate = version,
                    Build = build,
                    PatchedAt = DateTime.Now.ToString("O")
                });
                
                await File.WriteAllTextAsync(jsonPath, json);
            }
            catch (Exception ex)
            {
                _logger?.Log($"[VERSION] Failed to save version.json: {ex.Message}");
            }
        }

        /// <summary>
        /// Compare current steam.inf with saved version.json.
        /// Returns (matches, currentVersion, patchedVersion).
        /// </summary>
        public async Task<(bool Matches, string CurrentVersion, string PatchedVersion)> ComparePatchedVersionAsync(string targetPath)
        {
            try
            {
                string steamInfPath = Path.Combine(targetPath, SteamInfPath);
                string jsonPath = Path.Combine(targetPath, VersionJsonPath);
                
                // Read current steam.inf
                var (currentVersion, currentBuild) = await ReadSteamInfAsync(steamInfPath);
                
                if (!File.Exists(jsonPath))
                {
                    return (false, $"{currentVersion} (Build {currentBuild})", "Not patched yet");
                }
                
                // Read saved version
                var jsonContent = await File.ReadAllTextAsync(jsonPath);
                using var doc = System.Text.Json.JsonDocument.Parse(jsonContent);
                var root = doc.RootElement;
                
                string patchedVersion = root.TryGetProperty("VersionDate", out var v) ? v.GetString() ?? "" : "";
                string patchedBuild = root.TryGetProperty("Build", out var b) ? b.GetString() ?? "" : "";
                
                bool matches = currentVersion == patchedVersion && currentBuild == patchedBuild;
                
                return (matches, 
                    $"{currentVersion} (Build {currentBuild})", 
                    $"{patchedVersion} (Build {patchedBuild})");
            }
            catch (Exception ex)
            {
                _logger?.Log($"[VERSION] Compare failed: {ex.Message}");
                return (false, "Unknown", "Error");
            }
        }

        /// <summary>
        /// Get a formatted summary of changes for display.
        /// </summary>
        public string GetChangeSummary(DotaVersionInfo info)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("═══════════════════════════════════════════");
            sb.AppendLine("            VERSION STATUS");
            sb.AppendLine("═══════════════════════════════════════════");
            sb.AppendLine();
            
            // Current vs Last Patched
            sb.AppendLine($"Current Dota 2:  {info.DotaVersion} (Build {info.BuildNumber})");
            if (info.LastPatchedVersion != null)
            {
                sb.AppendLine($"Last Patched:    {info.LastPatchedVersion}");
            }
            if (info.LastPatchedDate.HasValue)
            {
                sb.AppendLine($"Patch Date:      {info.LastPatchedDate.Value:g}");
            }
            sb.AppendLine();
            
            // Changes detected
            sb.AppendLine("───────────────────────────────────────────");
            sb.AppendLine("CHANGES DETECTED:");
            sb.AppendLine("───────────────────────────────────────────");
            sb.AppendLine();
            
            // Signatures status
            sb.AppendLine("Core files:");
            if (string.IsNullOrEmpty(info.CurrentDigest))
            {
                sb.AppendLine("  └─ Status: FILE NOT FOUND");
            }
            else if (info.DigestChanged)
            {
                sb.AppendLine($"  ├─ DIGEST: {Truncate(info.CachedDigest ?? "None", 12)} → {Truncate(info.CurrentDigest, 12)}");
                sb.AppendLine("  └─ Status: CHANGED - Needs re-patch");
            }
            else
            {
                sb.AppendLine($"  ├─ DIGEST: {Truncate(info.CurrentDigest, 20)}");
                sb.AppendLine("  └─ Status: OK - No changes");
            }
            sb.AppendLine();
            
            // GameInfo status
            sb.AppendLine("Game config:");
            if (!info.GameInfoHasModEntry)
            {
                sb.AppendLine("  ├─ _ArdysaMods entry: MISSING");
                sb.AppendLine("  └─ Status: NEEDS PATCH");
            }
            else if (info.GameInfoChanged)
            {
                sb.AppendLine("  ├─ _ArdysaMods entry: Present");
                sb.AppendLine("  └─ Status: MODIFIED - May need re-patch");
            }
            else
            {
                sb.AppendLine("  ├─ _ArdysaMods entry: Present");
                sb.AppendLine("  └─ Status: OK");
            }
            sb.AppendLine();
            
            // Overall status
            sb.AppendLine("───────────────────────────────────────────");
            if (info.NeedsRepatch)
            {
                sb.AppendLine("RESULT: Re-patch required");
            }
            else
            {
                sb.AppendLine("RESULT: All files OK - No action needed");
            }
            sb.AppendLine("═══════════════════════════════════════════");
            
            return sb.ToString();
        }

        #region Private Helpers

        private static async Task<(string Version, string Build)> ReadSteamInfAsync(string path)
        {
            if (!File.Exists(path))
                return ("Unknown", "Unknown");

            try
            {
                var content = await File.ReadAllTextAsync(path);
                
                // Parse ClientVersion=XXXX (build number)
                var buildMatch = Regex.Match(content, @"ClientVersion=(\d+)");
                
                // Parse VersionDate=MMM DD YYYY (e.g., "Dec 20 2025")
                var dateMatch = Regex.Match(content, @"VersionDate=(.+)");
                
                string build = buildMatch.Success ? buildMatch.Groups[1].Value : "Unknown";
                string version = dateMatch.Success ? dateMatch.Groups[1].Value.Trim() : $"Build {build}";
                
                return (version, build);
            }
            catch
            {
                return ("Unknown", "Unknown");
            }
        }

        private static async Task<string> ReadDigestAsync(string path)
        {
            if (!File.Exists(path))
                return "";

            try
            {
                var content = await File.ReadAllTextAsync(path);
                var match = Regex.Match(content, @"DIGEST:([A-F0-9]+)");
                return match.Success ? match.Groups[1].Value : "";
            }
            catch
            {
                return "";
            }
        }

        private static async Task<(string? Digest, string? GameInfoHash, DateTime? PatchDate, string? Version)> 
            ReadVersionCacheAsync(string path)
        {
            if (!File.Exists(path))
                return (null, null, null, null);

            try
            {
                var lines = await File.ReadAllLinesAsync(path);
                string? digest = null, hash = null, version = null;
                DateTime? patchDate = null;

                foreach (var line in lines)
                {
                    var parts = line.Split('=', 2);
                    if (parts.Length != 2) continue;

                    switch (parts[0])
                    {
                        case "Digest": digest = parts[1]; break;
                        case "GameInfoHash": hash = parts[1]; break;
                        case "Version": version = parts[1]; break;
                        case "PatchDate":
                            if (DateTime.TryParse(parts[1], out var dt))
                                patchDate = dt;
                            break;
                    }
                }

                return (digest, hash, patchDate, version);
            }
            catch
            {
                return (null, null, null, null);
            }
        }

        private static string ComputeHash(string content)
        {
            if (string.IsNullOrEmpty(content))
                return "";
                
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(content));
            return Convert.ToHexString(bytes)[..16]; // First 16 chars
        }

        private static string Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return "N/A";
            return value.Length <= maxLength ? value : value[..maxLength] + "...";
        }

        #endregion
    }
}
