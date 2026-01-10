namespace ArdysaModsTools.Core.Services.Update
{
    /// <summary>
    /// Represents how the application was installed.
    /// </summary>
    public enum InstallationType
    {
        /// <summary>
        /// Unable to determine installation type.
        /// </summary>
        Unknown,

        /// <summary>
        /// Installed via the setup installer (Inno Setup).
        /// Located in Program Files, has uninstaller, requires admin for updates.
        /// </summary>
        Installer,

        /// <summary>
        /// Portable/standalone version extracted from ZIP.
        /// Can be anywhere, no uninstaller, updates in-place without admin.
        /// </summary>
        Portable
    }
}
