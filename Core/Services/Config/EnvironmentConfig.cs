using System;
using System.IO;

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
        
        // ============================================================
        // URL Builders
        // ============================================================
        
        /// <summary>
        /// Base URL for raw GitHub content.
        /// </summary>
        public static string RawGitHubBase => 
            $"https://raw.githubusercontent.com/{GitHubOwner}/{GitHubModsRepo}/{GitHubBranch}";
        
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
        
        /// <summary>
        /// Build raw content URL for a specific path.
        /// </summary>
        public static string BuildRawUrl(string path) => 
            $"{RawGitHubBase}/{path.TrimStart('/')}";
        
        /// <summary>
        /// Build download URL for a specific path.
        /// </summary>
        public static string BuildDownloadUrl(string path) => 
            $"{GitHubDownloadBase}/{path.TrimStart('/')}";
        
        // ============================================================
        // Validation
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
