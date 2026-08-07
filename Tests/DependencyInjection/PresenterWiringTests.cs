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
using System.Threading;
using System.Windows.Forms;
using ArdysaModsTools.Core.DependencyInjection;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.UI.Interfaces;
using ArdysaModsTools.UI.Presenters;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.DependencyInjection
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class PresenterWiringTests
    {
        private ServiceProvider _provider = null!;
        private RichTextBox _console = null!;

        [SetUp]
        public void Setup()
        {
            _provider = new ServiceCollection().AddArdysaServices().BuildServiceProvider();
            _console = new RichTextBox();
        }

        [TearDown]
        public void TearDown()
        {
            _console?.Dispose();
            _provider?.Dispose();
        }

        private MainFormPresenter CreatePresenterAsTheAppDoes()
        {
            var factory = _provider.GetRequiredService<Func<IMainFormView, Logger, MainFormPresenter>>();
            return factory(new Mock<IMainFormView>().Object, new Logger(_console));
        }

        [Test]
        public void TheContainerCanBuildTheMainPresenter()
        {
            Assert.That(CreatePresenterAsTheAppDoes(), Is.Not.Null);
        }

        [Test]
        public void ThePresenterTheAppBuilds_CanRepairAndLaunch()
        {
            using var presenter = CreatePresenterAsTheAppDoes();

            Assert.That(presenter.CanRepairAndLaunch, Is.True,
                "Play is wired to the package-repair services, or it can only ever refuse to launch");
        }

        [Test]
        public void ThePresenterTheAppBuilds_CanVerifyPackageSync()
        {
            using var presenter = CreatePresenterAsTheAppDoes();

            Assert.That(presenter.CanVerifyPackageSync, Is.True,
                "without a verdict source the check reports 'not verified' forever");
        }

        [Test]
        public void BuiltWithoutTheRepairServices_TheSymptomsAreReproduced()
        {
            using var presenter = new MainFormPresenter(
                new Mock<IMainFormView>().Object,
                new Logger(_console),
                _provider.GetRequiredService<IConfigService>(),
                _provider.GetRequiredService<IStatusService>());

            Assert.Multiple(() =>
            {
                Assert.That(presenter.CanRepairAndLaunch, Is.False, "this is what made Play refuse to launch");
                Assert.That(presenter.CanVerifyPackageSync, Is.False, "this is what pinned Package Sync to 'not verified'");
            });
        }

        [TestCase(typeof(IConfigService))]
        [TestCase(typeof(IStatusService))]
        [TestCase(typeof(ISetupVerificationService))]
        [TestCase(typeof(IItemsGameSyncService))]
        [TestCase(typeof(IItemsGameMergeService))]
        [TestCase(typeof(ISteamAppStateService))]
        [TestCase(typeof(IGameItemsGameExtractor))]
        [TestCase(typeof(IVpkExtractor))]
        [TestCase(typeof(IVpkRecompiler))]
        [TestCase(typeof(IVpkReplacer))]
        public void EveryPresenterDependencyIsRegistered(Type service)
        {
            Assert.That(_provider.GetService(service), Is.Not.Null, $"{service.Name} is not registered");
        }

        [Test]
        public void PackageSyncIsASingletonSoItsCachedVerdictSurvives()
        {
            var a = _provider.GetRequiredService<IItemsGameSyncService>();
            var b = _provider.GetRequiredService<IItemsGameSyncService>();

            Assert.That(a, Is.SameAs(b));
        }

        [Test]
        public void TheStatusPipelinesVerificationSweepSharesTheSameSyncService()
        {
            var shared = _provider.GetRequiredService<IItemsGameSyncService>();

            var verification = _provider.GetRequiredService<ISetupVerificationService>();

            Assert.That(verification, Is.Not.Null);
            Assert.That(_provider.GetRequiredService<IItemsGameSyncService>(), Is.SameAs(shared));
        }
    }
}
