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
using System.Collections.Generic;
using System.Linq;

namespace ArdysaModsTools.Core.Services.Update.Models
{
    public sealed record DeltaFile(string RelPath, string Sha256, long Size);

    public sealed class DeltaPlan
    {
        public required string Version { get; init; }

        public required string TargetDir { get; init; }

        public required string StagingDir { get; init; }

        public required string FilesBaseUrl { get; init; }

        public required IReadOnlyList<DeltaFile> Files { get; init; }

        public required IReadOnlyList<string> Deletions { get; init; }

        public long TotalDownloadBytes => Files.Sum(f => f.Size);
    }
}
