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
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Models;

namespace ArdysaModsTools.Helpers
{
    public static class NetworkHelper
    {
        /// <summary>
        /// Streaming download with progress callback (0..100). Caller should provide HttpClient.
        /// </summary>
        public static async Task DownloadFileAsync(HttpClient client, string url, string destinationPath, Action<int>? progress = null)
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var contentLength = response.Content.Headers.ContentLength;
            await using var networkStream = await response.Content.ReadAsStreamAsync();
            await using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            byte[] buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;
            int lastReported = -1;

            while ((bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fs.WriteAsync(buffer, 0, bytesRead);
                totalRead += bytesRead;

                if (contentLength.HasValue)
                {
                    int pct = (int)((totalRead * 100L) / contentLength.Value);
                    if (pct != lastReported)
                    {
                        lastReported = pct;
                        progress?.Invoke(pct);
                    }
                }
            }

            // Ensure 100 is reported if contentLength was unknown
            progress?.Invoke(100);
        }

        /// <summary>
        /// Simple wrapper to get byte[] using provided HttpClient.
        /// </summary>
        public static async Task<byte[]> GetByteArrayAsync(HttpClient client, string url)
        {
            return await client.GetByteArrayAsync(url);
        }

        /// <summary>
        /// Fetch latest release info (tag + first asset download URL) from a GitHub releases API response.
        /// Returns null on parse/fetch failure.
        /// </summary>
        public static async Task<VersionInfo?> GetLatestReleaseAsync(HttpClient client, string apiUrl)
        {
            try
            {
                using var resp = await client.GetAsync(apiUrl);
                resp.EnsureSuccessStatusCode();
                string json = await resp.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string tag = root.GetProperty("tag_name").GetString() ?? "";
                string downloadUrl = "";

                if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array && assets.GetArrayLength() > 0)
                {
                    var first = assets[0];
                    if (first.TryGetProperty("browser_download_url", out var durl))
                        downloadUrl = durl.GetString() ?? "";
                }

                DateTime? published = null;
                if (root.TryGetProperty("published_at", out var p) && p.ValueKind == JsonValueKind.String)
                {
                    if (DateTime.TryParse(p.GetString(), out var dt)) published = dt;
                }

                return new VersionInfo { TagName = tag, DownloadUrl = downloadUrl, PublishedAt = published };
            }
            catch
            {
                return null;
            }
        }
    }
}

