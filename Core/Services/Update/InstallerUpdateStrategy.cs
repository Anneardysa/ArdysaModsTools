using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Services.Update.Models;

namespace ArdysaModsTools.Core.Services.Update
{
    /// <summary>
    /// Update strategy for installer-based installations.
    /// </summary>
    public class InstallerUpdateStrategy : IUpdateStrategy
    {
        public InstallationType InstallationType => InstallationType.Installer;

        public string AssetPattern => "_Setup_";

        public bool RequiresAdminRights => true;

        public string? ValidateCanUpdate() => null;

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

                // Get current exe info for launching after install
                string currentExe = Process.GetCurrentProcess().MainModule!.FileName;
                string exeName = Path.GetFileName(currentExe);
                
                onStatusChanged?.Invoke("Preparing installer...");
                onProgress?.Invoke(50);

                // Create simple batch script
                // NO /DIR flag - let Inno Setup detect existing install location automatically
                string scriptPath = Path.Combine(Path.GetTempPath(), $"amt_installer_{Guid.NewGuid()}.bat");
                
                // Default install path for Inno Setup
                string defaultInstallPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "ArdysaModsTools",
                    "ArdysaModsTools.exe"
                );
                
                string batchScript = $@"@echo off
title ArdysaModsTools Installer
echo.
echo ============================================
echo   Installing ArdysaModsTools Update...
echo ============================================
echo.
echo Please wait, running installer...
echo.

""{downloadedFilePath}"" /VERYSILENT /SUPPRESSMSGBOXES

echo.
echo Installation complete!
echo Launching application...
echo.

start """" ""{defaultInstallPath}""

del ""%~f0""
";

                await File.WriteAllTextAsync(scriptPath, batchScript);

                onStatusChanged?.Invoke("Starting installer...");
                onProgress?.Invoke(100);

                // Run batch script with visible window
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{scriptPath}\"",
                    UseShellExecute = true,
                    Verb = "runas"
                });

                await Task.Delay(500);

                return UpdateResult.Succeeded(requiresRestart: true);
            }
            catch (Exception ex)
            {
                return UpdateResult.Failed($"Failed: {ex.Message}");
            }
        }
    }
}
