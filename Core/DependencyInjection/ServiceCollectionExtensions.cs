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
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Config;
using ArdysaModsTools.Core.Services.FileTransactions;
using Microsoft.Extensions.DependencyInjection;

namespace ArdysaModsTools.Core.DependencyInjection
{
    /// <summary>
    /// Extension methods for configuring dependency injection.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds all ArdysaModsTools services to the DI container.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddArdysaServices(this IServiceCollection services)
        {
            // ═══════════════════════════════════════════════════════════════
            // CORE SERVICES
            // ═══════════════════════════════════════════════════════════════
            
            // Mod Installation
            services.AddTransient<IModInstallerService, ModInstallerService>();
            services.AddTransient<IStatusService, StatusService>();
            
            // Detection
            services.AddTransient<IDetectionService, DetectionService>();
            
            // Configuration (singleton - shared state)
            services.AddSingleton<IConfigService, MainConfigService>();
            
            // File Transactions (transient - each user gets new factory)
            services.AddTransient<IFileTransactionFactory, FileTransactionFactory>();
            
            // ═══════════════════════════════════════════════════════════════
            // CONFLICT RESOLUTION SERVICES
            // ═══════════════════════════════════════════════════════════════
            services.AddSingleton<IConflictDetector, Services.Conflict.ConflictDetector>();
            services.AddSingleton<IConflictResolver, Services.Conflict.ConflictResolver>();
            services.AddSingleton<IModPriorityService, Services.Conflict.ModPriorityService>();
            
            // ═══════════════════════════════════════════════════════════════
            // HERO SERVICES
            // ═══════════════════════════════════════════════════════════════
            services.AddTransient<IHeroGenerationService, HeroGenerationService>();
            
            // ═══════════════════════════════════════════════════════════════
            // LOGGING
            // NullLogger is registered as default. UI can replace this with
            // a real Logger instance after the form is initialized.
            // ═══════════════════════════════════════════════════════════════
            services.AddSingleton<IAppLogger>(NullLogger.Instance);
            
            // ═══════════════════════════════════════════════════════════════
            // UI FACTORIES
            // Factory pattern for WinForms that can't use constructor injection
            // ═══════════════════════════════════════════════════════════════
            services.AddSingleton<UI.Factories.IMainFormFactory, UI.Factories.MainFormFactory>();
            
            return services;
        }
    }
}

