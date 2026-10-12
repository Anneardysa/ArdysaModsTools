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
using System.Linq;
using System.Text.Json.Serialization;

namespace ArdysaModsTools.Core.Models
{
    public enum AppCheckId
    {
        WebView2Runtime,

        WebView2Profile,

        WebView2Leftovers,

        AppDataWritable,

        InstallFolderWritable
    }

    public sealed record AppCheck
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = "";

        [JsonPropertyName("state")]
        public string State { get; init; } = "";

        [JsonPropertyName("titleKey")]
        public string TitleKey { get; init; } = "";

        [JsonPropertyName("detailKey")]
        public string DetailKey { get; init; } = "";

        [JsonPropertyName("detailVars")]
        public object? DetailVars { get; init; }

        [JsonPropertyName("diagnostic")]
        public string? Diagnostic { get; init; }

        [JsonPropertyName("canAutoFix")]
        public bool CanAutoFix { get; init; }

        [JsonPropertyName("needsConfirm")]
        public bool NeedsConfirm { get; init; }

        [JsonPropertyName("appliesOnRestart")]
        public bool AppliesOnRestart { get; init; }
    }

    public sealed record AppRepairReport
    {
        [JsonPropertyName("checks")]
        public IReadOnlyList<AppCheck> Checks { get; init; } = Array.Empty<AppCheck>();

        [JsonPropertyName("scannedAt")]
        public DateTime ScannedAt { get; init; } = DateTime.UtcNow;

        [JsonPropertyName("fixableIds")]
        public IReadOnlyList<string> FixableIds =>
            Checks.Where(c => c.CanAutoFix).Select(c => c.Id).ToArray();

        [JsonPropertyName("hasFailures")]
        public bool HasFailures => Checks.Any(c => c.State == nameof(SetupCheckState.Fail).ToLowerInvariant());
    }

    public sealed record AppRepairProgress
    {
        [JsonPropertyName("percent")]
        public int Percent { get; init; }

        [JsonPropertyName("messageKey")]
        public string MessageKey { get; init; } = "";

        [JsonPropertyName("messageVars")]
        public object? MessageVars { get; init; }
    }
}
