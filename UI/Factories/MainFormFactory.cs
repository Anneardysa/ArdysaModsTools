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
using System.Windows.Forms;
using ArdysaModsTools.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;

namespace ArdysaModsTools.UI.Factories
{
    /// <summary>
    /// Factory for creating MainForm instances with full dependency injection.
    /// Resolves all required services from the DI container and passes them to MainForm.
    /// </summary>
    public class MainFormFactory : IMainFormFactory
    {
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Creates a new MainFormFactory with access to the DI container.
        /// </summary>
        /// <param name="serviceProvider">The DI service provider.</param>
        public MainFormFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <inheritdoc />
        public Form Create(bool startMinimized = false)
        {
            // Resolve all dependencies from DI container
            var configService = _serviceProvider.GetRequiredService<IConfigService>();
            var detectionService = _serviceProvider.GetRequiredService<IDetectionService>();
            var modInstallerService = _serviceProvider.GetRequiredService<IModInstallerService>();
            var statusService = _serviceProvider.GetRequiredService<IStatusService>();

            // Prefer the WebView2 shell; fall back to the classic WinForms shell when the
            // Edge WebView2 runtime is missing or unusable on this machine.
            if (IsWebView2RuntimeAvailable())
            {
                return new MainFormWebView(
                    configService,
                    detectionService,
                    modInstallerService,
                    statusService,
                    _serviceProvider,
                    startMinimized);
            }

            return new MainForm(
                configService,
                detectionService,
                modInstallerService,
                statusService,
                _serviceProvider,
                startMinimized);
        }

        /// <summary>
        /// Synchronous pre-flight check for the Edge WebView2 runtime. Returns false when no
        /// runtime is installed (or the probe throws), in which case the classic shell is used.
        /// </summary>
        private static bool IsWebView2RuntimeAvailable()
        {
            try
            {
                var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
                return !string.IsNullOrEmpty(version);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
