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
using System.Linq;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Models;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class SetCompatibilityResolverTests
    {
        private const string Cdn = "https://cdn.ardysamods.my.id/Assets/models/Abaddon/";

        private static KeyValuePair<string, List<string>> Set(string key, string stem) =>
            new(key, new List<string> { $"{Cdn}{stem}.zip", $"{Cdn}{stem}.png" });

        private static List<KeyValuePair<string, List<string>>> Catalog() => new()
        {
            new("Default Set", new List<string> { "default_skin.vpk" }),
            Set("Set 1", "blightfall"),
            Set("Set 2", "dreadmist"),
            Set("Set 3", "echoes"),
            Set("Set 4", "item_weapon_mace"),
            Set("Set 5", "phantom")
        };

        private static SetIncompatibilityRule Rule(string[] sets, string[]? with = null) =>
            new() { Sets = sets, With = with ?? System.Array.Empty<string>() };

        [Test]
        public void EffectCarriers_EffectPointsAtTheItemItWasCutFrom()
        {
            var sets = new List<KeyValuePair<string, List<string>>>
            {
                Set("Set 1", "item_head_apex"),
                Set("Set 2", "effect_ability1_head_apex"),
                Set("Set 3", "effect_ability7_head_apex"),
                Set("Set 4", "effect_ability1_back_artistry_cape"),
                Set("Set 5", "item_head_apex_2"),
                Set("Set 6", "persona_apex"),
                Set("Set 7", "base_head_apex"),
                new("Default Set", new List<string> { "default_skin.vpk" }),
            };

            var map = SetCompatibilityResolver.BuildEffectCarriers(sets);

            Assert.Multiple(() =>
            {
                Assert.That(map, Has.Length.EqualTo(sets.Count));
                Assert.That(map[1], Is.EqualTo(new[] { 0 }));
                Assert.That(map[2], Is.EqualTo(new[] { 0 }));
                Assert.That(map[3], Is.Empty);
                Assert.That(new[] { 0, 4, 5, 6, 7 }.Select(i => map[i].Count), Is.All.Zero,
                    "only an Ability Effect has carriers");
            });
        }

        [Test]
        public void EffectCarriers_WithLabels_ReachStyleSiblings_NeverASet()
        {
            var sets = new List<KeyValuePair<string, List<string>>>
            {
                Set("Set 1", "item_weapon_charm_revival"),
                Set("Set 2", "item_weapon_charm_revival_alt"),
                Set("Set 3", "item_weapon_charm"),
                Set("Set 4", "effect_ability2_weapon_charm_revival"),
                Set("Set 5", "cannonroar"),
                Set("Set 6", "effect_ability2_cannonroar"),
                Set("Set 7", "effect_ability4_weapon_charm_revival"),
                Set("Set 8", "effect_ability3_back_mulctant"),
            };
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["item_weapon_charm_revival"] = "Fin King's Charm of Eminent Revival",
                ["item_weapon_charm_revival_alt"] = "Fin King's Charm of Eminent Revival",
                ["item_weapon_charm"] = "Fin King's Charm",
                ["effect_ability2_weapon_charm_revival"] = "Fin King's Charm of Eminent Revival",
                ["cannonroar"] = "Cannonroar Confessor",
                ["effect_ability2_cannonroar"] = "Cannonroar Confessor",
                ["effect_ability4_weapon_charm_revival"] = " fin king's charm of eminent revival ",
            };

            var map = SetCompatibilityResolver.BuildEffectCarriers(sets, names);

            Assert.Multiple(() =>
            {
                Assert.That(map[3], Is.EqualTo(new[] { 0, 1 }), "both style archives of the item carry it");
                Assert.That(map[5], Is.Empty, "a set with the same label does not carry it - effects come from items only");
                Assert.That(map[6], Is.EqualTo(new[] { 0, 1 }), "labels compare trimmed, case-insensitively");
                Assert.That(map[7], Is.Empty);
                Assert.That(new[] { 0, 1, 2, 4 }.Select(i => map[i].Count), Is.All.Zero);
            });
        }

        [Test]
        public void EffectCarriers_WithoutLabels_KeepsTheExactStemRule()
        {
            var sets = new List<KeyValuePair<string, List<string>>>
            {
                Set("Set 1", "item_weapon_charm_revival"),
                Set("Set 2", "item_weapon_charm_revival_alt"),
                Set("Set 3", "effect_ability2_weapon_charm_revival"),
                Set("Set 4", "cannonroar"),
                Set("Set 5", "effect_ability2_cannonroar"),
            };

            var map = SetCompatibilityResolver.BuildEffectCarriers(sets, null);

            Assert.That(map[2], Is.EqualTo(new[] { 0 }));
            Assert.That(map[4], Is.Empty);
        }

        [Test]
        public void EffectCarriers_NullOrEmpty_IsEmpty()
        {
            Assert.That(SetCompatibilityResolver.BuildEffectCarriers(null), Is.Empty);
            Assert.That(SetCompatibilityResolver.BuildEffectCarriers(new List<KeyValuePair<string, List<string>>>()), Is.Empty);
        }

        [Test]
        public void NoRules_EveryEntryIsUnconstrained()
        {
            var map = SetCompatibilityResolver.BuildConflictMap(Catalog(), null);

            Assert.That(map, Has.Length.EqualTo(6));
            Assert.That(map.All(e => e.Count == 0), Is.True);
        }

        [Test]
        public void CrossRule_LinksBothSidesAndLeavesTheLeftSideCombinable()
        {
            var rules = new[] { Rule(new[] { "blightfall", "dreadmist", "echoes" }, new[] { "item_weapon_mace" }) };

            var map = SetCompatibilityResolver.BuildConflictMap(Catalog(), rules);

            Assert.Multiple(() =>
            {
                Assert.That(map[1], Is.EqualTo(new[] { 4 }), "blightfall should lock only the mace");
                Assert.That(map[2], Is.EqualTo(new[] { 4 }));
                Assert.That(map[3], Is.EqualTo(new[] { 4 }));
                Assert.That(map[4], Is.EqualTo(new[] { 1, 2, 3 }));
                Assert.That(map[0], Is.Empty);
                Assert.That(map[5], Is.Empty);
            });
        }

        [Test]
        public void RuleWithoutWith_MakesEveryMemberMutuallyExclusive()
        {
            var rules = new[] { Rule(new[] { "blightfall", "dreadmist", "phantom" }) };

            var map = SetCompatibilityResolver.BuildConflictMap(Catalog(), rules);

            Assert.Multiple(() =>
            {
                Assert.That(map[1], Is.EqualTo(new[] { 2, 5 }));
                Assert.That(map[2], Is.EqualTo(new[] { 1, 5 }));
                Assert.That(map[5], Is.EqualTo(new[] { 1, 2 }));
                Assert.That(map[3], Is.Empty, "echoes was not in the rule");
            });
        }

        [Test]
        public void StemOnBothSides_DoesNotLockItself()
        {
            var rules = new[] { Rule(new[] { "blightfall", "dreadmist" }, new[] { "blightfall" }) };

            var map = SetCompatibilityResolver.BuildConflictMap(Catalog(), rules);

            Assert.Multiple(() =>
            {
                Assert.That(map[1], Does.Not.Contain(1));
                Assert.That(map[1], Is.EqualTo(new[] { 2 }));
                Assert.That(map[2], Is.EqualTo(new[] { 1 }));
            });
        }

        [Test]
        public void UnknownStem_IsIgnoredAndReported()
        {
            var warnings = new List<string>();
            var rules = new[] { Rule(new[] { "blightfall", "typo_that_does_not_exist" }, new[] { "item_weapon_mace" }) };

            var map = SetCompatibilityResolver.BuildConflictMap(Catalog(), rules, warnings.Add);

            Assert.Multiple(() =>
            {
                Assert.That(map[1], Is.EqualTo(new[] { 4 }), "the resolvable half of the rule still applies");
                Assert.That(map[4], Is.EqualTo(new[] { 1 }));
                Assert.That(warnings, Has.Count.EqualTo(1));
                Assert.That(warnings[0], Does.Contain("typo_that_does_not_exist"));
            });
        }

        [Test]
        public void DuplicateStem_LinksEveryPositionCarryingIt()
        {
            var sets = Catalog();
            sets.Add(Set("Set 6", "blightfall"));

            var map = SetCompatibilityResolver.BuildConflictMap(
                sets, new[] { Rule(new[] { "blightfall" }, new[] { "item_weapon_mace" }) });

            Assert.Multiple(() =>
            {
                Assert.That(map[1], Is.EqualTo(new[] { 4 }));
                Assert.That(map[6], Is.EqualTo(new[] { 4 }));
                Assert.That(map[4], Is.EqualTo(new[] { 1, 6 }));
            });
        }

        [Test]
        public void SplitArchive_ResolvesByItsStemNotItsPartExtension()
        {
            var sets = new List<KeyValuePair<string, List<string>>>
            {
                new("Set 1", new List<string> { $"{Cdn}huge_set.zip.001", $"{Cdn}huge_set.png" }),
                Set("Set 2", "item_weapon_mace")
            };

            var map = SetCompatibilityResolver.BuildConflictMap(
                sets, new[] { Rule(new[] { "huge_set" }, new[] { "item_weapon_mace" }) });

            Assert.That(map[0], Is.EqualTo(new[] { 1 }), "'huge_set.zip.001' must resolve to stem 'huge_set'");
        }

        [Test]
        public void AuthoredWithThatMatchesNothing_DoesNotTurnIntoMutualExclusion()
        {
            var warnings = new List<string>();
            var rules = new[] { Rule(new[] { "blightfall", "dreadmist", "echoes" }, new[] { "item_weapon_mcae" }) };

            var map = SetCompatibilityResolver.BuildConflictMap(Catalog(), rules, warnings.Add);

            Assert.Multiple(() =>
            {
                Assert.That(map.All(e => e.Count == 0), Is.True);
                Assert.That(warnings, Has.Count.EqualTo(1));
                Assert.That(warnings[0], Does.Contain("item_weapon_mcae"));
            });
        }

        [Test]
        public void EmptyCatalog_ReturnsEmptyMapWithoutThrowing()
        {
            var map = SetCompatibilityResolver.BuildConflictMap(
                new List<KeyValuePair<string, List<string>>>(),
                new[] { Rule(new[] { "anything" }, new[] { "else" }) });

            Assert.That(map, Is.Empty);
        }
    }
}
