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
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Services.Update.Models;

namespace ArdysaModsTools.Core.Services.Update
{
    /// <summary>
    /// Update strategy for installer-based installations.
    /// 
    /// Workflow:
    ///   1. Main app downloads new ArdysaModsTools_Setup_*.exe to temp
    ///   2. This strategy launches the installer with --update flag
    ///   3. The WPF installer detects existing install via registry
    ///   4. Installer closes the running app, extracts new payload, re-registers
    ///   5. Installer offers "Launch" button on completion
    /// </summary>
    public class InstallerUpdateStrategy : IUpdateStrategy
    {
        public InstallationType InstallationType => InstallationType.Installer;

        /// <summary>
        /// Asset filename pattern to match in GitHub/R2 release assets.
        /// Matches files like "ArdysaModsTools_Setup_2.1.12-beta.exe".
        /// </summary>
        public string AssetPattern => "_Setup_";

        /// <summary>
        /// The WPF installer writes to HKLM registry and creates shortcuts,
        /// which requires admin privileges.
        /// </summary>
        public bool RequiresAdminRights => true;

        /// <summary>
        /// No pre-flight validation needed — the installer handles
        /// all checks (disk space, running processes, etc.) internally.
        /// </summary>
        public string? ValidateCanUpdate() => null;

        /// <summary>
        /// Launches the downloaded WPF installer with --update flag.
        /// The installer will:
        ///   - Detect existing install path from registry
        ///   - Show update UI with version comparison
        ///   - Close running app, cleanup old files, extract new payload
        ///   - Re-register in Add/Remove Programs
        ///   - Offer "Launch" button on completion
        /// </summary>
        public async Task<UpdateResult> ApplyUpdateAsync(
            string downloadedFilePath,
            Action<int>? onProgress = null,
            Action<string>? onStatusChanged = null)
        {
            try
            {
                if (!File.Exists(downloadedFilePath))
                {
                    return UpdateResult.Failed("Downloaded installer file not found.");
                }

                // Validate the downloaded file is a valid Windows executable (PE format).
                // This catches corrupted or truncated downloads before they cause cryptic errors.
                if (!IsValidPortableExecutable(downloadedFilePath))
                {
                    return UpdateResult.Failed(
                        "The downloaded installer appears to be corrupted. Please try updating again.");
                }

                onStatusChanged?.Invoke("Launching installer...");
                onProgress?.Invoke(80);

                // Launch the WPF installer directly with --update flag.
                // The installer reads the existing install path from registry
                // (HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{GUID}\InstallLocation)
                // so no path argument is needed.
                var startInfo = new ProcessStartInfo
                {
                    FileName = downloadedFilePath,
                    Arguments = "--update",
                    UseShellExecute = true,
                    Verb = "runas", // Elevate — installer needs admin for registry + shortcuts
                };

                Process.Start(startInfo);

                onProgress?.Invoke(100);
                onStatusChanged?.Invoke("Installer started. Closing app...");

                // Give the installer a moment to launch before we exit
                await Task.Delay(800);

                return UpdateResult.Succeeded(requiresRestart: true);
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // ERROR_CANCELLED (1223) — user declined the UAC prompt
                return UpdateResult.Failed("Update cancelled: administrator access was denied.");
            }
            catch (Exception ex)
            {
                return UpdateResult.Failed($"Failed to launch installer: {ex.Message}");
            }
        }
        // ================================================================
        // PRIVATE HELPERS
        // ================================================================

        /// <summary>
        /// Validates that a file is a valid Windows Portable Executable (PE)
        /// by checking the MZ magic bytes and minimum size.
        /// Catches corrupted or truncated downloads before they cause cryptic OS errors.
        /// </summary>
        private static bool IsValidPortableExecutable(string filePath)
        {
            try
            {
                var fileInfo = new System.IO.FileInfo(filePath);

                // Minimum size: any real .NET WPF installer exe will be > 50KB
                if (fileInfo.Length < 50 * 1024)
                    return false;

                // Check PE magic bytes: first 2 bytes must be 'M' 'Z' (0x4D 0x5A)
                using var stream = File.OpenRead(filePath);
                var header = new byte[2];
                if (stream.Read(header, 0, 2) < 2)
                    return false;

                return header[0] == 0x4D && header[1] == 0x5A; // "MZ"
            }
            catch
            {
                return false;
            }
        }
    }
}
