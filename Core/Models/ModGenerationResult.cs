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

namespace ArdysaModsTools.Core.Models
{
    /// <summary>
    /// Represents the result of a mod generation operation.
    /// Used to communicate results from child forms back to MainForm for logging.
    /// </summary>
    public record ModGenerationResult
    {
        /// <summary>Whether the generation was successful.</summary>
        public bool Success { get; init; }
        
        /// <summary>Type of generation performed.</summary>
        public GenerationType Type { get; init; }
        
        /// <summary>Mode used for Misc generation (null for other types).</summary>
        public MiscGenerationMode? MiscMode { get; init; }
        
        /// <summary>Number of options/selections applied.</summary>
        public int OptionsCount { get; init; }
        
        /// <summary>Duration of the generation operation.</summary>
        public TimeSpan Duration { get; init; }
        
        /// <summary>Error message if generation failed.</summary>
        public string? ErrorMessage { get; init; }
        
        /// <summary>Additional details for logging.</summary>
        public string? Details { get; init; }
    }

    /// <summary>
    /// Type of mod generation operation.
    /// </summary>
    public enum GenerationType
    {
        /// <summary>Miscellaneous mods generation.</summary>
        Miscellaneous,
        
        /// <summary>Hero skin selector generation.</summary>
        SkinSelector,
        
        /// <summary>ModsPack install from cloud.</summary>
        ModsPackInstall,
        
        /// <summary>Manual VPK install.</summary>
        ManualInstall
    }
}
