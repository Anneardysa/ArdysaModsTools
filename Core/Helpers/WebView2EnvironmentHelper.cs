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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace ArdysaModsTools.Core.Helpers
{
    /// <summary>
    /// Centralizes creation of the shared WebView2 environment for every WebView2 form.
    /// The user-data folder lives in %LocalAppData% (sibling of the asset cache) instead of
    /// %TEMP%, so the browser cache and storage survive Windows temp cleanup (Storage Sense,
    /// Disk Cleanup) and app updates — eliminating thumbnail/asset re-downloads on each open.
    /// </summary>
    public static class WebView2EnvironmentHelper
    {
        /// <summary>
        /// Legacy %TEMP% user-data folder used by older builds. Cleaned up once on first use.
        /// </summary>
        private static readonly string LegacyUserDataFolder =
            Path.Combine(Path.GetTempPath(), "ArdysaModsTools.WebView2");

        private static int _legacyCleanupDone;

        /// <summary>
        /// Persistent WebView2 user-data folder: %LocalAppData%\ArdysaModsTools\WebView2.
        /// </summary>
        public static string UserDataFolder { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ArdysaModsTools",
            "WebView2");

        /// <summary>
        /// Creates the shared WebView2 environment rooted at the persistent user-data folder.
        /// Ensures the folder exists and performs a one-time cleanup of the legacy temp folder.
        /// </summary>
        public static async Task<CoreWebView2Environment> CreateEnvironmentAsync()
        {
            EnsureUserDataFolderExists();
            TryCleanupLegacyFolderOnce();
            return await CoreWebView2Environment.CreateAsync(null, UserDataFolder).ConfigureAwait(true);
        }

        private static void EnsureUserDataFolderExists()
        {
            try
            {
                if (!Directory.Exists(UserDataFolder))
                    Directory.CreateDirectory(UserDataFolder);
            }
            catch (Exception ex)
            {
                // Non-fatal: WebView2 will surface a clearer error if the folder is truly unusable.
                System.Diagnostics.Debug.WriteLine(
                    $"[WebView2EnvironmentHelper] Failed to create user-data folder: {ex.Message}");
            }
        }

        /// <summary>
        /// Best-effort, run-once removal of the legacy %TEMP% WebView2 folder so it stops
        /// consuming disk space after the move to the persistent location.
        /// </summary>
        private static void TryCleanupLegacyFolderOnce()
        {
            if (Interlocked.Exchange(ref _legacyCleanupDone, 1) != 0)
                return;

            try
            {
                if (Directory.Exists(LegacyUserDataFolder))
                    Directory.Delete(LegacyUserDataFolder, recursive: true);
            }
            catch
            {
                // Folder may be locked by another instance or already gone — ignore.
            }
        }
    }
}
