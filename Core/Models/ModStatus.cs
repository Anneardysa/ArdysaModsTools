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
    using System;
    using System.Drawing;

    public enum ModStatus
    {
        NotChecked,
        Checking,
        Ready,
        NeedUpdate,
        Disabled,
        NotInstalled,
        Error
    }

    public enum RecommendedAction
    {
        None,
        Install,
        Update,
        Enable,
        Fix
    }

    public record ModStatusInfo
    {
        public ModStatus Status { get; init; }
        
        public string StatusText { get; init; } = "";

        public string StatusTextKey { get; init; } = "";

        public string Description { get; init; } = "";

        public string DescriptionKey { get; init; } = "";

        public object? DescriptionVars { get; init; }
        
        public RecommendedAction Action { get; init; }
        
        public string ActionButtonText { get; init; } = "";
        
        public string? Version { get; init; }
        
        public DateTime? LastModified { get; init; }
        
        public string? ErrorMessage { get; init; }
        
        public Color StatusColor { get; init; } = Color.Gray;
    }

    public static class ModConstants
    {
        public const string ModPatchSHA1 = "162F5CF09FECCB510A3E13097F8045E5BC0B38F4";

        public const string ModPatchLine = @"...\..\..\dota\gameinfo_branchspecific.gi~SHA1:162F5CF09FECCB510A3E13097F8045E5BC0B38F4;CRC:41EFBC8A";
        
        public const string GameInfoMarker = "_Ardysa";
    }
}

