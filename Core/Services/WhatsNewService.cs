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
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services.Config;

namespace ArdysaModsTools.Core.Services
{
    /// <summary>
    /// Fetches the in-app "What's New" changelog from the GitHub Releases API
    /// (<see cref="EnvironmentConfig.ToolsReleasesListApi"/>) — the same source the website's
    /// whatsnew page uses. Short timeout + short-lived cache; returns <c>null</c> on failure so the
    /// dialog can show an offline fallback.
    /// </summary>
    public sealed class WhatsNewService
    {
        private static readonly HttpClient _http = CreateClient();

        private static List<ReleaseNote>? _cached;
        private static DateTime _cacheTime = DateTime.MinValue;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            // GitHub requires a User-Agent; the Accept header pins the API version.
            // Authorization is applied per-request (see FetchReleasesJsonAsync) so a rejected
            // token can be dropped for an anonymous retry without rebuilding the client.
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ArdysaModsTools");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return client;
        }

        /// <summary>
        /// Returns the published releases (newest first). Uses a fresh cached copy when available,
        /// otherwise queries GitHub; returns the stale cache / <c>null</c> on failure.
        /// </summary>
        public async Task<List<ReleaseNote>?> GetReleasesAsync(CancellationToken ct = default)
        {
            if (_cached != null && DateTime.UtcNow - _cacheTime < CacheDuration)
                return _cached;

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(10));

                var json = await FetchReleasesJsonAsync(cts.Token).ConfigureAwait(false);

                var releases = Parse(json);
                if (releases != null && releases.Count > 0)
                {
                    _cached = releases;
                    _cacheTime = DateTime.UtcNow;
                    return releases;
                }

                return _cached;
            }
            catch (OperationCanceledException)
            {
                return _cached;
            }
            catch (HttpRequestException)
            {
                return _cached;
            }
        }

        // [AMT:PRO] Public-repo read. A configured GITHUB_TOKEN only raises the rate limit and is
        // optional — if it is rejected (401/403, e.g. an expired PAT) we transparently retry the
        // request anonymously so a stale token can never break What's New on a public repository.
        private static async Task<string?> FetchReleasesJsonAsync(CancellationToken ct)
        {
            var token = EnvironmentConfig.GitHubToken;

            if (!string.IsNullOrWhiteSpace(token))
            {
                using var authed = await SendReleasesRequestAsync(token, ct).ConfigureAwait(false);
                if (authed.IsSuccessStatusCode)
                    return await authed.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                // Only an auth/permission rejection is worth an anonymous retry; surface everything else.
                if (authed.StatusCode != System.Net.HttpStatusCode.Unauthorized &&
                    authed.StatusCode != System.Net.HttpStatusCode.Forbidden)
                    return null;
            }

            using var anon = await SendReleasesRequestAsync(null, ct).ConfigureAwait(false);
            return anon.IsSuccessStatusCode
                ? await anon.Content.ReadAsStringAsync(ct).ConfigureAwait(false)
                : null;
        }

        private static Task<HttpResponseMessage> SendReleasesRequestAsync(string? token, CancellationToken ct)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, EnvironmentConfig.ToolsReleasesListApi);
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }

        /// <summary>
        /// Parses the GitHub releases JSON array into <see cref="ReleaseNote"/>s (drafts excluded),
        /// preserving the API order (newest first). Returns <c>null</c> for empty/invalid input.
        /// Pure and side-effect free — the unit-test seam for this service.
        /// </summary>
        public static List<ReleaseNote>? Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                var dtos = JsonSerializer.Deserialize<List<GitHubReleaseDto>>(json, _jsonOptions);
                if (dtos == null)
                    return null;

                var notes = new List<ReleaseNote>();
                foreach (var d in dtos)
                {
                    if (d == null || d.Draft)
                        continue;

                    var tag = d.TagName ?? "";
                    if (string.IsNullOrWhiteSpace(tag) && string.IsNullOrWhiteSpace(d.Name))
                        continue;

                    notes.Add(new ReleaseNote
                    {
                        Tag = tag,
                        Name = string.IsNullOrWhiteSpace(d.Name) ? tag : d.Name!,
                        Date = d.PublishedAt,
                        Body = d.Body ?? "",
                        HtmlUrl = d.HtmlUrl ?? ""
                    });
                }

                return notes.Count > 0 ? notes : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private sealed class GitHubReleaseDto
        {
            [JsonPropertyName("tag_name")] public string? TagName { get; set; }
            [JsonPropertyName("name")] public string? Name { get; set; }
            [JsonPropertyName("body")] public string? Body { get; set; }
            [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
            [JsonPropertyName("draft")] public bool Draft { get; set; }
            [JsonPropertyName("published_at")] public DateTime? PublishedAt { get; set; }
        }
    }
}
