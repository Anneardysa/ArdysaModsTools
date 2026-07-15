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
    public enum ConflictType
    {
        None = 0,

        File = 1,

        Script = 2,

        Asset = 3,

        Configuration = 4
    }

    public enum ConflictSeverity
    {
        None = 0,

        Low = 1,

        Medium = 2,

        High = 3,

        Critical = 4
    }

    public enum ResolutionStrategy
    {
        HigherPriority = 0,

        LowerPriority = 1,

        MostRecent = 2,

        Merge = 3,

        KeepExisting = 4,

        UseNew = 5,

        Interactive = 6
    }
}
