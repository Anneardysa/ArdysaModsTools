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

namespace ArdysaModsTools.Core.Models
{
    public enum SetupCheckId
    {
        SignatureMatchesGameInfo,

        SearchPathsMounted,

        NotForcedToRunAsAdmin
    }

    public enum SetupCheckState
    {
        Pass,

        Fail,

        Unknown
    }

    public record SetupCheck
    {
        public SetupCheckId Id { get; init; }

        public SetupCheckState State { get; init; }

        public string DetailKey { get; init; } = "";

        public object? DetailVars { get; init; }

        public string? Diagnostic { get; init; }

        public bool CanAutoFix { get; init; }

        public ModStatus FailStatus { get; init; } = ModStatus.NeedUpdate;
    }

    public record SetupVerificationResult
    {
        public IReadOnlyList<SetupCheck> Checks { get; init; } = Array.Empty<SetupCheck>();

        public bool AllPassed => !Checks.Any(c => c.State == SetupCheckState.Fail);

        public SetupCheck? FirstFailure => Checks.FirstOrDefault(c => c.State == SetupCheckState.Fail);

        public static SetupVerificationResult Empty { get; } = new();
    }
}
