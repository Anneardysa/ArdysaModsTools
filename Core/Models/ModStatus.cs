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

    /// <summary>
    /// Detailed status of mods installation.
    /// </summary>
    public enum ModStatus
    {
        /// <summary>Initial state, not yet checked.</summary>
        NotChecked,
        /// <summary>Currently checking status (loading state).</summary>
        Checking,
        /// <summary>Mods active and up-to-date.</summary>
        Ready,
        /// <summary>Mods active but Dota was updated, need to re-patch.</summary>
        NeedUpdate,
        /// <summary>Mods installed but not active (gameinfo not patched).</summary>
        Disabled,
        /// <summary>Mods not installed.</summary>
        NotInstalled,
        /// <summary>Error occurred during status check.</summary>
        Error
    }

    /// <summary>
    /// Recommended action based on current status.
    /// </summary>
    public enum RecommendedAction
    {
        /// <summary>No action needed - everything is good.</summary>
        None,
        /// <summary>Install mods for the first time.</summary>
        Install,
        /// <summary>Update/re-patch after Dota update.</summary>
        Update,
        /// <summary>Re-enable mods (patch gameinfo).</summary>
        Enable,
        /// <summary>Fix error condition.</summary>
        Fix
    }

    /// <summary>
    /// Detailed status result with metadata.
    /// </summary>
    public record ModStatusInfo
    {
        /// <summary>Current mod status.</summary>
        public ModStatus Status { get; init; }
        
        /// <summary>Human-readable status text.</summary>
        public string StatusText { get; init; } = "";
        
        /// <summary>Detailed description for user.</summary>
        public string Description { get; init; } = "";
        
        /// <summary>Recommended action for user.</summary>
        public RecommendedAction Action { get; init; }
        
        /// <summary>Action button text (e.g., "Install Now", "Update").</summary>
        public string ActionButtonText { get; init; } = "";
        
        /// <summary>ModsPack version if installed.</summary>
        public string? Version { get; init; }
        
        /// <summary>Last modification date of mod files.</summary>
        public DateTime? LastModified { get; init; }
        
        /// <summary>Error message if status is Error.</summary>
        public string? ErrorMessage { get; init; }
        
        /// <summary>Color for UI display.</summary>
        public Color StatusColor { get; init; } = Color.Gray;
    }

    /// <summary>
    /// Shared constants for mod detection.
    /// </summary>
    public static class ModConstants
    {
        /// <summary>SHA1 hash of our modded gameinfo file.</summary>
        public const string ModPatchSHA1 = "1A9B91FB43FE89AD104B8001282D292EED94584D";
        
        /// <summary>The full patch line added after DIGEST.</summary>
        public const string ModPatchLine = @"...\..\..\dota\gameinfo_branchspecific.gi~SHA1:1A9B91FB43FE89AD104B8001282D292EED94584D;CRC:043F604A";
        
        /// <summary>Marker text in modded gameinfo file.</summary>
        public const string GameInfoMarker = "_Ardysa";
    }
}

