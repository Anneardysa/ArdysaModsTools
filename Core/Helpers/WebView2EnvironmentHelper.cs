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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace ArdysaModsTools.Core.Helpers
{
    public static class WebView2EnvironmentHelper
    {
        private static readonly string LegacyUserDataFolder =
            Path.Combine(Path.GetTempPath(), "ArdysaModsTools.WebView2");

        private static int _legacyCleanupDone;

        private static readonly object _envGate = new();
        private static Task<CoreWebView2Environment>? _environmentTask;

        public static string UserDataFolder { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ArdysaModsTools",
            "WebView2");

        public static Task<CoreWebView2Environment> CreateEnvironmentAsync()
        {
            lock (_envGate)
            {
                if (_environmentTask is null || _environmentTask.IsFaulted || _environmentTask.IsCanceled)
                    _environmentTask = CreateEnvironmentInternalAsync();
                return _environmentTask;
            }
        }

        private static async Task<CoreWebView2Environment> CreateEnvironmentInternalAsync()
        {
            EnsureUserDataFolderExists();
            TryCleanupLegacyFolderOnce();

            try
            {
                return await CoreWebView2Environment.CreateAsync(null, UserDataFolder).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[WebView2EnvironmentHelper] CreateAsync failed, resetting profile: {ex.Message}");
                if (!TryResetUserDataFolder())
                    throw;

                EnsureUserDataFolderExists();
                return await CoreWebView2Environment.CreateAsync(null, UserDataFolder).ConfigureAwait(false);
            }
        }

        private static bool TryResetUserDataFolder()
        {
            try
            {
                if (!Directory.Exists(UserDataFolder))
                    return true;

                var retired = $"{UserDataFolder}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
                Directory.Move(UserDataFolder, retired);

                try { Directory.Delete(retired, recursive: true); }
                catch {  }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[WebView2EnvironmentHelper] Profile reset failed: {ex.Message}");
                return false;
            }
        }

        private static void EnsureUserDataFolderExists()
        {
            try
            {
                if (!Directory.Exists(UserDataFolder))
                    Directory.CreateDirectory(UserDataFolder);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[WebView2EnvironmentHelper] Failed to create user-data folder: {ex.Message}");
            }
        }

        private static void TryCleanupLegacyFolderOnce()
        {
            if (Interlocked.Exchange(ref _legacyCleanupDone, 1) != 0)
                return;

            try
            {
                if (Directory.Exists(LegacyUserDataFolder))
                    Directory.Delete(LegacyUserDataFolder, recursive: true);
            }
            catch
            {
            }
        }
    }
}
