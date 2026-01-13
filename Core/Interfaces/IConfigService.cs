namespace ArdysaModsTools.Core.Interfaces
{
    /// <summary>
    /// Interface for application configuration management.
    /// </summary>
    public interface IConfigService
    {
        /// <summary>
        /// Gets the last saved Dota 2 target path.
        /// </summary>
        /// <returns>The saved path, or null if not set.</returns>
        string? GetLastTargetPath();

        /// <summary>
        /// Sets the last saved Dota 2 target path.
        /// </summary>
        /// <param name="path">The path to save, or null to clear.</param>
        void SetLastTargetPath(string? path);

        /// <summary>
        /// Gets a configuration value by key.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="key">The configuration key.</param>
        /// <param name="defaultValue">Default value if not found.</param>
        /// <returns>The configuration value.</returns>
        T GetValue<T>(string key, T defaultValue);

        /// <summary>
        /// Sets a configuration value by key.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="key">The configuration key.</param>
        /// <param name="value">The value to set.</param>
        void SetValue<T>(string key, T value);

        /// <summary>
        /// Saves all pending configuration changes.
        /// </summary>
        void Save();
    }
}
