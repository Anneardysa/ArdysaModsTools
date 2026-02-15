/*
 * Copyright (C) 2026 Ardysa
 * Licensed under GPL v3
 */

using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace ArdysaModsTools.Installer.Services
{
    /// <summary>
    /// Checks for and installs prerequisite runtimes.
    /// Currently handles: WebView2 Runtime.
    /// 
    /// The .NET 8 runtime is NOT needed — the app is self-contained.
    /// </summary>
    public static class PrerequisiteChecker
    {
        /// <summary>
        /// Ensures WebView2 Runtime is installed. If not, extracts the embedded
        /// bootstrapper and runs it silently.
        /// </summary>
        public static async Task EnsureWebView2Async(CancellationToken ct = default)
        {
            if (IsWebView2Installed())
                return;

            // Extract the embedded WebView2 bootstrapper
            var tempBootstrapper = Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebview2Setup.exe");

            try
            {
                ExtractEmbeddedBootstrapper(tempBootstrapper);

                // Run silent install
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = tempBootstrapper,
                    Arguments = "/silent /install",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });

                if (process != null)
                {
                    await process.WaitForExitAsync(ct);
                }
            }
            finally
            {
                try { File.Delete(tempBootstrapper); } catch { }
            }
        }

        /// <summary>
        /// Checks the registry to determine if WebView2 Runtime is installed.
        /// Mirrors the check from the original ArdysaModsTools.iss.
        /// </summary>
        public static bool IsWebView2Installed()
        {
            // WebView2 registry client GUID
            const string clientGuid = "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";

            string[] registryPaths =
            [
                $@"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{clientGuid}",
                $@"SOFTWARE\Microsoft\EdgeUpdate\Clients\{clientGuid}",
            ];

            // Check HKLM paths
            foreach (var path in registryPaths)
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(path);
                    var version = key?.GetValue("pv") as string;
                    if (!string.IsNullOrEmpty(version))
                        return true;
                }
                catch { }
            }

            // Check HKCU
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    $@"SOFTWARE\Microsoft\EdgeUpdate\Clients\{clientGuid}");
                var version = key?.GetValue("pv") as string;
                if (!string.IsNullOrEmpty(version))
                    return true;
            }
            catch { }

            return false;
        }

        // ════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ════════════════════════════════════════════════════

        private static void ExtractEmbeddedBootstrapper(string targetPath)
        {
            var assembly = Assembly.GetExecutingAssembly();

            // Find the embedded WebView2 bootstrapper
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.Contains("MicrosoftEdgeWebview2Setup", StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
            {
                // No embedded bootstrapper — not a critical failure
                return;
            }

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return;

            using var fileStream = File.Create(targetPath);
            stream.CopyTo(fileStream);
        }
    }
}
