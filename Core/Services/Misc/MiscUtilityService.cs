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

            // ⚠️ Tip: move token to environment variable or config file later
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", "URL_TOKEN"); // This sample token : [ghp]_[UIJpAR5kEUE8EBqPw029f0zieeBB8g3Ez6ZN]
        }

        public static async Task<HttpResponseMessage?> GetWithRetryAsync(string url, int maxRetries = 3)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var response = await _httpClient.GetAsync(url);
                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        await Task.Delay(5000);
                        continue;
                    }
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        return null;

                    response.EnsureSuccessStatusCode();
                    return response;
                }
                catch (TaskCanceledException)
                {
                    if (attempt == maxRetries) throw;
                    await Task.Delay(3000);
                }
                catch
                {
                    if (attempt == maxRetries) throw;
                    await Task.Delay(2000);
                }
            }
            return null;
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