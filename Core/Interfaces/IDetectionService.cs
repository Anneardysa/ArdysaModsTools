using System.Threading.Tasks;

namespace ArdysaModsTools.Core.Interfaces
{
    /// <summary>
    /// Interface for Dota 2 installation detection operations.
    /// </summary>
    public interface IDetectionService
    {
        /// <summary>
        /// Automatically detect Dota 2 installation folder via registry and Steam libraries.
        /// </summary>
        /// <returns>The detected Dota 2 path, or null if not found.</returns>
        Task<string?> AutoDetectAsync();

        /// <summary>
        /// Opens a folder picker dialog for manual Dota 2 folder selection.
        /// </summary>
        /// <returns>The selected path, or null if cancelled or invalid.</returns>
        string? ManualDetect();
    }
}
