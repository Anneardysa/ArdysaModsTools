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
using System.Text.Json.Serialization;

namespace ArdysaModsTools.Core.Models
{
    public sealed class ServerConnectionResult
    {
        [JsonPropertyName("serverKey")]
        public string ServerKey { get; init; } = "";

        [JsonPropertyName("serverName")]
        public string ServerName { get; init; } = "";

        [JsonPropertyName("baseUrl")]
        public string BaseUrl { get; init; } = "";

        [JsonPropertyName("isReachable")]
        public bool IsReachable { get; init; }

        [JsonPropertyName("latencyMs")]
        public long LatencyMs { get; init; }

        [JsonPropertyName("jitterMs")]
        public long JitterMs { get; init; }

        [JsonPropertyName("downloadSpeedKBps")]
        public long DownloadSpeedKBps { get; init; }

        [JsonPropertyName("downloadSpeedMBps")]
        public double DownloadSpeedMBps => Math.Round(DownloadSpeedKBps / 1024.0, 2);

        [JsonPropertyName("peakSpeedMBps")]
        public double PeakSpeedMBps { get; init; }

        [JsonPropertyName("stabilityPercent")]
        public int StabilityPercent { get; init; }

        [JsonPropertyName("dataSampledMB")]
        public double DataSampledMB { get; init; }

        [JsonPropertyName("status")]
        public string Status { get; init; } = "online";

        [JsonPropertyName("errorDetail")]
        public string? ErrorDetail { get; init; }

        [JsonPropertyName("isRecommended")]
        public bool IsRecommended { get; init; }

        [JsonPropertyName("qualityScore")]
        public int QualityScore { get; init; }
    }

    public sealed class ConnectionTestReport
    {
        [JsonPropertyName("servers")]
        public List<ServerConnectionResult> Servers { get; init; } = new();

        [JsonPropertyName("recommendedServerKey")]
        public string RecommendedServerKey { get; init; } = "auto";

        [JsonPropertyName("recommendedServerName")]
        public string RecommendedServerName { get; init; } = "";

        [JsonPropertyName("diagnosticMessage")]
        public string DiagnosticMessage { get; init; } = "";

        [JsonPropertyName("diagnosticSeverity")]
        public string DiagnosticSeverity { get; init; } = "info";

        [JsonPropertyName("testedAt")]
        public DateTime TestedAt { get; init; } = DateTime.UtcNow;
    }

    public sealed class ConnectionTestProgress
    {
        [JsonPropertyName("stage")]
        public string Stage { get; init; } = "";

        [JsonPropertyName("currentServerName")]
        public string CurrentServerName { get; init; } = "";

        [JsonPropertyName("percent")]
        public int Percent { get; init; }

        [JsonPropertyName("message")]
        public string Message { get; init; } = "";
    }
}
