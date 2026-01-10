using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Services;
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
            // Note: Logger requires UI control (RetroTerminal/RichTextBox), 
            // so ILogger is registered per-form and not here.

            // Core Mod Services (with interfaces)
            services.AddTransient<IModInstallerService, ModInstallerService>();
            services.AddTransient<IStatusService, StatusService>();
            
            // Hero Services (with interface)
            services.AddTransient<IHeroGenerationService, HeroGenerationService>();
            
            return services;
        }
    }
}
