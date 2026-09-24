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
namespace ArdysaModsTools.Models
{
    public sealed record SetNotice(string Type, string? Anim = null, string? Text = null)
    {
        public const string AnimDemoOnly = "anim_demo_only";

        public const string AnimBug = "anim_bug";

        public const string Custom = "custom";

        public static bool IsKnownType(string type) =>
            type is AnimDemoOnly or AnimBug or Custom;
    }
}
