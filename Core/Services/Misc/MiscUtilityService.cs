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
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace ArdysaModsTools.Core.Services
{
    public class MiscUtilityService
    {
        // Files to preserve during temp cleanup  
        private static readonly HashSet<string> PreserveFiles = new(StringComparer.OrdinalIgnoreCase)
        {
            "extraction.log",
            "vuex.json",
            "misc_extraction_log.json",
            "hero_extraction_log.json",
            "digest.txt"
        };

        public async Task CleanupTempFoldersAsync(string targetPath, Action<string> log)
        {
            try
            {
                string tempDir = Path.Combine(targetPath, "game", "_ArdysaMods", "_temp");
                if (!Directory.Exists(tempDir)) return;

                await Task.Delay(200);
                
                // Delete files except preserved ones
                foreach (var file in Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories))
                {
                    if (!PreserveFiles.Contains(Path.GetFileName(file)))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }

                // Clean empty subdirectories
                foreach (var dir in Directory.GetDirectories(tempDir, "*", SearchOption.AllDirectories)
                    .OrderByDescending(d => d.Length)) // Delete deepest first
                {
                    try
                    {
                        if (!Directory.EnumerateFileSystemEntries(dir).Any())
                            Directory.Delete(dir);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                log($"Cleanup failed: {ex.Message}");
            }
        }

        private static readonly HttpClient _httpClient;

        static MiscUtilityService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            _httpClient.Timeout = TimeSpan.FromSeconds(300);

            // GitHub token for API rate limiting (optional - set via GITHUB_TOKEN env var)
            var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (!string.IsNullOrEmpty(githubToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", githubToken);
            }
        }

        public static async Task<HttpResponseMessage?> GetWithRetryAsync(string url, int maxRetries = 3)
        {
            try
            {
                return await Core.Helpers.RetryHelper.ExecuteAsync(async () =>
                {
                    var response = await _httpClient.GetAsync(url).ConfigureAwait(false);
                    
                    // Check for transient status codes
                    if (Core.Helpers.RetryHelper.IsTransientStatusCode(response.StatusCode))
                        throw new HttpRequestException($"Server returned {response.StatusCode}");
                    
                    // 404 is not retryable - return null
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        return null;
                    
                    response.EnsureSuccessStatusCode();
                    return response;
                },
                maxAttempts: maxRetries,
                initialDelayMs: 1000).ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }

        public static async Task<string?> GetStringWithRetryAsync(string url)
        {
            using (var response = await GetWithRetryAsync(url))
            {
                if (response == null) return null;
                return await response.Content.ReadAsStringAsync();
            }
        }

        public static async Task<byte[]?> GetByteArrayWithRetryAsync(string url)
        {
            using (var response = await GetWithRetryAsync(url))
            {
                if (response == null) return null;
                return await response.Content.ReadAsByteArrayAsync();
            }
        }

        public static void SafeLog(string message, Action<string>? uiLogger = null)
        {
            try
            {
                uiLogger?.Invoke(message);
                string logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mod_log.txt");
                File.AppendAllText(logFile, $"{DateTime.Now:HH:mm:ss} - {message}\n");
            }
            catch
            {
                // Ignore log errors
            }
        }

    }
}
