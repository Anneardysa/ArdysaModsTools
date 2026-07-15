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
    public static class ServiceCollectionExtensions
    {
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

        public static IServiceCollection AddCoreServices(this IServiceCollection services)
        {
            services.AddTransient<IModInstallerService, ModInstallerService>();
            services.AddTransient<IStatusService, StatusService>();
            services.AddTransient<IActiveModsService, ActiveModsService>();
            
            services.AddTransient<IDetectionService, DetectionService>();
            
            services.AddSingleton<IConfigService, MainConfigService>();

            services.AddSingleton<ILocalizationService, Services.Localization.LocalizationService>();
            services.AddTransient<IAutoexecService, Services.Misc.AutoexecService>();

            services.AddSingleton<IAssetPreloadService, Services.Cache.AssetPreloadService>();

            return services;
        }

        public static IServiceCollection AddConflictServices(this IServiceCollection services)
        {
            services.AddSingleton<IConflictDetector, Services.Conflict.ConflictDetector>();
            services.AddSingleton<IConflictResolver, Services.Conflict.ConflictResolver>();
            services.AddSingleton<IModPriorityService, Services.Conflict.ModPriorityService>();
            
            return services;
        }

        public static IServiceCollection AddHeroServices(this IServiceCollection services)
        {
            services.AddTransient<IHeroGenerationService, HeroGenerationService>();

            services.AddTransient<IHeroDatabaseService>(sp =>
                new HeroDatabaseService(System.AppContext.BaseDirectory, sp.GetService<IAppLogger>()));

            return services;
        }

        public static IServiceCollection AddLoggingServices(this IServiceCollection services)
        {
            services.AddSingleton<IAppLogger>(NullLogger.Instance);
            
            return services;
        }

        public static IServiceCollection AddPresenters(this IServiceCollection services)
        {
            services.AddTransient<INavigationPresenter, UI.Presenters.NavigationPresenter>();
            
            services.AddTransient<Func<UI.Interfaces.IDota2PerformanceView, string?, UI.Presenters.Dota2PerformancePresenter>>(
                provider => (view, path) => new UI.Presenters.Dota2PerformancePresenter(
                    view,
                    provider.GetRequiredService<IAutoexecService>(),
                    provider.GetRequiredService<IAppLogger>(),
                    path));
            
            return services;
        }

        public static IServiceCollection AddUIFactories(this IServiceCollection services)
        {
            services.AddSingleton<UI.Factories.IMainFormFactory, UI.Factories.MainFormFactory>();
            
            return services;
        }
    }
}

