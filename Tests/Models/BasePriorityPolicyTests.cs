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
using ArdysaModsTools.Models;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Models
{
    [TestFixture]
    public class BasePriorityPolicyTests
    {
        [Test]
        public void EmptyPolicy_FallsBackToDetection()
        {
            var policy = new BasePriorityPolicy();

            Assert.That(policy.BaseWins(null, null, true), Is.True);
            Assert.That(policy.BaseWins("Some Set", 123, false), Is.False);
            Assert.That(policy.HasOverrides, Is.False);
            Assert.That(policy.ScopeOf("Some Set", 123), Is.EqualTo("auto"));
        }

        [Test]
        public void HeroDefault_OverridesDetection()
        {
            var one = new BasePriorityPolicy { Default = 1 };
            var two = new BasePriorityPolicy { Default = 2 };

            Assert.That(one.BaseWins("Any", 1, false), Is.True);
            Assert.That(two.BaseWins("Any", 1, true), Is.False);
            Assert.That(one.ScopeOf("Any", 1), Is.EqualTo("hero"));
        }

        [Test]
        public void OutOfRangeDefault_FallsBackToDetection()
        {
            foreach (var bogus in new int?[] { 0, 3, 99, -1 })
            {
                var policy = new BasePriorityPolicy { Default = bogus };
                Assert.That(policy.BaseWins(null, null, true), Is.True, $"default={bogus}");
                Assert.That(policy.BaseWins(null, null, false), Is.False, $"default={bogus}");
            }
        }

        [Test]
        public void SetOverride_BeatsHeroDefault()
        {
            var policy = new BasePriorityPolicy { Default = 1 };
            policy.Sets["Blazing Superiority"] = 2;

            Assert.That(policy.BaseWins("Blazing Superiority", null, false), Is.False);
            Assert.That(policy.ScopeOf("Blazing Superiority", null), Is.EqualTo("set"));

            Assert.That(policy.BaseWins("Golden Basher", null, false), Is.True);
        }

        [Test]
        public void SetOverride_IsCaseInsensitive_AndMatchesFlattenedStyleKeys()
        {
            var policy = new BasePriorityPolicy { Default = 1 };
            policy.Sets["Manifold Paradox"] = 2;

            Assert.That(policy.BaseWins("manifold paradox", null, true), Is.False);

            Assert.That(policy.BaseWins("Manifold Paradox (Corrupted)", null, true), Is.False);
            Assert.That(policy.ScopeOf("Manifold Paradox (Corrupted)", null), Is.EqualTo("set"));

            Assert.That(policy.BaseWins("Other Set (Corrupted)", null, true), Is.True);
        }

        [Test]
        public void ItemOverride_BeatsSetOverrideAndHeroDefault()
        {
            var policy = new BasePriorityPolicy { Default = 1 };
            policy.Sets["Blazing Superiority"] = 1;
            policy.Items[12345] = 2;

            Assert.That(policy.BaseWins("Blazing Superiority", 12345, true), Is.False);
            Assert.That(policy.ScopeOf("Blazing Superiority", 12345), Is.EqualTo("item"));

            Assert.That(policy.BaseWins("Blazing Superiority", 999, true), Is.True);
        }

        [Test]
        public void InvalidOverrideValues_FallThroughToTheNextScope()
        {
            var policy = new BasePriorityPolicy { Default = 2 };
            policy.Sets["Weird Set"] = 7;
            policy.Items[12345] = 0;

            Assert.That(policy.BaseWins("Weird Set", 12345, true), Is.False, "must fall through to default=2");
            Assert.That(policy.ScopeOf("Weird Set", 12345), Is.EqualTo("hero"));
        }

        [Test]
        public void Clone_IsIndependentOfTheSource()
        {
            var source = new BasePriorityPolicy { Default = 1 };
            source.Sets["A"] = 2;
            source.Items[7] = 2;

            var copy = source.Clone();
            copy.Default = 2;
            copy.Sets["A"] = 1;
            copy.Items[7] = 1;

            Assert.That(source.Default, Is.EqualTo(1));
            Assert.That(source.Sets["A"], Is.EqualTo(2));
            Assert.That(source.Items[7], Is.EqualTo(2));
        }
    }
}
