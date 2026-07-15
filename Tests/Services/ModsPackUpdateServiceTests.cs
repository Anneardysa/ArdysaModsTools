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
using NUnit.Framework;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Mods;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class ModsPackUpdateServiceTests
    {
        private ModsPackUpdateService _service = null!;
        private string _dir = null!;

        [SetUp]
        public void Setup()
        {
            _service = new ModsPackUpdateService(new ModInstallerService(null));
            _dir = Path.Combine(Path.GetTempPath(), $"AMT_ModsPackUpdateTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_dir, true); } catch {  }
        }

        [Test]
        public void Constructor_WithNullInstaller_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ModsPackUpdateService(null!));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public async Task CheckForUpdateAsync_WithNoPath_ReturnsFalse(string? path)
        {
            Assert.That(await _service.CheckForUpdateAsync(path!), Is.False);
        }

        [Test]
        public async Task CheckForUpdateAsync_WithNoLocalInstall_ReturnsFalse()
        {
            Assert.That(await _service.CheckForUpdateAsync(_dir), Is.False);
        }

        [Test]
        public void CheckForUpdateAsync_WhenAlreadyCancelled_Throws()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(
                async () => await _service.CheckForUpdateAsync(_dir, cts.Token));
        }
    }
}
