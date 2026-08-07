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
using System.Linq;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Presenters
{
    [TestFixture]
    public class PackageSyncNoticeTests
    {
        private static SetupVerificationResult SweepWith(SetupCheckState state) => new()
        {
            Checks = new[]
            {
                new SetupCheck { Id = SetupCheckId.SignatureMatchesGameInfo, State = SetupCheckState.Pass },
                new SetupCheck
                {
                    Id = SetupCheckId.ItemsGameInSync,
                    State = state,
                    DetailKey = "verify.sync.fail",
                    HasOwnDialog = state == SetupCheckState.Fail,
                    FailStatus = ModStatus.NeedUpdate,
                    FailAction = RecommendedAction.Play
                }
            }
        };

        [Test]
        public void AStalePackage_CarriesItsOwnPanel()
        {
            var check = SweepWith(SetupCheckState.Fail).Checks.First(c => c.Id == SetupCheckId.ItemsGameInSync);

            Assert.That(check.HasOwnDialog, Is.True);
        }

        [Test]
        public void AStalePackage_DoesNotClaimTheSharedFixButton()
        {
            var sweep = SweepWith(SetupCheckState.Fail);

            Assert.That(sweep.Checks.Any(c => c.CanAutoFix), Is.False);
        }

        [Test]
        public void AHealthyPackage_HasNoPanelAndDoesNotBlockReady()
        {
            var sweep = SweepWith(SetupCheckState.Pass);
            var check = sweep.Checks.First(c => c.Id == SetupCheckId.ItemsGameInSync);

            Assert.Multiple(() =>
            {
                Assert.That(check.HasOwnDialog, Is.False);
                Assert.That(sweep.AllPassed, Is.True);
            });
        }

        [Test]
        public void AnUnverifiablePackage_IsNotReportedAsAProblem()
        {
            var sweep = SweepWith(SetupCheckState.Unknown);

            Assert.Multiple(() =>
            {
                Assert.That(sweep.AllPassed, Is.True);
                Assert.That(sweep.FirstFailure, Is.Null);
            });
        }
    }
}
