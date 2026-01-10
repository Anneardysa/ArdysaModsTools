using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArdysaModsTools.Core.Services
{
    /// <summary>
    /// Interface for VPK recompilation operations.
    /// </summary>
    public interface IVpkRecompiler
    {
        /// <summary>
        /// Recompiles a folder into VPK using vpk.exe.
        /// </summary>
        Task<string?> RecompileAsync(string vpkToolPath, string extractDir, string buildDir,
            string tempRoot, Action<string> log, CancellationToken ct = default,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null);
    }

    /// <summary>
    /// Focused service for VPK recompilation using vpk.exe.
    /// Single responsibility: Recompile folders into VPK archives.
    /// </summary>
    public sealed class VpkRecompilerService : IVpkRecompiler
    {
        private readonly ILogger? _logger;

        // Optimized timing constants
        private const int PostProcessDelayMs = 500;      // Brief delay after vpk.exe completes
        private const int VpkSearchIntervalMs = 300;     // Interval between VPK file searches
        private const int VpkSearchMaxRetries = 15;      // Max retries for VPK file search
        private const int FileReadyCheckMs = 200;        // Interval for file lock checks
        private const int FileReadyMaxAttempts = 20;     // Max attempts for file ready check
        private const int VpkProcessTimeoutMinutes = 5;  // Timeout for vpk.exe process

        public VpkRecompilerService(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<string?> RecompileAsync(string vpkToolPath, string extractDir, string buildDir,
            string tempRoot, Action<string> log, CancellationToken ct = default,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null)
        {
            // === DETAILED DIAGNOSTIC LOGGING ===
            _logger?.Log("=== VPK RECOMPILATION DIAGNOSTICS ===");
            _logger?.Log($"  vpkToolPath: {vpkToolPath}");
            _logger?.Log($"  extractDir: {extractDir}");
            _logger?.Log($"  buildDir: {buildDir}");
            _logger?.Log($"  tempRoot: {tempRoot}");
            
            // Check vpk.exe
            if (string.IsNullOrWhiteSpace(vpkToolPath))
            {
                log("[Recompile] vpk.exe path is empty or null.");
                _logger?.Log("ERROR: vpkToolPath is null or empty");
                return null;
            }
            
            if (!File.Exists(vpkToolPath))
            {
                log("[Recompile] vpk.exe not found.");
                _logger?.Log($"ERROR: vpk.exe not found at: {vpkToolPath}");
                
                // Check what DOES exist in the directory
                string vpkDir = Path.GetDirectoryName(vpkToolPath) ?? "";
                if (Directory.Exists(vpkDir))
                {
                    var filesInDir = Directory.GetFiles(vpkDir, "*.exe");
                    _logger?.Log($"  Files in vpk directory ({vpkDir}):");
                    foreach (var f in filesInDir.Take(10))
                    {
                        _logger?.Log($"    - {Path.GetFileName(f)}");
                    }
                }
                else
                {
                    _logger?.Log($"  Directory does not exist: {vpkDir}");
                }
                return null;
            }
            
            _logger?.Log($"  vpk.exe EXISTS: {new FileInfo(vpkToolPath).Length} bytes");
            
            // Check required DLLs
            string vpkDirectory = Path.GetDirectoryName(vpkToolPath) ?? "";
            string[] requiredDlls = { "filesystem_stdio.dll", "tier0.dll", "tier0_s.dll", "vstdlib.dll", "vstdlib_s.dll" };
            _logger?.Log("  Checking required DLLs:");
            foreach (var dll in requiredDlls)
            {
                string dllPath = Path.Combine(vpkDirectory, dll);
                bool exists = File.Exists(dllPath);
                _logger?.Log($"    {dll}: {(exists ? "OK" : "MISSING")}");
                if (!exists)
                {
                    log($"[Recompile] Missing required DLL: {dll}");
                }
            }

            // Validate extractDir exists and has content
            if (!Directory.Exists(extractDir))
            {
                log("[Recompile] Source folder not found.");
                _logger?.Log($"ERROR: extractDir does not exist: {extractDir}");
                return null;
            }

            var fileCount = Directory.GetFiles(extractDir, "*", SearchOption.AllDirectories).Length;
            var dirCount = Directory.GetDirectories(extractDir, "*", SearchOption.AllDirectories).Length;
            
            if (fileCount == 0)
            {
                log("[Recompile] Source folder is empty.");
                _logger?.Log($"ERROR: extractDir is empty: {extractDir}");
                return null;
            }

            _logger?.Log($"  Source folder: {fileCount} files, {dirCount} subdirectories");

            // Check buildDir
            if (!Directory.Exists(buildDir))
            {
                _logger?.Log($"  Creating buildDir: {buildDir}");
                try
                {
                    Directory.CreateDirectory(buildDir);
                }
                catch (Exception ex)
                {
                    log($"[Recompile] Cannot create build directory: {ex.Message}");
                    _logger?.Log($"ERROR: Failed to create buildDir: {ex.Message}");
                    return null;
                }
            }
            _logger?.Log($"  buildDir exists: {Directory.Exists(buildDir)}");

            var psi = new ProcessStartInfo
            {
                FileName = vpkToolPath,
                Arguments = $"\"{extractDir}\"",
                WorkingDirectory = buildDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            _logger?.Log($"  Process command: \"{vpkToolPath}\" \"{extractDir}\"");
            _logger?.Log($"  Working directory: {buildDir}");

            var startTime = DateTime.UtcNow;
            var errorOutput = new List<string>();
            var standardOutput = new List<string>();

            try
            {
                using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
                
                proc.OutputDataReceived += (s, e) => 
                { 
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        standardOutput.Add(e.Data);
                        _logger?.Log($"[vpk.exe stdout] {e.Data}");
                    }
                };
                proc.ErrorDataReceived += (s, e) => 
                { 
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        errorOutput.Add(e.Data);
                        _logger?.Log($"[vpk.exe stderr] {e.Data}"); 
                    }
                };

                _logger?.Log("  Starting vpk.exe process...");
                
                if (!proc.Start())
                {
                    log("[Recompile] Failed to start vpk.exe process.");
                    _logger?.Log("ERROR: Process.Start() returned false");
                    return null;
                }

                _logger?.Log($"  Process started with PID: {proc.Id}");

                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                // Wait for process with cancellation and timeout support
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromMinutes(VpkProcessTimeoutMinutes));
                
                using (ct.Register(() =>
                {
                    try { if (!proc.HasExited) proc.Kill(); } catch { }
                }))
                {
                    try
                    {
                        await proc.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        // Timeout occurred (not user cancellation)
                        try { if (!proc.HasExited) proc.Kill(); } catch { }
                        log($"[Recompile] vpk.exe timed out after {VpkProcessTimeoutMinutes} minutes");
                        _logger?.Log($"ERROR: vpk.exe timed out after {VpkProcessTimeoutMinutes} minutes");
                        return null;
                    }
                }

                _logger?.Log($"  Process exited with code: {proc.ExitCode}");
                _logger?.Log($"  Stdout lines: {standardOutput.Count}");
                _logger?.Log($"  Stderr lines: {errorOutput.Count}");

                // Check exit code
                if (proc.ExitCode != 0)
                {
                    var errorMsg = errorOutput.Count > 0 ? string.Join("; ", errorOutput) : "No error output captured";
                    log($"[Recompile] vpk.exe failed (code {proc.ExitCode})");
                    _logger?.Log($"ERROR: vpk.exe exited with code {proc.ExitCode}");
                    _logger?.Log($"  Error output: {errorMsg}");
                    if (standardOutput.Count > 0)
                    {
                        _logger?.Log($"  Standard output: {string.Join("; ", standardOutput)}");
                    }
                    return null;
                }
            }
            catch (System.ComponentModel.Win32Exception win32Ex)
            {
                log($"[Recompile] Windows error: {win32Ex.Message}");
                _logger?.Log($"ERROR: Win32Exception - {win32Ex.Message}");
                _logger?.Log($"  NativeErrorCode: {win32Ex.NativeErrorCode}");
                _logger?.Log($"  This may indicate vpk.exe is blocked by antivirus or missing dependencies");
                return null;
            }
            catch (OperationCanceledException)
            {
                _logger?.Log("VPK recompilation cancelled by user");
                return null;
            }
            catch (Exception ex)
            {
                log($"[Recompile] Error: {ex.Message}");
                _logger?.Log($"ERROR: Exception during recompilation: {ex.GetType().Name}");
                _logger?.Log($"  Message: {ex.Message}");
                _logger?.Log($"  StackTrace: {ex.StackTrace}");
                return null;
            }

            // Brief delay for filesystem to flush
            await Task.Delay(PostProcessDelayMs, ct).ConfigureAwait(false);

            // Search for output VPK file
            string? newVpk = await FindOutputVpkAsync(extractDir, buildDir, tempRoot, startTime, ct).ConfigureAwait(false);

            if (newVpk == null)
            {
                log("[Recompile] Failed — no output VPK found.");
                _logger?.Log($"No VPK output found. Searched: {buildDir}, {extractDir}, {tempRoot}");
                return null;
            }

            _logger?.Log($"VPK created successfully: {newVpk}");

            // Wait for file to be fully written and unlocked
            await WaitForFileReadyAsync(newVpk, ct).ConfigureAwait(false);
            
            // Calculate and report average write speed
            var totalTime = (DateTime.UtcNow - startTime).TotalSeconds;
            if (totalTime > 0)
            {
                var finalSize = new FileInfo(newVpk).Length;
                var avgWriteSpeed = finalSize / 1024.0 / 1024.0 / totalTime;
                speedProgress?.Report(new ArdysaModsTools.Core.Models.SpeedMetrics { WriteSpeed = $"{avgWriteSpeed:F1} MB/S" });
            }
            
            return newVpk;
        }

        /// <summary>
        /// Searches for the newly created VPK file in expected directories.
        /// </summary>
        private async Task<string?> FindOutputVpkAsync(string extractDir, string buildDir, 
            string tempRoot, DateTime startTime, CancellationToken ct)
        {
            string parentDir = Path.GetDirectoryName(extractDir) ?? tempRoot;
            string[] searchDirs = { buildDir, extractDir, parentDir, tempRoot };

            for (int i = 0; i < VpkSearchMaxRetries; i++)
            {
                ct.ThrowIfCancellationRequested();
                
                foreach (var dir in searchDirs.Where(Directory.Exists))
                {
                    try
                    {
                        var found = Directory.GetFiles(dir, "*.vpk", SearchOption.TopDirectoryOnly)
                            .Where(f => File.GetCreationTimeUtc(f) >= startTime.AddSeconds(-2))
                            .OrderByDescending(File.GetCreationTimeUtc)
                            .FirstOrDefault();
                        
                        if (found != null)
                        {
                            return found;
                        }
                    }
                    catch { /* Directory access error - continue */ }
                }

                await Task.Delay(VpkSearchIntervalMs, ct).ConfigureAwait(false);
            }

            return null;
        }

        /// <summary>
        /// Waits for the VPK file to be fully written and unlocked.
        /// </summary>
        private async Task WaitForFileReadyAsync(string filePath, CancellationToken ct)
        {
            for (int i = 0; i < FileReadyMaxAttempts; i++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                    return; // File is ready
                }
                catch (IOException)
                {
                    await Task.Delay(FileReadyCheckMs, ct).ConfigureAwait(false);
                }
            }
            // Continue anyway - file might be usable even if locked
        }
    }
}
