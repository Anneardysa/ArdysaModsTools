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
    public enum HeroBadgeShow
    {
        Keep,
        Tier,
        Hide
    }

    public enum HeroBadgeLevel { Keep, Hide, Custom }

    public enum HeroBadgeProgress { Keep, Max, Custom }

    public sealed record HeroBadgeRequest(
        HeroBadgeShow Show = HeroBadgeShow.Keep,
        int Tier = 5,
        HeroBadgeLevel Level = HeroBadgeLevel.Keep,
        int CustomLevel = 30,
        HeroBadgeProgress Progress = HeroBadgeProgress.Keep,
        int CustomProgress = 100,
        int? OnlyTier = null);

    public sealed record HeroBadgeState(bool Applied, HeroBadgeRequest? Request);
}
