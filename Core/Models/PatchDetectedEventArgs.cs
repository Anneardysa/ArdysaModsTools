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
