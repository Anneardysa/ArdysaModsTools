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
using System.Threading.Tasks;
using ArdysaModsTools.Core.Services;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    [NonParallelizable]
    public class InstallReportTests
    {
        [Test]
        public void Begin_ClearsPreviousRun_AndLinesKeepOrderAndCategory()
        {
            InstallReport.Begin();
            InstallReport.Fail("old failure");

            InstallReport.Begin();
            InstallReport.Step("Installation started.");
            InstallReport.Ok("Download verified.");
            InstallReport.Warn("Some localization files failed.");
            InstallReport.Fail("Download failed.");

            var lines = InstallReport.Snapshot();

            Assert.That(lines.Count, Is.EqualTo(4));
            Assert.That(lines.Any(l => l.Text == "old failure"), Is.False);
            Assert.That(lines[0], Is.EqualTo(("Installation started.", InstallReport.Default)));
            Assert.That(lines[1], Is.EqualTo(("Download verified.", InstallReport.Success)));
            Assert.That(lines[2], Is.EqualTo(("Some localization files failed.", InstallReport.Warning)));
            Assert.That(lines[3], Is.EqualTo(("Download failed.", InstallReport.Error)));
        }

        [Test]
        public void Add_IgnoresBlankLines_AndSnapshotIsACopy()
        {
            InstallReport.Begin();
            InstallReport.Step("");
            InstallReport.Step("   ");
            InstallReport.Ok("kept");

            var snapshot = InstallReport.Snapshot();
            InstallReport.Ok("added after snapshot");

            Assert.That(snapshot.Count, Is.EqualTo(1));
            Assert.That(snapshot[0].Text, Is.EqualTo("kept"));
            Assert.That(InstallReport.Snapshot().Count, Is.EqualTo(2));
        }

        [Test]
        public async Task ConcurrentWrites_DoNotThrowOrLoseLines()
        {
            InstallReport.Begin();

            await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Task.Run(() =>
            {
                for (int n = 0; n < 100; n++)
                    InstallReport.Step($"line {i}-{n}");
            })));

            Assert.That(InstallReport.Snapshot().Count, Is.EqualTo(800));
        }
    }
}
