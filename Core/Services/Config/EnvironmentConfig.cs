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
    public static class EnvironmentConfig
    {
        private static bool _loaded = false;
        
        public static void LoadFromEnvFile()
        {
            if (_loaded) return;
            _loaded = true;
            
            var envPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env");
            if (!File.Exists(envPath))
            {
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
                    
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                    {
                        Environment.SetEnvironmentVariable(key, value);
                    }
                }
            }
            catch
            {
            }
        }
        
        
        public static string Passphrase => 
            Environment.GetEnvironmentVariable("AMT_PASSPHRASE") ?? "";
        
        public static string GitHubOwner => 
            Environment.GetEnvironmentVariable("GITHUB_OWNER") ?? "Anneardysa";
        
        public static string GitHubModsRepo => 
            Environment.GetEnvironmentVariable("GITHUB_MODS_REPO") ?? "ModsPack";
        
        public static string GitHubToolsRepo => 
            Environment.GetEnvironmentVariable("GITHUB_TOOLS_REPO") ?? "ArdysaModsTools";
        
        public static string GitHubBranch => 
            Environment.GetEnvironmentVariable("GITHUB_BRANCH") ?? "main";
        
        public static bool UseJsDelivrCdn =>
            !string.Equals(
                Environment.GetEnvironmentVariable("USE_JSDELIVR_CDN"), 
                "false", 
                StringComparison.OrdinalIgnoreCase);
        
        public static bool IsDevMode =>
            string.Equals(
                Environment.GetEnvironmentVariable("AMT_DEV_MODE"),
                "true",
                StringComparison.OrdinalIgnoreCase);
        
        public static string? GitHubToken =>
            Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        
        
        public static string RawGitHubBase =>
            $"https://raw.githubusercontent.com/{GitHubOwner}/{GitHubModsRepo}/{GitHubBranch}";
        
        public static string JsDelivrBase =>
            $"https://cdn.jsdelivr.net/gh/{GitHubOwner}/{GitHubModsRepo}@{GitHubBranch}";
        
        public static string R2CdnBase =>
            Environment.GetEnvironmentVariable("R2_CDN_BASE") ?? "https://cdn.ardysamods.my.id";
        
        public static string ContentBase => R2CdnBase;
        
        public static string GitHubDownloadBase =>
            $"https://github.com/{GitHubOwner}/{GitHubModsRepo}/raw/{GitHubBranch}";
        
        public static string ModsPackReleasesApi =>
            $"https://api.github.com/repos/{GitHubOwner}/{GitHubModsRepo}/releases/latest";
        
        public static string ToolsReleasesApi =>
            $"https://api.github.com/repos/{GitHubOwner}/{GitHubToolsRepo}/releases/latest";

        public static string ToolsReleasesListApi =>
            $"https://api.github.com/repos/{GitHubOwner}/{GitHubToolsRepo}/releases?per_page=30";

        public static string WebsiteBase =>
            (Environment.GetEnvironmentVariable("AMT_WEBSITE_BASE") ?? "https://ardysamods.my.id").TrimEnd('/');

        public static string ModsPackUpdatesUrl => $"{WebsiteBase}/updates.json";

        public static string WhatsNewFeedUrl =>
            Environment.GetEnvironmentVariable("AMT_WHATSNEW_FEED") ?? $"{R2CdnBase}/config/whatsnew.json";


        
        
        public static string BuildRawUrl(string path) =>
            $"{ContentBase}/{path.TrimStart('/')}";
        
        public static string BuildFreshUrl(string path) =>
            BuildRawUrl(path);
        
        public static string BuildDownloadUrl(string path) => 
            $"{GitHubDownloadBase}/{path.TrimStart('/')}";
        
        public static string ConvertToFastUrl(string? url)
        {
            if (string.IsNullOrEmpty(url))
                return url ?? string.Empty;
            
            const string rawPrefix = "https://raw.githubusercontent.com/";
            if (!url.StartsWith(rawPrefix, StringComparison.OrdinalIgnoreCase))
                return url;
            
            try
            {
                var (owner, repo, branch, path) = ParseRawGitHubUrl(url);
                if (string.IsNullOrEmpty(path))
                    return url;
                
                if (owner.Equals(GitHubOwner, StringComparison.OrdinalIgnoreCase) &&
                    repo.Equals(GitHubModsRepo, StringComparison.OrdinalIgnoreCase) &&
                    branch.Equals(GitHubBranch, StringComparison.OrdinalIgnoreCase))
                {
                    return $"{R2CdnBase}/{path}";
                }
                
                if (UseJsDelivrCdn)
                    return $"https://cdn.jsdelivr.net/gh/{owner}/{repo}@{branch}/{path}";
                
                return url;
            }
            catch
            {
                return url;
            }
        }
        
        public static string[] BuildFallbackUrls(string? url)
        {
            if (string.IsNullOrEmpty(url))
                return Array.Empty<string>();
            
            string? repoPath = null;
            var fallbacks = new List<string>(2);
            
            if (url.StartsWith(R2CdnBase, StringComparison.OrdinalIgnoreCase))
            {
                repoPath = url.Substring(R2CdnBase.Length).TrimStart('/');
                fallbacks.Add($"{JsDelivrBase}/{repoPath}");
                fallbacks.Add($"{RawGitHubBase}/{repoPath}");
            }
            else if (url.StartsWith("https://raw.githubusercontent.com/", StringComparison.OrdinalIgnoreCase))
            {
                var parsed = ParseRawGitHubUrl(url);
                if (!string.IsNullOrEmpty(parsed.Path) &&
                    parsed.Owner.Equals(GitHubOwner, StringComparison.OrdinalIgnoreCase) &&
                    parsed.Repo.Equals(GitHubModsRepo, StringComparison.OrdinalIgnoreCase))
                {
                    repoPath = parsed.Path;
                    fallbacks.Add($"{R2CdnBase}/{repoPath}");
                    fallbacks.Add($"{JsDelivrBase}/{repoPath}");
                }
            }
            else if (url.StartsWith("https://cdn.jsdelivr.net/gh/", StringComparison.OrdinalIgnoreCase))
            {
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
                            fallbacks.Add($"{R2CdnBase}/{repoPath}");
                            fallbacks.Add($"{RawGitHubBase}/{repoPath}");
                        }
                    }
                }
                catch { }
            }
            
            return fallbacks.ToArray();
        }
        
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
        
        
        public static bool IsConfigured => 
            !string.IsNullOrEmpty(GitHubOwner);
        
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

