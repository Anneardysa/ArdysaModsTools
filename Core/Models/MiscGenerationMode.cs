namespace ArdysaModsTools.Core.Models
{
    /// <summary>
    /// Specifies the generation mode for Misc mods.
    /// </summary>
    public enum MiscGenerationMode
    {
        /// <summary>
        /// Apply modifications on top of existing game VPK.
        /// </summary>
        AddToCurrent,

        /// <summary>
        /// Generate clean VPK from Original.zip (removes existing mods).
        /// </summary>
        GenerateOnly
    }
}
