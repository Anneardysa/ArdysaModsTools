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
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace ArdysaModsTools.Core.Helpers
{
    public enum WebView2Source
    {
        Loader,
        Registry,
        None,
    }

    public readonly record struct WebView2Detection(WebView2Source Source, string? Version, string? Diagnostic)
    {
        public bool IsInstalled => Source != WebView2Source.None;
    }

    public static class WebView2Runtime
    {
        private const string ClientGuid = "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";

        public static WebView2Detection Detect()
        {
            string? probeDiagnostic = null;

            try
            {
                var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
                if (!string.IsNullOrEmpty(version))
                    return new WebView2Detection(WebView2Source.Loader, version, null);
            }
            catch (Exception ex)
            {
                probeDiagnostic = $"{ex.GetType().Name}: {ex.Message}";
            }

            if (TryGetRegistryVersion(out var pv))
                return new WebView2Detection(WebView2Source.Registry, pv, probeDiagnostic);

            return new WebView2Detection(WebView2Source.None, null, probeDiagnostic);
        }

        private static bool TryGetRegistryVersion(out string? version)
        {
            if (TryReadPv(Registry.LocalMachine, $@"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{ClientGuid}", out version))
                return true;
            if (TryReadPv(Registry.LocalMachine, $@"SOFTWARE\Microsoft\EdgeUpdate\Clients\{ClientGuid}", out version))
                return true;
            return TryReadPv(Registry.CurrentUser, $@"SOFTWARE\Microsoft\EdgeUpdate\Clients\{ClientGuid}", out version);
        }

        private static bool TryReadPv(RegistryKey hive, string path, out string? version)
        {
            version = null;
            try
            {
                using var key = hive.OpenSubKey(path);
                var pv = key?.GetValue("pv") as string;
                if (!string.IsNullOrEmpty(pv) && pv != "0.0.0.0")
                {
                    version = pv;
                    return true;
                }
            }
            catch
            {
            }
            return false;
        }
    }
}
