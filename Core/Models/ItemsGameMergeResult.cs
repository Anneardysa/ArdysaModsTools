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
namespace ArdysaModsTools.Core.Models
{
    public enum ItemsGameMergeOutcome
    {
        AlreadyCurrent,

        Merged,

        NothingToMerge,

        Failed
    }

    public record ItemsGameMergeResult
    {
        public ItemsGameMergeOutcome Outcome { get; init; }

        public int Applied { get; init; }

        public int Dropped { get; init; }

        public string? FailureKey { get; init; }

        public string? Diagnostic { get; init; }

        public bool IsPlayable => Outcome is ItemsGameMergeOutcome.AlreadyCurrent
                                          or ItemsGameMergeOutcome.Merged
                                          or ItemsGameMergeOutcome.NothingToMerge;

        internal static ItemsGameMergeResult Fail(string failureKey, string? diagnostic = null) =>
            new() { Outcome = ItemsGameMergeOutcome.Failed, FailureKey = failureKey, Diagnostic = diagnostic };
    }
}
