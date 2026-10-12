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
using System.Diagnostics;
using System.Text;
using ArdysaModsTools.Core.Helpers;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    public class ItemsGameOneLinerMergeTests
    {
        private const int Items = 3000;

        private static string Pretty()
        {
            var sb = new StringBuilder("\"items_game\"\n{\n\t\"items\"\n\t{\n");
            for (int i = 1; i <= Items; i++)
                sb.Append($"\t\t\"{i}\"\n\t\t{{\n\t\t\t\"name\"\t\t\"item_{i}\"\n\t\t\t\"model_player\"\t\t\"models/vanilla_{i}.vmdl\"\n\t\t}}\n");
            return sb.Append("\t}\n}\n").ToString();
        }

        private static string OneLiner(string moddedModelForId7)
        {
            var sb = new StringBuilder("\"items_game\" { \"items\" { ");
            for (int i = 1; i <= Items; i++)
            {
                string model = i == 7 ? moddedModelForId7 : $"models/vanilla_{i}.vmdl";
                sb.Append($"\"{i}\" {{ \"name\" \"item_{i}\" \"model_player\" \"{model}\" }} ");
            }
            return sb.Append("} }").ToString();
        }

        [Test]
        public void IndexSpans_OnOneLiner_GivesEachBlockItsOwnSpan()
        {
            string oneLiner = OneLiner("models/MODDED.vmdl");
            var spans = ItemsGameBlockIndex.IndexSpans(oneLiner);

            Assert.That(spans.Count, Is.EqualTo(Items));
            var block = oneLiner.Substring(spans["7"].Start, spans["7"].Length);
            Assert.That(block.TrimStart(), Does.StartWith("\"7\""));
            Assert.That(block, Does.Contain("models/MODDED.vmdl"));
            Assert.That(block, Does.Not.Contain("\"6\""), "span must not swallow everything before the block");
        }

        [Test]
        public void Merge_WithOneLinerPackage_KeepsTheModAndFinishesFast()
        {
            string vanilla = Pretty();
            string modded = OneLiner("models/MODDED.vmdl");

            var sw = Stopwatch.StartNew();
            var diff = ItemsGameBlockIndex.FindDifferingItemIds(vanilla, modded);
            var result = ItemsGameMerger.Merge(vanilla, modded, diff);
            sw.Stop();

            Assert.Multiple(() =>
            {
                Assert.That(diff, Is.EquivalentTo(new[] { "7" }), "only the customised item differs");
                Assert.That(result.Text, Does.Contain("models/MODDED.vmdl"));
                Assert.That(result.Text, Does.Contain("models/vanilla_3000.vmdl"), "untouched items survive");
                Assert.That(sw.ElapsedMilliseconds, Is.LessThan(5000),
                    "a one-liner package must not make indexing quadratic");
            });
        }
    }
}
