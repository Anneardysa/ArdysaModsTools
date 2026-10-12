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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Interfaces;

namespace ArdysaModsTools.Core.Services
{
    public interface IVpkRecompiler
    {
        Task<string?> RecompileAsync(string vpkToolPath, string extractDir, string buildDir,
            string tempRoot, Action<string> log, CancellationToken ct = default,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null);
    }

    public sealed class VpkRecompilerService : IVpkRecompiler
    {
        private readonly IAppLogger? _logger;

        private const int PostProcessDelayMs = 500;
        private const int VpkSearchIntervalMs = 300;
        private const int VpkSearchMaxRetries = 15;
        private const int FileReadyCheckMs = 200;
        private const int FileReadyMaxAttempts = 20;
        private const int VpkProcessTimeoutMinutes = 5;

        public VpkRecompilerService(IAppLogger? logger = null)
        {
            _logger = logger;
        }

        public async Task<string?> RecompileAsync(string vpkToolPath, string extractDir, string buildDir,
            string tempRoot, Action<string> log, CancellationToken ct = default,
            IProgress<ArdysaModsTools.Core.Models.SpeedMetrics>? speedProgress = null)
        {
            _logger?.LogDebug("=== VPK RECOMPILATION DIAGNOSTICS ===");
            _logger?.LogDebug($"  vpkToolPath: {vpkToolPath}");
            _logger?.LogDebug($"  extractDir: {extractDir}");
            _logger?.LogDebug($"  buildDir: {buildDir}");
            _logger?.LogDebug($"  tempRoot: {tempRoot}");
            
            if (string.IsNullOrWhiteSpace(vpkToolPath))
            {
                log("[Recompile] vpk.exe path is empty or null.");
                _logger?.LogDebug("ERROR: vpkToolPath is null or empty");
                return null;
            }
            
            if (!File.Exists(vpkToolPath))
            {
                log("[Recompile] vpk.exe not found.");
                _logger?.LogDebug($"ERROR: vpk.exe not found at: {vpkToolPath}");
                
                string vpkDir = Path.GetDirectoryName(vpkToolPath) ?? "";
                if (Directory.Exists(vpkDir))
                {
                    var filesInDir = Directory.GetFiles(vpkDir, "*.exe");
                    _logger?.LogDebug($"  Files in vpk directory ({vpkDir}):");
                    foreach (var f in filesInDir.Take(10))
                    {
                        _logger?.LogDebug($"    - {Path.GetFileName(f)}");
                    }
                }
                else
                {
                    _logger?.LogDebug($"  Directory does not exist: {vpkDir}");
                }
                return null;
            }
            
            _logger?.LogDebug($"  vpk.exe EXISTS: {new FileInfo(vpkToolPath).Length} bytes");
            
            string vpkDirectory = Path.GetDirectoryName(vpkToolPath) ?? "";
            string[] requiredDlls = { "filesystem_stdio.dll", "tier0.dll", "tier0_s.dll", "vstdlib.dll", "vstdlib_s.dll" };
            _logger?.LogDebug("  Checking required DLLs:");
            foreach (var dll in requiredDlls)
            {
                string dllPath = Path.Combine(vpkDirectory, dll);
                bool exists = File.Exists(dllPath);
                _logger?.LogDebug($"    {dll}: {(exists ? "OK" : "MISSING")}");
                if (!exists)
                {
                    log($"[Recompile] Missing required DLL: {dll}");
                }
            }

            if (!Directory.Exists(extractDir))
            {
                log("[Recompile] Source folder not found.");
                _logger?.LogDebug($"ERROR: extractDir does not exist: {extractDir}");
                return null;
            }

            var fileCount = Directory.GetFiles(extractDir, "*", SearchOption.AllDirectories).Length;
            var dirCount = Directory.GetDirectories(extractDir, "*", SearchOption.AllDirectories).Length;
            
            if (fileCount == 0)
            {
                log("[Recompile] Source folder is empty.");
                _logger?.LogDebug($"ERROR: extractDir is empty: {extractDir}");
                return null;
            }

            _logger?.LogDebug($"  Source folder: {fileCount} files, {dirCount} subdirectories");

            if (!Directory.Exists(buildDir))
            {
                _logger?.LogDebug($"  Creating buildDir: {buildDir}");
                try
                {
                    Directory.CreateDirectory(buildDir);
                }
                catch (Exception ex)
                {
                    log($"[Recompile] Cannot create build directory: {ex.Message}");
                    _logger?.LogDebug($"ERROR: Failed to create buildDir: {ex.Message}");
                    return null;
                }
            }
            _logger?.LogDebug($"  buildDir exists: {Directory.Exists(buildDir)}");

            string parentDir = Path.GetDirectoryName(extractDir) ?? "";
            string expectedVpkName = Path.GetFileName(extractDir) + ".vpk";
            string expectedVpkPath = Path.Combine(parentDir, expectedVpkName);
            
            if (File.Exists(expectedVpkPath))
            {
                _logger?.LogDebug($"  Deleting existing VPK: {expectedVpkPath}");
                try
                {
                    File.Delete(expectedVpkPath);
                }
                catch (Exception ex)
                {
                    log($"[Recompile] Warning: Could not delete existing VPK: {ex.Message}");
                    _logger?.LogDebug($"  Warning: Could not delete existing VPK: {ex.Message}");
                }
            }

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

            _logger?.LogDebug($"  Process command: \"{vpkToolPath}\" \"{extractDir}\"");
            _logger?.LogDebug($"  Working directory: {buildDir}");

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
                        _logger?.LogDebug($"[vpk.exe stdout] {e.Data}");
                    }
                };
                proc.ErrorDataReceived += (s, e) => 
                { 
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        errorOutput.Add(e.Data);
                        _logger?.LogDebug($"[vpk.exe stderr] {e.Data}"); 
                    }
                };

                _logger?.LogDebug("  Starting vpk.exe process...");
                
                if (!proc.Start())
                {
                    log("[Recompile] Failed to start vpk.exe process.");
                    _logger?.LogDebug("ERROR: Process.Start() returned false");
                    return null;
                }

                _logger?.LogDebug($"  Process started with PID: {proc.Id}");

                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

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
                        try { if (!proc.HasExited) proc.Kill(); } catch { }
                        log($"[Recompile] vpk.exe timed out after {VpkProcessTimeoutMinutes} minutes");
                        _logger?.LogDebug($"ERROR: vpk.exe timed out after {VpkProcessTimeoutMinutes} minutes");
                        return null;
                    }
                }

                _logger?.LogDebug($"  Process exited with code: {proc.ExitCode}");
                _logger?.LogDebug($"  Stdout lines: {standardOutput.Count}");
                _logger?.LogDebug($"  Stderr lines: {errorOutput.Count}");

                if (proc.ExitCode != 0)
                {
                    var errorMsg = errorOutput.Count > 0 ? string.Join("; ", errorOutput) : "No error output captured";
                    log($"[Recompile] vpk.exe failed (code {proc.ExitCode})");
                    _logger?.LogDebug($"ERROR: vpk.exe exited with code {proc.ExitCode}");
                    _logger?.LogDebug($"  Error output: {errorMsg}");
                    if (standardOutput.Count > 0)
                    {
                        _logger?.LogDebug($"  Standard output: {string.Join("; ", standardOutput)}");
                    }
                    return null;
                }
            }
            catch (System.ComponentModel.Win32Exception win32Ex)
            {
                log($"[Recompile] Windows error: {win32Ex.Message}");
                _logger?.LogDebug($"ERROR: Win32Exception - {win32Ex.Message}");
                _logger?.LogDebug($"  NativeErrorCode: {win32Ex.NativeErrorCode}");
                _logger?.LogDebug($"  This may indicate vpk.exe is blocked by antivirus or missing dependencies");
                return null;
            }
            catch (OperationCanceledException)
            {
                _logger?.LogDebug("VPK recompilation cancelled by user");
                return null;
            }
            catch (Exception ex)
            {
                log($"[Recompile] Error: {ex.Message}");
                _logger?.LogDebug($"ERROR: Exception during recompilation: {ex.GetType().Name}");
                _logger?.LogDebug($"  Message: {ex.Message}");
                _logger?.LogDebug($"  StackTrace: {ex.StackTrace}");
                return null;
            }

            await Task.Delay(PostProcessDelayMs, ct).ConfigureAwait(false);

            string? newVpk = await FindOutputVpkAsync(extractDir, buildDir, tempRoot, startTime, log, ct).ConfigureAwait(false);

            if (newVpk == null)
            {
                log("[Recompile] Failed — no output VPK found.");
                return null;
            }

            _logger?.LogDebug($"VPK created successfully: {newVpk}");

            await WaitForFileReadyAsync(newVpk, ct).ConfigureAwait(false);
            
            var totalTime = (DateTime.UtcNow - startTime).TotalSeconds;
            if (totalTime > 0)
            {
                var finalSize = new FileInfo(newVpk).Length;
                var avgWriteSpeed = finalSize / 1024.0 / 1024.0 / totalTime;
                speedProgress?.Report(new ArdysaModsTools.Core.Models.SpeedMetrics { WriteSpeed = $"{avgWriteSpeed:F1} MB/S" });
            }
            
            return newVpk;
        }

        private async Task<string?> FindOutputVpkAsync(string extractDir, string buildDir, 
            string tempRoot, DateTime startTime, Action<string> log, CancellationToken ct)
        {
            string parentDir = Path.GetDirectoryName(extractDir) ?? tempRoot;
            
            string expectedVpkName = Path.GetFileName(extractDir) + ".vpk";
            string expectedVpkPath = Path.Combine(parentDir, expectedVpkName);
            
            _logger?.LogDebug($"[VPK-Search] Expected: {expectedVpkPath}");
            _logger?.LogDebug($"  Start time: {startTime:O}");

            if (File.Exists(expectedVpkPath))
            {
                var vpkWriteTime = File.GetLastWriteTimeUtc(expectedVpkPath);
                var timeDiff = (DateTime.UtcNow - vpkWriteTime).TotalSeconds;
                _logger?.LogDebug($"[VPK-Search] Found VPK, age: {timeDiff:F1}s");
                
                if (vpkWriteTime >= startTime.AddSeconds(-5))
                {
                    _logger?.LogDebug($"[VPK-Search] VPK valid, using: {Path.GetFileName(expectedVpkPath)}");
                    return expectedVpkPath;
                }
                else
                {
                    _logger?.LogDebug($"[VPK-Search] VPK too old (created {timeDiff:F0}s ago)");
                }
            }
            else
            {
                _logger?.LogDebug($"[VPK-Search] Expected VPK not found at: {expectedVpkPath}");
            }

            string[] searchDirs = { buildDir, parentDir, extractDir, tempRoot };

            for (int i = 0; i < VpkSearchMaxRetries; i++)
            {
                ct.ThrowIfCancellationRequested();
                
                foreach (var dir in searchDirs.Where(Directory.Exists))
                {
                    try
                    {
                        var found = Directory.GetFiles(dir, "*.vpk", SearchOption.TopDirectoryOnly)
                            .Where(f => File.GetLastWriteTimeUtc(f) >= startTime.AddSeconds(-5))
                            .OrderByDescending(File.GetLastWriteTimeUtc)
                            .FirstOrDefault();
                        
                        if (found != null)
                        {
                            _logger?.LogDebug($"[VPK-Search] Found: {found}");
                            return found;
                        }
                    }
                    catch {  }
                }

                await Task.Delay(VpkSearchIntervalMs, ct).ConfigureAwait(false);
            }

            _logger?.LogDebug($"[VPK-Search] FAILED after {VpkSearchMaxRetries} retries");
            foreach (var dir in searchDirs)
            {
                if (Directory.Exists(dir))
                {
                    try
                    {
                        var vpkFiles = Directory.GetFiles(dir, "*.vpk", SearchOption.TopDirectoryOnly);
                        if (vpkFiles.Length > 0)
                        {
                            _logger?.LogDebug($"[VPK-Search] {Path.GetFileName(dir)}/: {vpkFiles.Length} VPK(s)");
                            foreach (var f in vpkFiles.Take(3))
                            {
                                var writeTime = File.GetLastWriteTimeUtc(f);
                                var age = (DateTime.UtcNow - writeTime).TotalSeconds;
                                _logger?.LogDebug($"[VPK-Search]   {Path.GetFileName(f)} ({age:F0}s old)");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug($"[VPK-Search] Error reading {dir}: {ex.Message}");
                    }
                }
            }

            return null;
        }

        private async Task WaitForFileReadyAsync(string filePath, CancellationToken ct)
        {
            for (int i = 0; i < FileReadyMaxAttempts; i++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                    return;
                }
                catch (IOException)
                {
                    await Task.Delay(FileReadyCheckMs, ct).ConfigureAwait(false);
                }
            }
        }
    }
}

