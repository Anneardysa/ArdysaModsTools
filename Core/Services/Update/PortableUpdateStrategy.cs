using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Services.Update.Models;

namespace ArdysaModsTools.Core.Services.Update
{
    /// <summary>
    /// Update strategy for portable installations.
    /// Extracts the new version and replaces files in-place via a batch script.
    /// </summary>
    public class PortableUpdateStrategy : IUpdateStrategy
    {
        public InstallationType InstallationType => InstallationType.Portable;

        /// <summary>
        /// Matches files like: AMT-v2.0.1.zip
        /// </summary>
        public string AssetPattern => "AMT-v";

        public bool RequiresAdminRights => false;

        public string? ValidateCanUpdate()
        {
            string installPath = InstallationDetector.GetInstallPath();

            // Check if we have write access to the install directory
            try
            {
                string testFile = Path.Combine(installPath, $".write_test_{Guid.NewGuid()}");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
            }
            catch (UnauthorizedAccessException)
            {
                return $"Cannot write to the application folder: {installPath}\n" +
                       "Please ensure you have write permissions or move the application to a writable location.";
            }
            catch (Exception ex)
            {
                return $"Cannot verify write access: {ex.Message}";
            }

            return null; // Valid
        }

        public async Task<UpdateResult> ApplyUpdateAsync(
            string downloadedFilePath,
            Action<int>? onProgress = null,
            Action<string>? onStatusChanged = null)
        {
            string? tempDir = null;
            string? scriptPath = null;

            try
            {
                if (!File.Exists(downloadedFilePath))
                {
                    return UpdateResult.Failed("Downloaded portable file not found.");
                }

                string currentExe = Process.GetCurrentProcess().MainModule!.FileName;
                // Use the directory of the actual running exe, not AppContext.BaseDirectory
                // This ensures we replace the exe in the correct location
                string installDir = Path.GetDirectoryName(currentExe) ?? AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
                string exeName = Path.GetFileName(currentExe);
                
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Portable update:");
                System.Diagnostics.Debug.WriteLine($"[DEBUG]   currentExe: {currentExe}");
                System.Diagnostics.Debug.WriteLine($"[DEBUG]   installDir: {installDir}");
                System.Diagnostics.Debug.WriteLine($"[DEBUG]   exeName: {exeName}");
#endif
                
                // Create temp directory
                tempDir = Path.Combine(Path.GetTempPath(), $"ArdysaModsTools_Update_{Guid.NewGuid()}");
                Directory.CreateDirectory(tempDir);

                // Determine if this is a single-file exe or zip
                bool isExe = downloadedFilePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
                bool isZip = downloadedFilePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

                if (!isExe && !isZip)
                {
                    return UpdateResult.Failed("Invalid portable file format. Expected .exe or .zip file.");
                }

                string sourceDir;

                if (isExe)
                {
                    // Single-file exe: just need to replace the exe
                    onStatusChanged?.Invoke("Preparing update...");
                    onProgress?.Invoke(30);

                    sourceDir = tempDir;
                    string newExePath = Path.Combine(tempDir, exeName);
                    File.Copy(downloadedFilePath, newExePath, true);
                }
                else
                {
                    // Zip file: extract and find content
                    onStatusChanged?.Invoke("Extracting update...");
                    onProgress?.Invoke(20);

                    string extractDir = Path.Combine(tempDir, "extracted");
                    await Task.Run(() => ZipFile.ExtractToDirectory(downloadedFilePath, extractDir));

                    // Find the actual content directory (might be nested)
                    sourceDir = FindContentDirectory(extractDir);
                }

                onStatusChanged?.Invoke("Preparing update script...");
                onProgress?.Invoke(50);

                // Create the update batch script
                scriptPath = Path.Combine(tempDir, "apply_update.bat");

                string batchContent = CreateUpdateScript(
                    sourceDir: sourceDir,
                    targetDir: installDir,
                    exeName: exeName,
                    tempDir: tempDir,
                    isSingleFile: isExe
                );

                await File.WriteAllTextAsync(scriptPath, batchContent);

                onStatusChanged?.Invoke("Starting update process...");
                onProgress?.Invoke(75);

                // Start the batch script
                Process.Start(new ProcessStartInfo
                {
                    FileName = scriptPath,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                onStatusChanged?.Invoke("Update will complete after restart...");
                onProgress?.Invoke(100);

                // Give the script a moment to start
                await Task.Delay(500);

                return UpdateResult.Succeeded(requiresRestart: true);
            }
            catch (Exception ex)
            {
                // Clean up on failure
                try
                {
                    if (tempDir != null && Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch { }

                return UpdateResult.Failed($"Failed to apply portable update: {ex.Message}");
            }
        }

        /// <summary>
        /// Finds the directory containing the actual application files.
        /// Handles cases where zip extracts to a nested folder.
        /// </summary>
        private string FindContentDirectory(string extractDir)
        {
            // Look for the exe in the extract directory
            var exeFiles = Directory.GetFiles(extractDir, "*.exe", SearchOption.TopDirectoryOnly);
            if (exeFiles.Length > 0)
            {
                return extractDir;
            }

            // Check one level down (common zip structure)
            var subDirs = Directory.GetDirectories(extractDir);
            foreach (var subDir in subDirs)
            {
                exeFiles = Directory.GetFiles(subDir, "*.exe", SearchOption.TopDirectoryOnly);
                if (exeFiles.Length > 0)
                {
                    return subDir;
                }
            }

            // Default to extract directory
            return extractDir;
        }

        /// <summary>
        /// Creates a batch script that replaces all application files.
        /// Supports both single-file exe and multi-file publish folder structures.
        /// </summary>
        private string CreateUpdateScript(string sourceDir, string targetDir, string exeName, string tempDir, bool isSingleFile)
        {
            string targetPath = Path.Combine(targetDir, exeName);
            
            if (isSingleFile)
            {
                // Single-file exe: just copy the one file
                string sourcePath = Path.Combine(sourceDir, exeName);
                return $@"@echo off
title ArdysaModsTools Updater
echo Waiting for application to close...

:waitloop
tasklist /FI ""IMAGENAME eq {exeName}"" 2>NUL | find /I ""{exeName}"" >NUL
if %ERRORLEVEL%==0 (
    timeout /t 1 /nobreak >NUL
    goto waitloop
)

echo Updating...

REM Use PowerShell for reliable copy with spaces in paths
powershell -Command ""Start-Sleep -Seconds 1; Remove-Item -Path '{targetPath.Replace("'", "''")}' -Force -ErrorAction SilentlyContinue; Copy-Item -Path '{sourcePath.Replace("'", "''")}' -Destination '{targetPath.Replace("'", "''")}' -Force""

if %ERRORLEVEL%==0 (
    echo Update complete! Launching...
    powershell -Command ""Start-Process '{targetPath.Replace("'", "''")}' -WorkingDirectory '{targetDir.Replace("'", "''")}'""
) else (
    echo Update failed.
    pause
)

REM Cleanup
timeout /t 2 /nobreak >NUL
powershell -Command ""Remove-Item -Path '{tempDir.Replace("'", "''")}' -Recurse -Force -ErrorAction SilentlyContinue""

exit
";
            }
            else
            {
                // Multi-file folder: copy all files from source to target
                return $@"@echo off
title ArdysaModsTools Updater
echo Waiting for application to close...

:waitloop
tasklist /FI ""IMAGENAME eq {exeName}"" 2>NUL | find /I ""{exeName}"" >NUL
if %ERRORLEVEL%==0 (
    timeout /t 1 /nobreak >NUL
    goto waitloop
)

echo Updating application files...
timeout /t 1 /nobreak >NUL

REM Use PowerShell to copy all files from source to target (overwrite existing)
powershell -Command ""Get-ChildItem -Path '{sourceDir.Replace("'", "''")}' -Recurse | ForEach-Object {{ $targetFile = $_.FullName.Replace('{sourceDir.Replace("'", "''")}', '{targetDir.Replace("'", "''")}'); $targetFolder = Split-Path -Parent $targetFile; if (!(Test-Path $targetFolder)) {{ New-Item -ItemType Directory -Path $targetFolder -Force | Out-Null }}; if (!$_.PSIsContainer) {{ Copy-Item -Path $_.FullName -Destination $targetFile -Force }} }}""

if %ERRORLEVEL%==0 (
    echo Update complete! Launching...
    powershell -Command ""Start-Process '{targetPath.Replace("'", "''")}' -WorkingDirectory '{targetDir.Replace("'", "''")}'""
) else (
    echo Update failed. Error copying files.
    pause
    exit /b 1
)

REM Cleanup temp folder
timeout /t 2 /nobreak >NUL
powershell -Command ""Remove-Item -Path '{tempDir.Replace("'", "''")}' -Recurse -Force -ErrorAction SilentlyContinue""

exit
";
            }
        }
    }
}
