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
using System.Collections.Generic;
using System.Linq;
using ArdysaModsTools.Core.Models;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Models
{
    [TestFixture]
    public class SetUpdatesDataTests
    {
        private static readonly DateTime Today = new(2026, 9, 29);

        private static SetUpdateBatch Batch(DateTime date, params string[] sets) => new()
        {
            Date = date,
            Sets = sets.Select(s => new SetUpdateEntry { HeroId = "npc_dota_hero_axe", SetName = s }).ToList()
        };

        private static SetUpdatesData History(string version = "1.0.176")
        {
            var batches = new List<SetUpdateBatch>();
            for (int day = 0; day < 30; day++)
            {
                var date = Today.AddDays(-day);
                batches.Add(Batch(date, Enumerable.Range(0, 30).Select(i => $"d{day}-set{i}").ToArray()));
            }
            return new SetUpdatesData { Version = version, Updates = batches };
        }

        [Test]
        public void ThumbnailEpoch_CarriesVersionAndNewestBatchDate()
        {
            Assert.That(History().ThumbnailEpoch, Is.EqualTo("1.0.176|2026-09-29"));
        }

        [Test]
        public void ThumbnailEpoch_MovesWhenVersionBumpsOnTheSameDay()
        {
            Assert.That(History("1.0.177").ThumbnailEpoch, Is.Not.EqualTo(History("1.0.176").ThumbnailEpoch));
        }

        [Test]
        public void GetUpdatesSinceEpoch_AfterAnUpload_ReturnsOnlyTheNewBatches_NotTheWholeHistory()
        {
            var lastSeen = "1.0.175|2026-09-28";

            var changed = History().GetUpdatesSinceEpoch(lastSeen);

            Assert.That(changed, Has.Count.EqualTo(60), "yesterday's batch (same-day re-uploads) + today's");
            Assert.That(changed.Select(c => c.AddedDate).Distinct(),
                Is.EquivalentTo(new[] { Today, Today.AddDays(-1) }));
        }

        [Test]
        public void GetUpdatesSinceEpoch_SecondUploadSameDay_IncludesThatDay()
        {
            var changed = History("1.0.177").GetUpdatesSinceEpoch("1.0.176|2026-09-29");

            Assert.That(changed.Select(c => c.AddedDate).Distinct(), Is.EqualTo(new[] { Today }));
        }

        [Test]
        public void GetUpdatesSinceEpoch_LongAbsence_CoversEveryBatchSinceLastSeen()
        {
            var changed = History().GetUpdatesSinceEpoch("1.0.150|2026-09-19");

            Assert.That(changed.Select(c => c.AddedDate).Distinct().Count(), Is.EqualTo(11));
            Assert.That(changed.All(c => c.AddedDate >= new DateTime(2026, 9, 19)), Is.True);
        }

        [TestCase("1.0.175")]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("1.0.175|not-a-date")]
        public void GetUpdatesSinceEpoch_UnreadableLastSeen_FallsBackToNewestBatchOnly(string? lastSeen)
        {
            var changed = History().GetUpdatesSinceEpoch(lastSeen);

            Assert.That(changed, Has.Count.EqualTo(30));
            Assert.That(changed.All(c => c.AddedDate == Today), Is.True);
        }

        [Test]
        public void GetUpdatesSinceEpoch_NoBatches_ReturnsEmpty()
        {
            var data = new SetUpdatesData { Version = "1.0.0" };

            Assert.That(data.GetUpdatesSinceEpoch("1.0.0|2026-09-01"), Is.Empty);
            Assert.That(data.GetUpdatesSinceEpoch(null), Is.Empty);
            Assert.That(data.ThumbnailEpoch, Is.EqualTo("1.0.0|"));
        }
    }
}
