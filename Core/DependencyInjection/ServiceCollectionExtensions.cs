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
    /// Provides specialized registration methods for each service category.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds all ArdysaModsTools services to the DI container.
        /// This is the main entry point for service registration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddArdysaServices(this IServiceCollection services)
        {
            return services
                .AddCoreServices()
                .AddConflictServices()
                .AddHeroServices()
                .AddLoggingServices()
                .AddPresenters()
                .AddUIFactories();
        }

        /// <summary>
        /// Adds core mod installation and detection services.
        /// </summary>
        public static IServiceCollection AddCoreServices(this IServiceCollection services)
        {
            // Mod Installation
            services.AddTransient<IModInstallerService, ModInstallerService>();
            services.AddTransient<IStatusService, StatusService>();
            services.AddTransient<IActiveModsService, ActiveModsService>();
            
            // Detection
            services.AddTransient<IDetectionService, DetectionService>();
            
            // Configuration (singleton - shared state)
            services.AddSingleton<IConfigService, MainConfigService>();
            
            // File Transactions (transient - each user gets new factory)
            services.AddTransient<IFileTransactionFactory, FileTransactionFactory>();
            
            return services;
        }

        /// <summary>
        /// Adds conflict detection and resolution services.
        /// </summary>
        public static IServiceCollection AddConflictServices(this IServiceCollection services)
        {
            services.AddSingleton<IConflictDetector, Services.Conflict.ConflictDetector>();
            services.AddSingleton<IConflictResolver, Services.Conflict.ConflictResolver>();
            services.AddSingleton<IModPriorityService, Services.Conflict.ModPriorityService>();
            
            return services;
        }

        /// <summary>
        /// Adds hero selection and generation services.
        /// </summary>
        public static IServiceCollection AddHeroServices(this IServiceCollection services)
        {
            services.AddTransient<IHeroGenerationService, HeroGenerationService>();
            
            return services;
        }

        /// <summary>
        /// Adds logging services.
        /// NullLogger is registered as default. UI can replace with real Logger.
        /// </summary>
        public static IServiceCollection AddLoggingServices(this IServiceCollection services)
        {
            services.AddSingleton<IAppLogger>(NullLogger.Instance);
            
            return services;
        }

        /// <summary>
        /// Adds specialized UI presenters extracted from MainFormPresenter for SRP.
        /// </summary>
        public static IServiceCollection AddPresenters(this IServiceCollection services)
        {
            services.AddTransient<IModOperationsPresenter, UI.Presenters.ModOperationsPresenter>();
            services.AddTransient<IPatchPresenter, UI.Presenters.PatchPresenter>();
            services.AddTransient<INavigationPresenter, UI.Presenters.NavigationPresenter>();
            
            return services;
        }

        /// <summary>
        /// Adds UI factories for WinForms that can't use constructor injection.
        /// </summary>
        public static IServiceCollection AddUIFactories(this IServiceCollection services)
        {
            services.AddSingleton<UI.Factories.IMainFormFactory, UI.Factories.MainFormFactory>();
            
            return services;
        }
    }
}

