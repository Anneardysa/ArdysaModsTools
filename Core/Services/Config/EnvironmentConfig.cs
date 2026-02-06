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
        public static string ConvertToFastUrl(string? url)
        {
            if (string.IsNullOrEmpty(url) || !UseJsDelivrCdn)
                return url ?? string.Empty;
            
            // Pattern: https://raw.githubusercontent.com/{owner}/{repo}/{branch}/{path}
            // Also handles: https://raw.githubusercontent.com/{owner}/{repo}/refs/heads/{branch}/{path}
            const string rawPrefix = "https://raw.githubusercontent.com/";
            if (!url.StartsWith(rawPrefix, StringComparison.OrdinalIgnoreCase))
                return url;
            
            try
            {
                // Extract: owner/repo/...rest
                var remainder = url.Substring(rawPrefix.Length);
                var parts = remainder.Split('/');
                
                if (parts.Length < 4)
                    return url; // Invalid format, return original
                
                var owner = parts[0];
                var repo = parts[1];
                
                string branch;
                string path;
                
                // Handle refs/heads/branch format
                if (parts.Length >= 5 && parts[2] == "refs" && parts[3] == "heads")
                {
                    // Format: owner/repo/refs/heads/branch/path...
                    branch = parts[4];
                    path = string.Join("/", parts.Skip(5));
                }
                else
                {
                    // Standard format: owner/repo/branch/path...
                    branch = parts[2];
                    path = string.Join("/", parts.Skip(3));
                }
                
                if (string.IsNullOrEmpty(path))
                    return url; // No path, return original
                
                // jsDelivr format: https://cdn.jsdelivr.net/gh/owner/repo@branch/path
                return $"https://cdn.jsdelivr.net/gh/{owner}/{repo}@{branch}/{path}";
            }
            catch
            {
                return url; // Any error, return original
            }
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

