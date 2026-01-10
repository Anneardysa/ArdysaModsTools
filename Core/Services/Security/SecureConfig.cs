using System;
using System.Collections.Generic;
using ArdysaModsTools.Core.Services.Config;

namespace ArdysaModsTools.Core.Services.Security
{
    /// <summary>
    /// Centralized secure configuration storage.
    /// All sensitive values are loaded from environment variables.
    /// See .env.example for required configuration.
    /// </summary>
    public static class SecureConfig
    {
        // Passphrase loaded from environment
        private static string Passphrase => EnvironmentConfig.Passphrase;

        /// <summary>
        /// Gets the archive password for encrypted mod files.
        /// </summary>
        public static string GetArchivePassword()
        {
            return EnvironmentConfig.ArchivePassword;
        }

        /// <summary>
        /// Gets the raw GitHub content base URL.
        /// </summary>
        public static string GetGitHubRawBase()
        {
            return $"https://raw.githubusercontent.com";
        }

        /// <summary>
        /// Gets the GitHub API base URL.
        /// </summary>
        public static string GetGitHubApiBase()
        {
            return "https://api.github.com";
        }

        /// <summary>
        /// Gets the main GitHub base URL.
        /// </summary>
        public static string GetGitHubBase()
        {
            return "https://github.com";
        }

        /// <summary>
        /// Gets the ModsPack repository path.
        /// </summary>
        public static string GetModsPackRepoPath()
        {
            return $"/{EnvironmentConfig.GitHubOwner}/{EnvironmentConfig.GitHubModsRepo}";
        }

        /// <summary>
        /// Gets the ArdysaModsTools repository path.
        /// </summary>
        public static string GetToolsRepoPath()
        {
            return $"/{EnvironmentConfig.GitHubOwner}/{EnvironmentConfig.GitHubToolsRepo}";
        }

        /// <summary>
        /// Constructs a raw GitHub URL for ModsPack assets.
        /// </summary>
        /// <param name="branch">Branch name (e.g., "main")</param>
        /// <param name="path">Path within repo (e.g., "Assets/heroes.json")</param>
        public static string BuildModsPackRawUrl(string branch, string path)
        {
            return $"{GetGitHubRawBase()}{GetModsPackRepoPath()}/{branch}/{path}";
        }

        /// <summary>
        /// Constructs a GitHub raw URL for downloading large files.
        /// </summary>
        /// <param name="branch">Branch name</param>
        /// <param name="path">File path</param>
        public static string BuildModsPackDownloadUrl(string branch, string path)
        {
            return $"{GetGitHubBase()}{GetModsPackRepoPath()}/raw/{branch}/{path}";
        }

        /// <summary>
        /// Constructs the GitHub API URL for latest release.
        /// </summary>
        public static string BuildLatestReleaseApiUrl()
        {
            return $"{GetGitHubApiBase()}/repos{GetToolsRepoPath()}/releases/latest";
        }

        /// <summary>
        /// Constructs the ModsPack releases API URL.
        /// </summary>
        public static string BuildModsPackReleasesApiUrl()
        {
            return $"{GetGitHubApiBase()}/repos{GetModsPackRepoPath()}/releases/latest";
        }

        #region Utility Methods

        /// <summary>
        /// Encrypts a value for storage in this class.
        /// Call this during development to generate encrypted values.
        /// </summary>
        public static string EncryptForStorage(string plainText)
        {
            return StringProtection.EncryptPortable(plainText, Passphrase);
        }

        /// <summary>
        /// Decrypts a stored encrypted value.
        /// </summary>
        public static string DecryptStored(string encrypted)
        {
            return StringProtection.DecryptPortable(encrypted, Passphrase);
        }

        #endregion
    }
}
