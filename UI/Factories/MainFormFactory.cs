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
using ArdysaModsTools.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

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
        public MainForm Create()
        {
            // Resolve all dependencies from DI container
            var configService = _serviceProvider.GetRequiredService<IConfigService>();
            var detectionService = _serviceProvider.GetRequiredService<IDetectionService>();
            var modInstallerService = _serviceProvider.GetRequiredService<IModInstallerService>();
            var statusService = _serviceProvider.GetRequiredService<IStatusService>();
            
            // Create form with constructor injection
            return new MainForm(
                configService,
                detectionService,
                modInstallerService,
                statusService);
        }
    }
}
