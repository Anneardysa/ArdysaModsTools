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

namespace ArdysaModsTools.UI.Factories
{
    public class MainFormFactory : IMainFormFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public MainFormFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public Form Create(bool startMinimized = false)
        {
            var configService = _serviceProvider.GetRequiredService<IConfigService>();
            var detectionService = _serviceProvider.GetRequiredService<IDetectionService>();
            var modInstallerService = _serviceProvider.GetRequiredService<IModInstallerService>();
            var statusService = _serviceProvider.GetRequiredService<IStatusService>();

            return new MainFormWebView(
                configService,
                detectionService,
                modInstallerService,
                statusService,
                _serviceProvider,
                startMinimized);
        }
    }
}
