/*
 * Copyright (C) 2026 Ardysa
 * Licensed under GPL v3
 */

using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace ArdysaModsTools.Installer.Services
{
    public record InstallProgress(int Percent, string Status);

    public sealed class InstallerService
    {
        private const string PayloadResourceName = "payload.zip";
        private const string AppExeName = "ArdysaModsTools.exe";

        public static string GetDefaultInstallPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ArdysaModsTools");
        }

        public static string NormalizeInstallPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Install path is empty.");

            var full = Path.GetFullPath(path.Trim());
            if (!Path.IsPathRooted(path.Trim()))
                throw new ArgumentException("Install path must be absolute (e.g. C:\\ArdysaModsTools).");

            var leaf = Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (!string.Equals(leaf, "ArdysaModsTools", StringComparison.OrdinalIgnoreCase))
                full = Path.Combine(full, "ArdysaModsTools");

            return full;
        }

        public static long GetEmbeddedPayloadSize()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = FindPayloadResourceName(assembly);
            if (resourceName == null) return 0;

            using var stream = assembly.GetManifestResourceStream(resourceName);
            return stream?.Length ?? 0;
        }

        public async Task ExtractPayloadAsync(
            string targetPath,
            IProgress<InstallProgress> progress,
            CancellationToken ct = default,
            string? previousInstallPath = null)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = FindPayloadResourceName(assembly)
                ?? throw new InvalidOperationException(
                    "Embedded payload not found. The installer may be corrupted.");

            using var resourceStream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException(
                    "Cannot read embedded payload. The installer may be corrupted.");

            var tempZip = Path.GetTempFileName();
            var stagingDir = Path.Combine(Path.GetTempPath(), $"ArdysaModsTools_Install_{Guid.NewGuid():N}");

            try
            {
                progress.Report(new InstallProgress(5, "Preparing files..."));

                using (var tempStream = File.Create(tempZip))
                {
                    await resourceStream.CopyToAsync(tempStream, ct);
                }

                progress.Report(new InstallProgress(15, "Extracting files..."));
                Directory.CreateDirectory(stagingDir);

                await ExtractZipWithProgressAsync(tempZip, stagingDir, progress, ct);

                progress.Report(new InstallProgress(85, "Verifying files..."));

                var stagedExe = Path.Combine(stagingDir, AppExeName);
                if (!File.Exists(stagedExe))
                {
                    throw new FileNotFoundException(
                        $"Installation verification failed: {AppExeName} not found in extracted payload.");
                }

                if (previousInstallPath != null && Directory.Exists(previousInstallPath))
                {
                    progress.Report(new InstallProgress(88, "Removing previous version..."));
                    CleanupOldInstallation(previousInstallPath);
                }

                progress.Report(new InstallProgress(90, "Installing to destination..."));
                Directory.CreateDirectory(targetPath);

                MoveDirectoryContents(stagingDir, targetPath);

                progress.Report(new InstallProgress(100, "Complete!"));
            }
            finally
            {
                try { File.Delete(tempZip); } catch { }
                try { if (Directory.Exists(stagingDir)) Directory.Delete(stagingDir, true); } catch { }
            }
        }

        public static void CleanupOldInstallation(string installPath)
        {
            if (!Directory.Exists(installPath)) return;
            if (UninstallService.IsDangerousDeleteTarget(installPath)) return;

            try
            {
                foreach (var file in Directory.GetFiles(installPath, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }
                    catch
                    {
                    }
                }

                foreach (var dir in Directory.GetDirectories(installPath, "*", SearchOption.AllDirectories)
                    .OrderByDescending(d => d.Length))
                {
                    try { Directory.Delete(dir, false); } catch { }
                }
            }
            catch
            {
            }
        }


        private static string? FindPayloadResourceName(Assembly assembly)
        {
            return assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(PayloadResourceName, StringComparison.OrdinalIgnoreCase));
        }

        private static void MoveDirectoryContents(string sourceDir, string targetDir)
        {
            foreach (var sourceFile in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceDir, sourceFile);
                var targetFile = Path.Combine(targetDir, relativePath);
                var targetFileDir = Path.GetDirectoryName(targetFile);

                if (targetFileDir != null)
                    Directory.CreateDirectory(targetFileDir);

                if (File.Exists(targetFile))
                {
                    try
                    {
                        File.SetAttributes(targetFile, FileAttributes.Normal);
                        File.Delete(targetFile);
                    }
                    catch
                    {
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

                var rootPrefix = Path.GetFullPath(targetPath)
                    .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

                foreach (var entry in archive.Entries)
                {
                    ct.ThrowIfCancellationRequested();

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        processed++;
                        continue;
                    }

                    var destinationPath = Path.GetFullPath(Path.Combine(targetPath, entry.FullName));
                    if (!destinationPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"Payload entry escapes the install directory: {entry.FullName}");
                    }

                    var destinationDir = Path.GetDirectoryName(destinationPath);

                    if (destinationDir != null)
                        Directory.CreateDirectory(destinationDir);

                    entry.ExtractToFile(destinationPath, overwrite: true);

                    processed++;

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
