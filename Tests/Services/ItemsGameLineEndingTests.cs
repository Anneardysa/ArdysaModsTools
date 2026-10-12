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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Helpers;
using ArdysaModsTools.Core.Services;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class ItemsGameLineEndingTests
    {
        private const string Cr = "\r";

        [Test]
        public async Task ExtractedItemsGame_IsWrittenWithoutCarriageReturns()
        {
            string dir = Path.Combine(Path.GetTempPath(), "AmtLfTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string source = Path.Combine(dir, "source.txt");
                string dest = Path.Combine(dir, "items_game.txt");

                string crlf = "\"items_game\"\r\n{\r\n\t\"items\"\r\n\t{\r\n\t}\r\n}\r\n";
                await File.WriteAllTextAsync(source, crlf);

                await GameItemsGameExtractorService.CopyForBuildAsync(source, dest, CancellationToken.None);

                string written = await File.ReadAllTextAsync(dest);
                Assert.That(written, Does.Not.Contain(Cr), "CR reached the build copy.");
                Assert.That(written, Is.EqualTo(crlf.Replace("\r\n", "\n")),
                    "Normalising changed more than the line endings.");
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch {  }
            }
        }

        [Test]
        public async Task LoneCarriageReturn_BecomesALineBreak_NotNothing()
        {
            await AssertNormalisesAsync("\"a\"\r\"b\"\r\n\"c\"", "\"a\"\n\"b\"\n\"c\"");
        }

        [Test]
        public async Task CrlfSplitAcrossTheReadBoundary_CollapsesToOneLineBreak()
        {
            const int BufferSize = 1 << 20;
            string filler = new string('x', BufferSize - 1);

            await AssertNormalisesAsync(filler + "\r\n\"tail\"", filler + "\n\"tail\"");
        }

        private static async Task AssertNormalisesAsync(string input, string expected)
        {
            string dir = Path.Combine(Path.GetTempPath(), "AmtLfTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string source = Path.Combine(dir, "source.txt");
                string dest = Path.Combine(dir, "items_game.txt");
                await File.WriteAllTextAsync(source, input);

                await GameItemsGameExtractorService.CopyForBuildAsync(source, dest, CancellationToken.None);

                Assert.That(await File.ReadAllTextAsync(dest), Is.EqualTo(expected));
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch {  }
            }
        }

        [Test]
        public void ReplaceIdBlock_WithACrlfAuthoredBlock_SplicesItAsLf()
        {
            string content =
                "\"items_game\"\n{\n\t\"items\"\n\t{\n" +
                "\t\t\"595\"\n\t\t{\n\t\t\t\"prefab\"\t\"courier\"\n\t\t\t\"name\"\t\"old\"\n\t\t}\n" +
                "\t}\n}\n";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("\t\t\"595\"");
            sb.AppendLine("\t\t{");
            sb.AppendLine("\t\t\t\"prefab\"\t\"courier\"");
            sb.AppendLine("\t\t\t\"name\"\t\"merged\"");
            sb.AppendLine("\t\t}");
            string authored = sb.ToString();

            Assume.That(authored, Does.Contain(Cr), "the fixture must reproduce how these blocks are built");

            string patched = KeyValuesBlockHelper.ReplaceIdBlock(content, "595", authored, out bool replaced);

            Assert.That(replaced, Is.True);
            Assert.That(patched, Does.Contain("merged"));
            Assert.That(patched, Does.Not.Contain(Cr), "a CRLF block was spliced into an LF package.");
        }

        [Test]
        public void Merge_OnCrlfVanilla_ProducesLfOnlyOutput()
        {
            string vanilla =
                "\"items_game\"\r\n" +
                "{\r\n" +
                "\t\"items\"\r\n" +
                "\t{\r\n" +
                "\t\t\"1\"\r\n" +
                "\t\t{\r\n" +
                "\t\t\t\"name\"\t\t\"axe\"\r\n" +
                "\t\t\t\"model_player\"\t\t\"models/axe.vmdl\"\r\n" +
                "\t\t}\r\n" +
                "\t}\r\n" +
                "}\r\n";

            string modded = vanilla.Replace("\r\n", "\n").Replace("models/axe.vmdl", "models/MODDED_axe.vmdl");

            var merged = ItemsGameMerger.Merge(vanilla, modded);

            Assert.That(merged.Text, Does.Not.Contain(Cr), "The merge carried the game's CR into the package.");
            Assert.That(merged.Text, Does.Contain("models/MODDED_axe.vmdl"), "The customisation was lost.");
        }
    }
}
