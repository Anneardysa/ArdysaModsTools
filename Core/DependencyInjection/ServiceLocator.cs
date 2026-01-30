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
using Microsoft.Extensions.DependencyInjection;

namespace ArdysaModsTools.Core.DependencyInjection
{
    /// <summary>
    /// Service locator for accessing the DI container.
    /// Used for transitional support while migrating to full DI.
    /// </summary>
    /// <remarks>
    /// This is a temporary solution to enable DI in forms that cannot receive
    /// constructor injection. As the codebase matures, direct constructor 
    /// injection should be preferred. Use IMainFormFactory for MainForm.
    /// </remarks>
    [Obsolete("Use constructor injection instead. For MainForm, use IMainFormFactory. Will be removed in v3.0.")]
    public static class ServiceLocator
    {
        private static IServiceProvider? _serviceProvider;
        private static IServiceScope? _currentScope;

        /// <summary>
        /// Gets the current service provider. Throws if not initialized.
        /// </summary>
        public static IServiceProvider Services
        {
            get
            {
                if (_serviceProvider == null)
                    throw new InvalidOperationException(
                        "ServiceLocator has not been initialized. Call Initialize() in Program.Main().");
                return _serviceProvider;
            }
        }

        /// <summary>
        /// Gets whether the service locator has been initialized.
        /// </summary>
        public static bool IsInitialized => _serviceProvider != null;

        /// <summary>
        /// Initializes the service locator with the specified service provider.
        /// </summary>
        /// <param name="serviceProvider">The built service provider.</param>
        public static void Initialize(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>
        /// Gets a required service of the specified type.
        /// </summary>
        /// <typeparam name="T">The service type.</typeparam>
        /// <returns>The service instance.</returns>
        /// <exception cref="InvalidOperationException">If the service is not registered.</exception>
        public static T GetRequired<T>() where T : notnull
        {
            return Services.GetRequiredService<T>();
        }

        /// <summary>
        /// Gets an optional service of the specified type.
        /// </summary>
        /// <typeparam name="T">The service type.</typeparam>
        /// <returns>The service instance, or null if not registered.</returns>
        public static T? Get<T>() where T : class
        {
            return Services.GetService<T>();
        }

        /// <summary>
        /// Creates a new scope for scoped services.
        /// </summary>
        /// <returns>A new service scope.</returns>
        public static IServiceScope CreateScope()
        {
            return Services.CreateScope();
        }

        /// <summary>
        /// Disposes of the service locator resources.
        /// </summary>
        public static void Dispose()
        {
            _currentScope?.Dispose();
            _currentScope = null;

            if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _serviceProvider = null;
        }
    }
}

