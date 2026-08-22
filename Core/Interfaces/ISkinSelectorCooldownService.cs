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

namespace ArdysaModsTools.Core.Interfaces
{
    public enum SkinSelectorLockReason
    {
        None = 0,
        Cooldown = 1,
        DailyLimitReached = 2,
        ClockAnomaly = 3
    }

    public sealed class SkinSelectorCooldownStatus
    {
        public bool IsActive { get; init; }
        public TimeSpan Remaining { get; init; }
        public TimeSpan TotalDuration { get; init; }
        public DateTime? LastGenerationTimeUtc { get; init; }
        public int DailyGenerationsUsed { get; init; }
        public int DailyGenerationsMax { get; init; } = 0;
        public bool IsDailyLimitReached { get; init; }
        public SkinSelectorLockReason LockReason { get; init; }
    }

    public interface ISkinSelectorCooldownService
    {
        TimeSpan CooldownDuration { get; }

        int MaxDailyGenerations { get; }

        bool IsOnCooldown(out TimeSpan remaining);

        bool IsOnCooldown(out TimeSpan remaining, out SkinSelectorLockReason reason);

        SkinSelectorCooldownStatus GetStatus();

        void RecordGeneration();

        void ResetCooldown();
    }
}
