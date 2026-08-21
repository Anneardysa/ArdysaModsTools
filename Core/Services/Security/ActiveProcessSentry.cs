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
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Interfaces;

namespace ArdysaModsTools.Core.Services.Security;

public sealed class ActiveProcessSentry : IDisposable
{
    public static readonly HashSet<string> BlacklistedProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "vrf",
        "source2viewer",
        "gcfscape",
        "hlextract",
        "vpkedit",
        "vpkedit-cli",
        "vpk",
        "crowbar",
        "vtfedit",
        "quickbms",
        "offzip",
        "source2gen",
        "source2dumper",
        "steam_dumper",
        "valveresourceformat",

        "python",
        "pythonw",
        "python3",
        "python3.10",
        "python3.11",
        "python3.12",
        "python3.13",
        "py",
        "pypy",
        "pypy3",
        "ipython",

        "7zfm",
        "7zg",
        "winrar",
        "bandizip",
        "peazip",
        "winzip64",
        "winzip32",
        "HxD",
        "HxD64",
        "HxD32",
        "010editor",

        "assetstudio",
        "ninjaripper",
        "renderdoc",
        "processhacker",
        "procexp",
        "procexp64",
        "handle64",
        "blender",

        "cheatengine-x86_64",
        "cheatengine-i386",
        "x64dbg",
        "x32dbg",
        "ida64",
        "ida",
        "ghidra",
        "ghidraw",
        "dnspy",
        "ilspy",
        "fiddler",
        "wireshark"
    };

    public static readonly string[] SuspiciousWindowTitles = new[]
    {
        "Source 2 Viewer",
        "VRF -",
        "VPKEdit",
        "GCFScape",
        "HLExtract",
        "010 Editor",
        "Cheat Engine",
        "x64dbg",
        "x32dbg",
        "IDA Pro",
        "Ghidra",
        "dnSpy"
    };

    private readonly IAppLogger? _logger;
    private readonly Func<string, bool>? _threatCallback;
    private CancellationTokenSource? _cts;
    private Task? _watchTask;
    private bool _disposed;

    public ActiveProcessSentry(Func<string, bool>? threatCallback = null, IAppLogger? logger = null)
    {
        _threatCallback = threatCallback;
        _logger = logger;
    }

    public static string? DetectRunningThreat(IEnumerable<string>? customBlacklist = null)
    {
        var blacklist = customBlacklist != null
            ? new HashSet<string>(customBlacklist, StringComparer.OrdinalIgnoreCase)
            : BlacklistedProcesses;

        try
        {
            var processes = Process.GetProcesses();
            foreach (var proc in processes)
            {
                try
                {
                    string name = proc.ProcessName;
                    if (blacklist.Contains(name))
                    {
                        return name;
                    }

                    string title = proc.MainWindowTitle;
                    if (!string.IsNullOrEmpty(title))
                    {
                        foreach (var keyword in SuspiciousWindowTitles)
                        {
                            if (title.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                            {
                                return $"{name} (Title: {keyword})";
                            }
                        }
                    }
                }
                catch
                {
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            FallbackLogger.LogFileOnly($"ActiveProcessSentry.DetectRunningThreat error: {ex.Message}");
        }

        return null;
    }

    public static int KillRunningThreats(IAppLogger? logger = null, IEnumerable<string>? customBlacklist = null)
    {
        var blacklist = customBlacklist != null
            ? new HashSet<string>(customBlacklist, StringComparer.OrdinalIgnoreCase)
            : BlacklistedProcesses;

        int killed = 0;
        try
        {
            var processes = Process.GetProcesses();
            foreach (var proc in processes)
            {
                try
                {
                    string name = proc.ProcessName;
                    bool isThreat = blacklist.Contains(name);

                    if (!isThreat)
                    {
                        string title = proc.MainWindowTitle;
                        if (!string.IsNullOrEmpty(title))
                        {
                            foreach (var keyword in SuspiciousWindowTitles)
                            {
                                if (title.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                                {
                                    isThreat = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (isThreat)
                    {
                        proc.Kill(entireProcessTree: true);
                        killed++;
                        logger?.Log($"ActiveProcessSentry: Terminated unauthorized process '{name}' (PID: {proc.Id}).");
                        FallbackLogger.Log($"[ActiveProcessSentry] Terminated threat process: '{name}'");
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogDebug($"ActiveProcessSentry: Could not kill process '{proc.ProcessName}': {ex.Message}");
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            FallbackLogger.LogFileOnly($"ActiveProcessSentry.KillRunningThreats error: {ex.Message}");
        }

        return killed;
    }

    public void Start(int pollIntervalMs = 800)
    {
        if (_watchTask != null || _disposed)
            return;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _watchTask = Task.Run(async () =>
        {
            _logger?.LogDebug("ActiveProcessSentry started watching for unauthorized ripper processes.");
            while (!token.IsCancellationRequested)
            {
                try
                {
                    string? detected = DetectRunningThreat();
                    if (!string.IsNullOrEmpty(detected))
                    {
                        _logger?.Log($"ActiveProcessSentry: detected unauthorized process '{detected}' during active session!");
                        FallbackLogger.Log($"[ActiveProcessSentry] Threat detected: '{detected}'");

                        bool handled = _threatCallback?.Invoke(detected) ?? false;
                        if (handled)
                        {
                            _logger?.LogDebug($"ActiveProcessSentry: threat '{detected}' handled by callback.");
                        }

                        await Task.Delay(2000, token).ConfigureAwait(false);
                    }
                    else
                    {
                        await Task.Delay(pollIntervalMs, token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    FallbackLogger.LogFileOnly($"ActiveProcessSentry watch loop error: {ex.Message}");
                    await Task.Delay(pollIntervalMs * 2, token).ConfigureAwait(false);
                }
            }
            _logger?.LogDebug("ActiveProcessSentry stopped.");
        }, token);
    }

    public void Stop()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
            try { _watchTask?.Wait(1000); } catch { }
            _cts.Dispose();
            _cts = null;
            _watchTask = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
