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

namespace ArdysaModsTools.Core.Services.Config
{
    /// <summary>
    /// Loads configuration from environment variables or .env file.
    /// For open-source deployment, users must provide their own configuration.
    /// </summary>
    public static class EnvironmentConfig
    {
        private static bool _loaded = false;
        
        /// <summary>
        /// Load environment variables from .env file if it exists.
        /// Call this at application startup.
        /// </summary>
        public static void LoadFromEnvFile()
        {
            if (_loaded) return;
            _loaded = true;
            
            var envPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env");
            if (!File.Exists(envPath))
            {
                // Try parent directories (for development)
                var current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                while (current?.Parent != null)
                {
                    var testPath = Path.Combine(current.FullName, ".env");
                    if (File.Exists(testPath))
                    {
                        envPath = testPath;
                        break;
                    }
                    current = current.Parent;
                }
            }
            
            if (!File.Exists(envPath)) return;
            
            try
            {
                foreach (var line in File.ReadAllLines(envPath))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                        continue;
                    
                    var idx = trimmed.IndexOf('=');
                    if (idx <= 0) continue;
                    
                    var key = trimmed.Substring(0, idx).Trim();
                    var value = trimmed.Substring(idx + 1).Trim();
                    
                    // Only set if not already defined (system env takes precedence)
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                    {
                        Environment.SetEnvironmentVariable(key, value);
                    }
                }
            }
            catch
            {
                // Silently fail - app will use defaults or show setup instructions
            }
        }
        
        // ============================================================
        // Configuration Properties
        // ============================================================
        
        /// <summary>
        /// Password for encrypted mod archive files.
        /// DEPRECATED: Archives are now expected to be unprotected.
        /// </summary>
        [Obsolete("Archives no longer require passwords")]
        public static string ArchivePassword => 
            Environment.GetEnvironmentVariable("AMT_ARCHIVE_PASSWORD") ?? "";
        
        /// <summary>
        /// Passphrase for portable string encryption.
        /// </summary>
        public static string Passphrase => 
            Environment.GetEnvironmentVariable("AMT_PASSPHRASE") ?? "";
        
        /// <summary>
        /// GitHub repository owner/organization.
        /// </summary>
        public static string GitHubOwner => 
            Environment.GetEnvironmentVariable("GITHUB_OWNER") ?? "Anneardysa";
        
        /// <summary>
        /// GitHub mods pack repository name.
        /// </summary>
        public static string GitHubModsRepo => 
            Environment.GetEnvironmentVariable("GITHUB_MODS_REPO") ?? "ModsPack";
        
        /// <summary>
        /// GitHub tools repository name.
        /// </summary>
        public static string GitHubToolsRepo => 
            Environment.GetEnvironmentVariable("GITHUB_TOOLS_REPO") ?? "ArdysaModsTools";
        
        /// <summary>
        /// Default branch name.
        /// </summary>
        public static string GitHubBranch => 
            Environment.GetEnvironmentVariable("GITHUB_BRANCH") ?? "main";
        
        /// <summary>
        /// Whether to use jsDelivr CDN for GitHub content (faster global CDN).
        /// Set USE_JSDELIVR_CDN=false in .env to disable.
        /// Default: true (CDN enabled for better performance).
        /// </summary>
        public static bool UseJsDelivrCdn =>
            !string.Equals(
                Environment.GetEnvironmentVariable("USE_JSDELIVR_CDN"), 
                "false", 
                StringComparison.OrdinalIgnoreCase);
        
        /// <summary>
        /// Optional GitHub Personal Access Token for higher rate limits.
        /// Without token: 60 requests/hour. With token: 5000 requests/hour.
        /// Only needs "public_repo" scope for public repository access.
        /// </summary>
        public static string? GitHubToken =>
            Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        
        // ============================================================
        // URL Builders
        // ============================================================
        
        /// <summary>
        /// Base URL for raw GitHub content (original, may be slower).
        /// </summary>
        public static string RawGitHubBase =>
            $"https://raw.githubusercontent.com/{GitHubOwner}/{GitHubModsRepo}/{GitHubBranch}";
        
        /// <summary>
        /// Base URL for jsDelivr CDN GitHub content (faster, cached globally).
        /// Format: https://cdn.jsdelivr.net/gh/owner/repo@branch
        /// </summary>
        public static string JsDelivrBase =>
            $"https://cdn.jsdelivr.net/gh/{GitHubOwner}/{GitHubModsRepo}@{GitHubBranch}";
        
        /// <summary>
        /// Base URL for Cloudflare R2 CDN (primary, fastest).
        /// </summary>
        public static string R2CdnBase =>
            Environment.GetEnvironmentVariable("R2_CDN_BASE") ?? "https://cdn.ardysamods.my.id";
        
        /// <summary>
        /// Get the best content base URL.
        /// Priority: R2 CDN (primary) -> jsDelivr CDN -> raw GitHub (fallback).
        /// </summary>
        public static string ContentBase => R2CdnBase;
        
        /// <summary>
        /// Base URL for GitHub downloads.
        /// </summary>
        public static string GitHubDownloadBase =>
            $"https://github.com/{GitHubOwner}/{GitHubModsRepo}/raw/{GitHubBranch}";
        
        /// <summary>
        /// GitHub API URL for mods repository releases.
        /// </summary>
        public static string ModsPackReleasesApi =>
            $"https://api.github.com/repos/{GitHubOwner}/{GitHubModsRepo}/releases/latest";
        
        /// <summary>
        /// GitHub API URL for tools repository releases.
        /// </summary>
        public static string ToolsReleasesApi =>
            $"https://api.github.com/repos/{GitHubOwner}/{GitHubToolsRepo}/releases/latest";
        
        // ============================================================
        // Cache-Busting for CDN
        // ============================================================
        
        /// <summary>
        /// Generate a cache-busting key based on current hour.
        /// This ensures cache refreshes at most once per hour while maximizing CDN hits.
        /// Format: YYYYMMDDHH (year + month + day + hour)
        /// </summary>
        private static string GetCacheBustKey() =>
            DateTime.UtcNow.ToString("yyyyMMddHH");
        
        /// <summary>
        /// Build content URL for a specific path.
        /// Uses CDN when enabled for faster downloads.
        /// NOTE: This URL is cached by CDN - use BuildFreshUrl for JSON that needs real-time updates.
        /// </summary>
        public static string BuildRawUrl(string path) =>
            $"{ContentBase}/{path.TrimStart('/')}";
        
        /// <summary>
        /// Build content URL with cache-busting for files that need real-time updates (e.g., JSON configs).
        /// Adds an hourly cache-bust parameter to ensure fresh data while still benefiting from CDN.
        /// Use this for: heroes.json, set_update.json, and other frequently-updated metadata.
        /// </summary>
        /// <param name="path">Path within repo (e.g., "Assets/heroes.json")</param>
        public static string BuildFreshUrl(string path) =>
            $"{ContentBase}/{path.TrimStart('/')}?v={GetCacheBustKey()}";
        
        /// <summary>
        /// Build download URL for a specific path.
        /// </summary>
        public static string BuildDownloadUrl(string path) => 
            $"{GitHubDownloadBase}/{path.TrimStart('/')}";
        
        /// <summary>
        /// Convert a raw GitHub URL to use jsDelivr CDN when enabled.
        /// If CDN is disabled or URL is not a GitHub raw URL, returns original URL.
        /// </summary>
        /// <example>
        /// Input:  https://raw.githubusercontent.com/Owner/Repo/main/path/file.zip
        /// Input:  https://raw.githubusercontent.com/Owner/Repo/refs/heads/main/path/file.zip
        /// Output: https://cdn.jsdelivr.net/gh/Owner/Repo@main/path/file.zip
        /// </example>
        /// <summary>
        /// Convert a raw GitHub URL to use the fastest available CDN.
        /// Priority: R2 CDN (for our ModsPack repo) → jsDelivr (for other repos).
        /// </summary>
        public static string ConvertToFastUrl(string? url)
        {
            if (string.IsNullOrEmpty(url))
                return url ?? string.Empty;
            
            // Pattern: https://raw.githubusercontent.com/{owner}/{repo}/{branch}/{path}
            // Also handles: https://raw.githubusercontent.com/{owner}/{repo}/refs/heads/{branch}/{path}
            const string rawPrefix = "https://raw.githubusercontent.com/";
            if (!url.StartsWith(rawPrefix, StringComparison.OrdinalIgnoreCase))
                return url;
            
            try
            {
                var (owner, repo, branch, path) = ParseRawGitHubUrl(url);
                if (string.IsNullOrEmpty(path))
                    return url;
                
                // R2 CDN: use for our own ModsPack repo (faster, no file-type restrictions)
                if (owner.Equals(GitHubOwner, StringComparison.OrdinalIgnoreCase) &&
                    repo.Equals(GitHubModsRepo, StringComparison.OrdinalIgnoreCase) &&
                    branch.Equals(GitHubBranch, StringComparison.OrdinalIgnoreCase))
                {
                    return $"{R2CdnBase}/{path}";
                }
                
                // jsDelivr CDN for other repos (when enabled)
                if (UseJsDelivrCdn)
                    return $"https://cdn.jsdelivr.net/gh/{owner}/{repo}@{branch}/{path}";
                
                return url;
            }
            catch
            {
                return url;
            }
        }
        
        /// <summary>
        /// Build fallback CDN URLs for a given primary URL.
        /// Given any recognized CDN URL (R2/jsDelivr/raw GitHub), extracts the repo-relative
        /// path and returns alternative CDN URLs to try if the primary fails.
        /// This enables resilience against regional CDN blocks.
        /// </summary>
        /// <returns>Fallback URLs in priority order (may be empty if URL is not recognized).</returns>
        public static string[] BuildFallbackUrls(string? url)
        {
            if (string.IsNullOrEmpty(url))
                return Array.Empty<string>();
            
            string? repoPath = null;
            var fallbacks = new List<string>(2);
            
            // Detect URL type and extract repo-relative path
            if (url.StartsWith(R2CdnBase, StringComparison.OrdinalIgnoreCase))
            {
                // R2 CDN URL: https://cdn.ardysamods.my.id/{path}
                repoPath = url.Substring(R2CdnBase.Length).TrimStart('/');
                // Fallbacks: jsDelivr → raw GitHub
                fallbacks.Add($"{JsDelivrBase}/{repoPath}");
                fallbacks.Add($"{RawGitHubBase}/{repoPath}");
            }
            else if (url.StartsWith("https://raw.githubusercontent.com/", StringComparison.OrdinalIgnoreCase))
            {
                // Raw GitHub URL
                var parsed = ParseRawGitHubUrl(url);
                if (!string.IsNullOrEmpty(parsed.Path) &&
                    parsed.Owner.Equals(GitHubOwner, StringComparison.OrdinalIgnoreCase) &&
                    parsed.Repo.Equals(GitHubModsRepo, StringComparison.OrdinalIgnoreCase))
                {
                    repoPath = parsed.Path;
                    // Fallbacks: R2 → jsDelivr
                    fallbacks.Add($"{R2CdnBase}/{repoPath}");
                    fallbacks.Add($"{JsDelivrBase}/{repoPath}");
                }
            }
            else if (url.StartsWith("https://cdn.jsdelivr.net/gh/", StringComparison.OrdinalIgnoreCase))
            {
                // jsDelivr URL: https://cdn.jsdelivr.net/gh/owner/repo@branch/path
                try
                {
                    var remainder = url.Substring("https://cdn.jsdelivr.net/gh/".Length);
                    var atIdx = remainder.IndexOf('@');
                    if (atIdx >= 0)
                    {
                        var afterAt = remainder.Substring(atIdx + 1);
                        var slashIdx = afterAt.IndexOf('/');
                        if (slashIdx >= 0)
                        {
                            repoPath = afterAt.Substring(slashIdx + 1);
                            // Fallbacks: R2 → raw GitHub
                            fallbacks.Add($"{R2CdnBase}/{repoPath}");
                            fallbacks.Add($"{RawGitHubBase}/{repoPath}");
                        }
                    }
                }
                catch { }
            }
            
            return fallbacks.ToArray();
        }
        
        /// <summary>
        /// Parse a raw GitHub URL into its components.
        /// Handles both standard format and refs/heads format.
        /// </summary>
        private static (string Owner, string Repo, string Branch, string Path) ParseRawGitHubUrl(string url)
        {
            const string rawPrefix = "https://raw.githubusercontent.com/";
            if (!url.StartsWith(rawPrefix, StringComparison.OrdinalIgnoreCase))
                return (string.Empty, string.Empty, string.Empty, string.Empty);
            
            var remainder = url.Substring(rawPrefix.Length);
            var parts = remainder.Split('/');
            
            if (parts.Length < 4)
                return (string.Empty, string.Empty, string.Empty, string.Empty);
            
            var owner = parts[0];
            var repo = parts[1];
            
            string branch;
            string path;
            
            // Handle refs/heads/branch format
            if (parts.Length >= 5 && parts[2] == "refs" && parts[3] == "heads")
            {
                branch = parts[4];
                path = string.Join("/", parts.Skip(5));
            }
            else
            {
                branch = parts[2];
                path = string.Join("/", parts.Skip(3));
            }
            
            return (owner, repo, branch, path);
        }
        
        // ============================================================
        // ============================================================
        
        /// <summary>
        /// Check if the configuration is valid for operation.
        /// </summary>
        public static bool IsConfigured => 
            !string.IsNullOrEmpty(GitHubOwner);
        
        /// <summary>
        /// Get configuration status message.
        /// </summary>
        public static string GetConfigStatus()
        {
            if (IsConfigured)
                return "Configuration loaded successfully.";
            
            var missing = new System.Collections.Generic.List<string>();
            if (string.IsNullOrEmpty(GitHubOwner)) missing.Add("GITHUB_OWNER");
            
            return $"Missing configuration: {string.Join(", ", missing)}. " +
                   "Copy .env.example to .env and fill in your values.";
        }
    }
}

