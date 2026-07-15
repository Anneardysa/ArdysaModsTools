/*
 * Copyright (C) 2026 Ardysa
 * Licensed under GPL v3
 */

using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace ArdysaModsTools.Installer.Services
{
    public static class PrerequisiteChecker
    {
        public static async Task EnsureWebView2Async(CancellationToken ct = default)
        {
            if (IsWebView2Installed())
                return;

            var tempBootstrapper = Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebview2Setup.exe");

            try
            {
                ExtractEmbeddedBootstrapper(tempBootstrapper);

                if (!File.Exists(tempBootstrapper))
                    return;

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

        public static bool IsWebView2Installed()
            => IsWebView2RuntimeAvailable() || IsWebView2InRegistry();

        private static bool IsWebView2RuntimeAvailable()
        {
            try
            {
                var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
                return !string.IsNullOrEmpty(version);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsWebView2InRegistry()
        {
            const string clientGuid = "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";

            if (HasValidPv(Registry.LocalMachine,
                    $@"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{clientGuid}"))
                return true;
            if (HasValidPv(Registry.LocalMachine,
                    $@"SOFTWARE\Microsoft\EdgeUpdate\Clients\{clientGuid}"))
                return true;

            return HasValidPv(Registry.CurrentUser,
                $@"SOFTWARE\Microsoft\EdgeUpdate\Clients\{clientGuid}");
        }

        private static bool HasValidPv(RegistryKey hive, string path)
        {
            try
            {
                using var key = hive.OpenSubKey(path);
                var version = key?.GetValue("pv") as string;
                return !string.IsNullOrEmpty(version) && version != "0.0.0.0";
            }
            catch
            {
                return false;
            }
        }


        private static void ExtractEmbeddedBootstrapper(string targetPath)
        {
            var assembly = Assembly.GetExecutingAssembly();

            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.Contains("MicrosoftEdgeWebview2Setup", StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
            {
                return;
            }

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return;

            using var fileStream = File.Create(targetPath);
            stream.CopyTo(fileStream);
        }
    }
}
