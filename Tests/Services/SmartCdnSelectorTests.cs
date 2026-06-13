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
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Services.Cdn;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    /// <summary>
    /// Tests for the SmartCdnSelector session circuit breaker (ReportFailure / ReportSuccess
    /// and the resulting GetOrderedCdnUrls demotion). The selector is a process-wide singleton,
    /// so each test resets the penalty state for every known CDN before and after running.
    /// </summary>
    [TestFixture]
    public class SmartCdnSelectorTests
    {
        private SmartCdnSelector _selector = null!;
        private string _target = null!;
        private string[] _naturalOrder = null!;

        [SetUp]
        public void Setup()
        {
            _selector = SmartCdnSelector.Instance;
            ClearAllPenalties();

            _naturalOrder = _selector.GetOrderedCdnUrls();
            // Use a CDN that is NOT already last in the natural order so demotion is observable.
            _target = _naturalOrder.First();
        }

        [TearDown]
        public void TearDown()
        {
            ClearAllPenalties();
        }

        private void ClearAllPenalties()
        {
            foreach (var cdn in CdnConfig.GetCdnBaseUrls())
                SmartCdnSelector.Instance.ReportSuccess(cdn);
        }

        [Test]
        public void GetOrderedCdnUrls_NoPenalties_ReturnsNaturalOrder()
        {
            Assert.That(_selector.GetOrderedCdnUrls(), Is.EqualTo(_naturalOrder));
        }

        [Test]
        public void ReportFailure_BelowThreshold_DoesNotDemote()
        {
            for (int i = 0; i < CdnConfig.CdnFailureThreshold - 1; i++)
                _selector.ReportFailure(_target);

            Assert.That(_selector.GetOrderedCdnUrls(), Is.EqualTo(_naturalOrder));
        }

        [Test]
        public void ReportFailure_AtThreshold_DemotesCdnToEnd()
        {
            for (int i = 0; i < CdnConfig.CdnFailureThreshold; i++)
                _selector.ReportFailure(_target);

            var ordered = _selector.GetOrderedCdnUrls();

            Assert.That(ordered.Length, Is.EqualTo(_naturalOrder.Length), "no CDN should be dropped");
            Assert.That(ordered.Last(), Is.EqualTo(_target), "tripped CDN should be last");
            Assert.That(ordered, Is.EquivalentTo(_naturalOrder), "set of CDNs is unchanged");
        }

        [Test]
        public void ReportSuccess_AfterTrip_RestoresNaturalOrder()
        {
            for (int i = 0; i < CdnConfig.CdnFailureThreshold; i++)
                _selector.ReportFailure(_target);
            Assert.That(_selector.GetOrderedCdnUrls().Last(), Is.EqualTo(_target));

            _selector.ReportSuccess(_target);

            Assert.That(_selector.GetOrderedCdnUrls(), Is.EqualTo(_naturalOrder));
        }

        [Test]
        public void ReportSuccess_ResetsFailureCount()
        {
            // Two failures, then a success, then two more — should still be below the trip threshold.
            _selector.ReportFailure(_target);
            _selector.ReportFailure(_target);
            _selector.ReportSuccess(_target);
            _selector.ReportFailure(_target);
            _selector.ReportFailure(_target);

            Assert.That(_selector.GetOrderedCdnUrls(), Is.EqualTo(_naturalOrder));
        }
    }
}
