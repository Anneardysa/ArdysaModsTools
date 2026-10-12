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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Services;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class ItemDataLineEndingRepairTests
    {
        private string _file = null!;

        [SetUp]
        public void SetUp() => _file = Path.Combine(Path.GetTempPath(), $"items_game_{Guid.NewGuid():N}.txt");

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_file)) File.Delete(_file);
        }

        [Test]
        public async Task Crlf_IsRewrittenToLf_KeepingEveryLine()
        {
            File.WriteAllText(_file, "\"items_game\"\r\n{\r\n\t\"items\"\r\n\t{\r\n\t}\r\n}\r\n");

            bool repaired = await GameItemsGameExtractorService.RepairLineEndingsInPlaceAsync(_file, CancellationToken.None);

            Assert.That(repaired, Is.True);
            Assert.That(File.ReadAllText(_file), Is.EqualTo("\"items_game\"\n{\n\t\"items\"\n\t{\n\t}\n}\n"));
            Assert.That(File.Exists(_file + ".lf"), Is.False, "no working copy is left behind");
        }

        [Test]
        public async Task LfFile_IsLeftUntouched()
        {
            File.WriteAllText(_file, "\"items_game\"\n{\n}\n");
            var written = File.GetLastWriteTimeUtc(_file);

            bool repaired = await GameItemsGameExtractorService.RepairLineEndingsInPlaceAsync(_file, CancellationToken.None);

            Assert.That(repaired, Is.False);
            Assert.That(File.GetLastWriteTimeUtc(_file), Is.EqualTo(written));
        }

        [Test]
        public async Task MissingFile_IsNothingToRepair()
        {
            Assert.That(await GameItemsGameExtractorService.RepairLineEndingsInPlaceAsync(_file, CancellationToken.None), Is.False);
        }

        [Test]
        public async Task CrlfAcrossTheReadBuffer_BecomesOneLineBreak()
        {
            var text = new StringBuilder(new string('a', (1 << 20) - 1)).Append("\r\n").Append("b\r\n").ToString();
            File.WriteAllText(_file, text);

            await GameItemsGameExtractorService.RepairLineEndingsInPlaceAsync(_file, CancellationToken.None);

            string result = File.ReadAllText(_file);
            Assert.That(result, Does.Not.Contain("\r"));
            Assert.That(result, Does.EndWith("a\nb\n"));
            Assert.That(result.Length, Is.EqualTo((1 << 20) - 1 + 3));
        }
    }
}
