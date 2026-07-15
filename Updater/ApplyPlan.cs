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
using System.Text.Json.Serialization;

namespace ArdysaModsTools.Updater
{
    public sealed class ApplyPlan
    {
        public string Version { get; set; } = "";

        public string TargetDir { get; set; } = "";

        public string StagingDir { get; set; } = "";

        public ApplyFile[] Files { get; set; } = [];

        public string[] Deletions { get; set; } = [];
    }

    public sealed class ApplyFile
    {
        public string RelPath { get; set; } = "";

        public string Sha256 { get; set; } = "";

        public long Size { get; set; }
    }

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(ApplyPlan))]
    internal sealed partial class ApplyJsonContext : JsonSerializerContext
    {
    }
}
