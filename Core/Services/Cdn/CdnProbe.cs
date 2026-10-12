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
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace ArdysaModsTools.Core.Services.Cdn
{
    public readonly record struct CdnProbeResult(
        bool Success,
        HttpStatusCode StatusCode,
        long TotalBytes,
        bool SupportsRange,
        string? ETag,
        string? LastModified);

    public static class CdnProbe
    {
        public static async Task<CdnProbeResult> ProbeAsync(HttpClient client, string url, CancellationToken ct)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Range = new RangeHeaderValue(0, 0);

                using var response = await client
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                    return new CdnProbeResult(false, response.StatusCode, -1, false, null, null);

                long totalBytes = response.Content.Headers.ContentRange?.Length
                    ?? response.Content.Headers.ContentLength
                    ?? -1;

                return new CdnProbeResult(
                    Success: true,
                    StatusCode: response.StatusCode,
                    TotalBytes: totalBytes,
                    SupportsRange: response.StatusCode == HttpStatusCode.PartialContent,
                    ETag: response.Headers.ETag?.Tag,
                    LastModified: response.Content.Headers.LastModified?.ToString("R"));
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                return new CdnProbeResult(false, default, -1, false, null, null);
            }
        }
    }
}
