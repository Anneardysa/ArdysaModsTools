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
using System.Collections.Generic;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Services.Config;
using ArdysaModsTools.Core.Services.Localization;
using ArdysaModsTools.Helpers;
using ArdysaModsTools.UI.Interfaces;
using Moq;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    public class FeatureBlockedNoticeTests
    {
        private const string Feature = FeatureAccessService.InstallModsPackFeature;

        private Mock<IMainFormView> _view = null!;
        private List<(string Eyebrow, string Note, string Confirm)> _shown = null!;

        [SetUp]
        public void SetUp()
        {
            _view = new Mock<IMainFormView>();
            _shown = new List<(string, string, string)>();
        }

        private static FeatureCheckResult Offline() =>
            FeatureCheckResult.Offline(Feature, "Install ModsPack", "Server unreachable.");

        private void Answers(params bool[] answers)
        {
            var queue = new Queue<bool>(answers);
            _view.Setup(v => v.ShowShellConfirmAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
                .Callback<string, string, string, string, string, string, int, string>(
                    (eyebrow, _, _, note, confirm, _, _, _) => _shown.Add((eyebrow, note, confirm)))
                .ReturnsAsync(() => queue.Count > 0 && queue.Dequeue());
        }

        private static (System.Func<string, Task<FeatureCheckResult>> Fn, System.Func<int> Calls) Recheck(
            params FeatureCheckResult[] results)
        {
            int calls = 0;
            return (name =>
            {
                Assert.That(name, Is.EqualTo(Feature), "Re-check must target the blocked feature.");
                return Task.FromResult(results[System.Math.Min(calls++, results.Length - 1)]);
            }, () => calls);
        }

        [Test]
        public async Task Offline_ShowsCantReachServer_WithRetryAndWarpTip_NotJoinDiscord()
        {
            Answers(false);

            await UIHelpers.ShowFeatureBlockedAsync(_view.Object, Offline(), recheck: Recheck().Fn);

            Assert.That(_shown, Has.Count.EqualTo(1));
            Assert.That(_shown[0].Eyebrow, Is.EqualTo(Loc.T("feature.offline.title")));
            Assert.That(_shown[0].Eyebrow, Is.Not.EqualTo(Loc.T("feature.unavailable.title")));
            Assert.That(_shown[0].Confirm, Is.EqualTo(Loc.T("feature.offline.retry")));
            Assert.That(_shown[0].Confirm, Is.Not.EqualTo(Loc.T("feature.unavailable.joinDiscord")));
            Assert.That(_shown[0].Note, Is.EqualTo(Loc.T("feature.offline.note")));
        }

        [Test]
        public async Task Offline_RetrySucceeds_ReturnsTrue_SoTheActionContinues()
        {
            Answers(true);
            var recheck = Recheck(FeatureCheckResult.Allowed("Install ModsPack", featureName: Feature));

            bool proceed = await UIHelpers.ShowFeatureBlockedAsync(_view.Object, Offline(), recheck: recheck.Fn);

            Assert.That(proceed, Is.True);
            Assert.That(_shown, Has.Count.EqualTo(1));
            Assert.That(recheck.Calls(), Is.EqualTo(1));
        }

        [Test]
        public async Task Offline_Close_ReturnsFalse_AndNeverRechecks()
        {
            Answers(false);
            var recheck = Recheck();

            bool proceed = await UIHelpers.ShowFeatureBlockedAsync(_view.Object, Offline(), recheck: recheck.Fn);

            Assert.That(proceed, Is.False);
            Assert.That(recheck.Calls(), Is.EqualTo(0));
        }

        [Test]
        public async Task Offline_RetryStillOffline_ShowsNoticeAgain_ThenCloseReturnsFalse()
        {
            Answers(true, false);
            var recheck = Recheck(Offline());

            bool proceed = await UIHelpers.ShowFeatureBlockedAsync(_view.Object, Offline(), recheck: recheck.Fn);

            Assert.That(proceed, Is.False);
            Assert.That(_shown, Has.Count.EqualTo(2));
            Assert.That(_shown, Has.All.Matches<(string Eyebrow, string, string)>(
                s => s.Eyebrow == Loc.T("feature.offline.title")));
            Assert.That(recheck.Calls(), Is.EqualTo(1));
        }

        [Test]
        public async Task Offline_RetryBecomesOutdated_HandsOffToUpdateNotice_ReturnsFalse()
        {
            Answers(true, false);
            var recheck = Recheck(FeatureCheckResult.Outdated("Install ModsPack", "Too old.", "2.7.0 (Build 2431)", Feature));

            bool proceed = await UIHelpers.ShowFeatureBlockedAsync(_view.Object, Offline(), recheck: recheck.Fn);

            Assert.That(proceed, Is.False);
            Assert.That(_shown, Has.Count.EqualTo(2));
            Assert.That(_shown[1].Eyebrow, Is.EqualTo(Loc.T("update.required.title")));
        }

        [Test]
        public async Task Disabled_ShowsFeatureUnavailable_WithJoinDiscord_NoRetry()
        {
            Answers(false);
            var recheck = Recheck();
            var disabled = FeatureCheckResult.Blocked("Install ModsPack", "Maintenance.", Feature);

            bool proceed = await UIHelpers.ShowFeatureBlockedAsync(_view.Object, disabled, recheck: recheck.Fn);

            Assert.That(proceed, Is.False);
            Assert.That(_shown, Has.Count.EqualTo(1));
            Assert.That(_shown[0].Eyebrow, Is.EqualTo(Loc.T("feature.unavailable.title")));
            Assert.That(_shown[0].Confirm, Is.EqualTo(Loc.T("feature.unavailable.joinDiscord")));
            Assert.That(recheck.Calls(), Is.EqualTo(0));
        }
    }
}
