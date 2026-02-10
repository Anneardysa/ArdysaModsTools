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
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.UI.Interfaces;
using System.Windows.Forms;

namespace ArdysaModsTools.Tests.Helpers
{
    /// <summary>
    /// Factory for creating test services and mocks.
    /// Replaces ServiceLocator usage in tests with direct dependency injection.
    /// </summary>
    public class TestServiceFactory : IDisposable
    {
        #region Mocks

        /// <summary>View mock for UI testing.</summary>
        public Mock<IMainFormView> ViewMock { get; }
        
        /// <summary>Config service mock.</summary>
        public Mock<IConfigService> ConfigMock { get; }
        
        /// <summary>Logger instance for tests.</summary>
        public Logger Logger { get; }
        
        /// <summary>Captured log messages.</summary>
        public List<string> LogMessages { get; }

        #endregion

        #region Private Fields

        private readonly RichTextBox _testConsole;
        private readonly ServiceProvider _serviceProvider;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new TestServiceFactory with default mock setups.
        /// </summary>
        public TestServiceFactory()
        {
            // Create mocks
            ViewMock = new Mock<IMainFormView>();
            ConfigMock = new Mock<IConfigService>();
            LogMessages = new List<string>();
            
            // Create console and logger
            _testConsole = new RichTextBox();
            Logger = new Logger(_testConsole);
            
            // Setup default behaviors
            SetupDefaultMocks();
            
            // Build service provider
            var services = new ServiceCollection();
            services.AddSingleton(ConfigMock.Object);
            services.AddSingleton<IAppLogger>(Logger);
            _serviceProvider = services.BuildServiceProvider();
        }

        #endregion

        #region Setup Helpers

        private void SetupDefaultMocks()
        {
            // Default config behavior
            ConfigMock.Setup(c => c.GetLastTargetPath()).Returns((string?)null);
            ConfigMock.Setup(c => c.GetValue(It.IsAny<string>(), It.IsAny<bool>())).Returns(false);
            
            // Default view behavior - capture logs
            ViewMock.Setup(v => v.Log(It.IsAny<string>()))
                .Callback<string>(msg => LogMessages.Add(msg));
            
            // Default view behavior - execute UI actions immediately
            ViewMock.Setup(v => v.InvokeOnUIThread(It.IsAny<Action>()))
                .Callback<Action>(action => action());
            
            ViewMock.Setup(v => v.IsVisible).Returns(true);
        }

        /// <summary>
        /// Configures the view mock to return the specified target path.
        /// </summary>
        public TestServiceFactory WithTargetPath(string? path)
        {
            ConfigMock.Setup(c => c.GetLastTargetPath()).Returns(path);
            return this;
        }

        /// <summary>
        /// Configures message box to return the specified result.
        /// </summary>
        public TestServiceFactory WithMessageBoxResult(DialogResult result)
        {
            ViewMock.Setup(v => v.ShowMessageBox(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<MessageBoxButtons>(),
                It.IsAny<MessageBoxIcon>()
            )).Returns(result);
            return this;
        }

        #endregion

        #region Service Access

        /// <summary>
        /// Gets a service from the test service provider.
        /// </summary>
        public T GetService<T>() where T : class
        {
            return _serviceProvider.GetService<T>()!;
        }

        /// <summary>
        /// Gets a required service from the test service provider.
        /// </summary>
        public T GetRequiredService<T>() where T : notnull
        {
            return _serviceProvider.GetRequiredService<T>();
        }

        #endregion

        #region IDisposable

        /// <inheritdoc />
        public void Dispose()
        {
            _testConsole?.Dispose();
            _serviceProvider?.Dispose();
        }

        #endregion
    }
}
