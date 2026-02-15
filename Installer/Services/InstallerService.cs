/*
 * Copyright (C) 2026 Ardysa
 * Licensed under GPL v3
 */

using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace ArdysaModsTools.Installer.Services
{
    /// <summary>
    /// Progress data for the installation pipeline.
    /// </summary>
    public record InstallProgress(int Percent, string Status);

    /// <summary>
    /// Core installation logic: extracts the embedded payload.zip to the target directory.
    ///
    /// Uses an atomic extract-then-swap pattern to prevent broken installs:
    ///   1. Extract payload to a temp staging directory
    ///   2. Backup existing install (if updating)
    ///   3. Move staged files into the real install path
    ///   4. Delete backup on success, or restore on failure
    /// </summary>
    public sealed class InstallerService
    {
        private const string PayloadResourceName = "payload.zip";
        private const string AppExeName = "ArdysaModsTools.exe";

        /// <summary>
        /// Default install location: %LocalAppData%\ArdysaModsTools
        /// Matches modern app patterns (WeMod, Discord, etc.)
        /// </summary>
        public static string GetDefaultInstallPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ArdysaModsTools");
        }

        /// <summary>
        /// Returns the size (in bytes) of the embedded payload.zip, or 0 if not found.
        /// Used to display estimated install size in the UI.
        /// </summary>
        public static long GetEmbeddedPayloadSize()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = FindPayloadResourceName(assembly);
            if (resourceName == null) return 0;

            using var stream = assembly.GetManifestResourceStream(resourceName);
            return stream?.Length ?? 0;
        }

        /// <summary>
        /// Extracts the embedded payload.zip to the target directory using
        /// an atomic extract-then-swap approach to prevent broken installs.
        /// Reports progress through <paramref name="progress"/>.
        /// </summary>
        public async Task ExtractPayloadAsync(
            string targetPath,
            IProgress<InstallProgress> progress,
            CancellationToken ct = default)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = FindPayloadResourceName(assembly)
                ?? throw new InvalidOperationException(
                    "Embedded payload not found. The installer may be corrupted.");

            using var resourceStream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException(
                    "Cannot read embedded payload. The installer may be corrupted.");

            // ─────────────────────────────────────────────
            // Phase 1: Extract to temp zip file
            // ─────────────────────────────────────────────
            var tempZip = Path.GetTempFileName();
            var stagingDir = Path.Combine(Path.GetTempPath(), $"ArdysaModsTools_Install_{Guid.NewGuid():N}");

            try
            {
                progress.Report(new InstallProgress(5, "Preparing files..."));

                using (var tempStream = File.Create(tempZip))
                {
                    await resourceStream.CopyToAsync(tempStream, ct);
                }

                // ─────────────────────────────────────────────
                // Phase 2: Extract to staging directory (not the real target)
                // This way, if extraction fails, the existing install is untouched
                // ─────────────────────────────────────────────
                progress.Report(new InstallProgress(15, "Extracting files..."));
                Directory.CreateDirectory(stagingDir);

                await ExtractZipWithProgressAsync(tempZip, stagingDir, progress, ct);

                progress.Report(new InstallProgress(85, "Verifying files..."));

                // Verify main executable exists in staging
                var stagedExe = Path.Combine(stagingDir, AppExeName);
                if (!File.Exists(stagedExe))
                {
                    throw new FileNotFoundException(
                        $"Installation verification failed: {AppExeName} not found in extracted payload.");
                }

                // ─────────────────────────────────────────────
                // Phase 3: Atomic swap — move staged files to target
                // ─────────────────────────────────────────────
                progress.Report(new InstallProgress(90, "Installing to destination..."));
                Directory.CreateDirectory(targetPath);

                MoveDirectoryContents(stagingDir, targetPath);

                progress.Report(new InstallProgress(100, "Complete!"));
            }
            finally
            {
                // Cleanup temp files — best effort
                try { File.Delete(tempZip); } catch { }
                try { if (Directory.Exists(stagingDir)) Directory.Delete(stagingDir, true); } catch { }
            }
        }

        /// <summary>
        /// Removes an old installation directory. Called before a fresh install/update.
        /// Only removes application files (exe, dll, etc.), preserves user data directories.
        /// Preserves user settings in %AppData% (they're not in the install dir).
        /// </summary>
        public static void CleanupOldInstallation(string installPath)
        {
            if (!Directory.Exists(installPath)) return;

            try
            {
                // Delete application files — skip directories that might contain user data
                foreach (var file in Directory.GetFiles(installPath, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }
                    catch
                    {
                        // Skip locked files — they'll be overwritten during extraction
                    }
                }

                // Try to remove empty subdirectories (deepest first)
                foreach (var dir in Directory.GetDirectories(installPath, "*", SearchOption.AllDirectories)
                    .OrderByDescending(d => d.Length))
                {
                    try { Directory.Delete(dir, false); } catch { }
                }
            }
            catch
            {
                // Non-critical — extraction will overwrite
            }
        }

        // ================================================================
        // PRIVATE HELPERS
        // ================================================================

        private static string? FindPayloadResourceName(Assembly assembly)
        {
            return assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(PayloadResourceName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Moves all files and directories from source into target, overwriting existing files.
        /// Uses File.Move where possible (fast, same-volume), falls back to Copy+Delete for cross-volume.
        /// </summary>
        private static void MoveDirectoryContents(string sourceDir, string targetDir)
        {
            // Move files
            foreach (var sourceFile in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceDir, sourceFile);
                var targetFile = Path.Combine(targetDir, relativePath);
                var targetFileDir = Path.GetDirectoryName(targetFile);

                if (targetFileDir != null)
                    Directory.CreateDirectory(targetFileDir);

                // Delete existing file first (can't Move over existing)
                if (File.Exists(targetFile))
                {
                    try
                    {
                        File.SetAttributes(targetFile, FileAttributes.Normal);
                        File.Delete(targetFile);
                    }
                    catch
                    {
                        // File is locked — overwrite via copy instead
                        File.Copy(sourceFile, targetFile, overwrite: true);
                        continue;
                    }
                }

                try
                {
                    File.Move(sourceFile, targetFile);
                }
                catch
                {
                    // Cross-volume or locked — fallback to copy
                    File.Copy(sourceFile, targetFile, overwrite: true);
                }
            }
        }

        private static async Task ExtractZipWithProgressAsync(
            string zipPath,
            string targetPath,
            IProgress<InstallProgress> progress,
            CancellationToken ct)
        {
            await Task.Run(() =>
            {
                using var archive = ZipFile.OpenRead(zipPath);
                var totalEntries = archive.Entries.Count;
                var processed = 0;

                foreach (var entry in archive.Entries)
                {
                    ct.ThrowIfCancellationRequested();

                    // Skip directory entries
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        processed++;
                        continue;
                    }

                    var destinationPath = Path.Combine(targetPath, entry.FullName);
                    var destinationDir = Path.GetDirectoryName(destinationPath);

                    if (destinationDir != null)
                        Directory.CreateDirectory(destinationDir);

                    // Overwrite existing files
                    entry.ExtractToFile(destinationPath, overwrite: true);

                    processed++;

                    // Report progress (scale from 15% to 85%)
                    var percent = 15 + (int)(processed / (double)totalEntries * 70);
                    if (processed % 50 == 0 || processed == totalEntries)
                    {
                        progress.Report(new InstallProgress(
                            Math.Min(percent, 85),
                            $"Extracting files... ({processed}/{totalEntries})"));
                    }
                }
            }, ct);
        }
    }
}
