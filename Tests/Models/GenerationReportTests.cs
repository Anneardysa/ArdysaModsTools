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
using ArdysaModsTools.Core.Models;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Models
{
    [TestFixture]
    public class GenerationReportTests
    {
        [Test]
        public void Log_ForwardsToCallback_AddsNoWarning()
        {
            var sink = new List<string>();
            var report = new GenerationReport(sink.Add);

            report.Log("step");

            Assert.That(report.Warnings, Is.Empty);
            Assert.That(sink, Does.Contain("step"));
        }

        [Test]
        public void Skip_RecordsOneWarning_AndForwards()
        {
            var sink = new List<string>();
            var report = new GenerationReport(sink.Add);

            report.Skip("Anti-Mage", "set 'X' not found");

            Assert.That(report.Warnings, Has.Count.EqualTo(1));
            Assert.That(report.Warnings[0], Is.EqualTo("Anti-Mage: set 'X' not found"));
            Assert.That(sink, Has.Some.Contains("Anti-Mage"));
        }

        [Test]
        public void Warn_RecordsOneWarning()
        {
            var report = new GenerationReport(_ => { });

            report.Warn("localization failed");

            Assert.That(report.Warnings, Has.Count.EqualTo(1));
            Assert.That(report.Warnings[0], Is.EqualTo("localization failed"));
        }

        [Test]
        public void Debug_BuffersLine_ButDoesNotForwardToLiveLog()
        {
            var sink = new List<string>();
            var report = new GenerationReport(sink.Add);

            report.Debug("priority resolved");

            Assert.That(sink, Is.Empty, "debug detail must not reach the live status line");
            Assert.That(report.Lines, Has.Some.Contains("DEBUG priority resolved"));
            Assert.That(report.Warnings, Is.Empty);
        }

        [Test]
        public void Log_MasksLocalWindowsPaths_InBufferAndLiveLog()
        {
            var sink = new List<string>();
            var report = new GenerationReport(sink.Add);

            report.Log(@"Using cached set: C:\Users\SomeUser\AppData\Local\Temp\cache\set.zip");

            Assert.That(sink[0], Does.Not.Contain("SomeUser"));
            Assert.That(sink[0], Does.Contain(@"…\set.zip"));
            Assert.That(report.Lines[0], Does.Not.Contain("SomeUser"));
        }

        [Test]
        public void Log_MasksUrls_KeepingOnlyFileName()
        {
            var sink = new List<string>();
            var report = new GenerationReport(sink.Add);

            report.Log("Downloading https://cdn.example.com/private-bucket/hero/set.zip now");

            Assert.That(sink[0], Does.Not.Contain("cdn.example.com"));
            Assert.That(sink[0], Does.Not.Contain("private-bucket"));
            Assert.That(sink[0], Does.Contain("…/set.zip"));
        }

        [Test]
        public void Warn_SanitizesMessage_BeforeRecordingWarning()
        {
            var report = new GenerationReport(_ => { });

            report.Warn(@"failed reading D:\Games\Steam\file.txt");

            Assert.That(report.Warnings[0], Does.Not.Contain(@"D:\Games"));
            Assert.That(report.Warnings[0], Does.Contain(@"…\file.txt"));
        }

        [Test]
        public void Save_NeverThrows_OnInvalidTarget()
        {
            var report = new GenerationReport(_ => { });
            report.Log("trace");

            Assert.DoesNotThrow(() => report.Save(""));
        }

        [Test]
        public void Save_PrunesToTenMostRecentReports()
        {
            var target = Path.Combine(Path.GetTempPath(), "ArdysaReportTest_" + System.Guid.NewGuid().ToString("N"));
            var dir = Path.Combine(target, "game", "_ArdysaMods", "_temp");
            Directory.CreateDirectory(dir);
            try
            {
                for (int i = 0; i < 12; i++)
                    File.WriteAllText(Path.Combine(dir, $"generation_report_20200101_0000{i:D2}.txt"), "old");

                new GenerationReport(_ => { }).Save(target);

                var remaining = Directory.GetFiles(dir, "generation_report_*.txt");
                Assert.That(remaining, Has.Length.EqualTo(10), "should keep only the 10 newest reports");
                Assert.That(remaining.Any(f => !Path.GetFileName(f).Contains("20200101")), Is.True);
            }
            finally
            {
                try { Directory.Delete(target, true); } catch { }
            }
        }
    }
}
