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
    /// <summary>
    /// Event arguments for when a Dota 2 patch is detected.
    /// </summary>
    public record PatchDetectedEventArgs
    {
        /// <summary>Previous Dota version before the update.</summary>
        public string OldVersion { get; init; } = "";
        
        /// <summary>New Dota version after the update.</summary>
        public string NewVersion { get; init; } = "";
        
        /// <summary>Previous DIGEST hash from dota.signatures.</summary>
        public string OldDigest { get; init; } = "";
        
        /// <summary>New DIGEST hash from dota.signatures.</summary>
        public string NewDigest { get; init; } = "";
        
        /// <summary>When the patch was detected.</summary>
        public DateTime DetectedAt { get; init; } = DateTime.Now;
        
        /// <summary>Whether re-patching is required for mods to work.</summary>
        public bool RequiresRepatch { get; init; }
        
        /// <summary>Human-readable summary of what changed.</summary>
        public string ChangeSummary => GetChangeSummary();
        
        private string GetChangeSummary()
        {
            var changes = new List<string>();
            
            if (!string.Equals(OldVersion, NewVersion, StringComparison.OrdinalIgnoreCase))
            {
                changes.Add($"Version: {OldVersion} → {NewVersion}");
            }
            
            if (!string.Equals(OldDigest, NewDigest, StringComparison.OrdinalIgnoreCase))
            {
                var oldShort = OldDigest.Length > 8 ? OldDigest[..8] : OldDigest;
                var newShort = NewDigest.Length > 8 ? NewDigest[..8] : NewDigest;
                changes.Add($"DIGEST: {oldShort}... → {newShort}...");
            }
            
            return changes.Count > 0 
                ? string.Join("; ", changes) 
                : "No significant changes detected";
        }
    }
}

