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
    public class TestServiceFactory : IDisposable
    {
        #region Mocks

        public Mock<IMainFormView> ViewMock { get; }
        
        public Mock<IConfigService> ConfigMock { get; }
        
        public Logger Logger { get; }
        
        public List<string> LogMessages { get; }

        #endregion

        #region Private Fields

        private readonly RichTextBox _testConsole;
        private readonly ServiceProvider _serviceProvider;

        #endregion

        #region Constructor

        public TestServiceFactory()
        {
            ViewMock = new Mock<IMainFormView>();
            ConfigMock = new Mock<IConfigService>();
            LogMessages = new List<string>();
            
            _testConsole = new RichTextBox();
            Logger = new Logger(_testConsole);
            
            SetupDefaultMocks();
            
            var services = new ServiceCollection();
            services.AddSingleton(ConfigMock.Object);
            services.AddSingleton<IAppLogger>(Logger);
            _serviceProvider = services.BuildServiceProvider();
        }

        #endregion

        #region Setup Helpers

        private void SetupDefaultMocks()
        {
            ConfigMock.Setup(c => c.GetLastTargetPath()).Returns((string?)null);
            ConfigMock.Setup(c => c.GetValue(It.IsAny<string>(), It.IsAny<bool>())).Returns(false);
            
            ViewMock.Setup(v => v.Log(It.IsAny<string>()))
                .Callback<string>(msg => LogMessages.Add(msg));
            
            ViewMock.Setup(v => v.InvokeOnUIThread(It.IsAny<Action>()))
                .Callback<Action>(action => action());
            
            ViewMock.Setup(v => v.IsVisible).Returns(true);
        }

        public TestServiceFactory WithTargetPath(string? path)
        {
            ConfigMock.Setup(c => c.GetLastTargetPath()).Returns(path);
            return this;
        }

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

        public T GetService<T>() where T : class
        {
            return _serviceProvider.GetService<T>()!;
        }

        public T GetRequiredService<T>() where T : notnull
        {
            return _serviceProvider.GetRequiredService<T>();
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _testConsole?.Dispose();
            _serviceProvider?.Dispose();
        }

        #endregion
    }
}
