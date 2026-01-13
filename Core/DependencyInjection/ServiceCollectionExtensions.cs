using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Config;
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
            
            // ═══════════════════════════════════════════════════════════════
            // HERO SERVICES
            // ═══════════════════════════════════════════════════════════════
            services.AddTransient<IHeroGenerationService, HeroGenerationService>();
            
            // ═══════════════════════════════════════════════════════════════
            // NOTE: ILogger is NOT registered here because it requires a UI control
            // (RetroTerminal/RichTextBox). Forms should create their own Logger
            // and register it in a scoped container if needed.
            // ═══════════════════════════════════════════════════════════════
            
            return services;
        }
    }
}
