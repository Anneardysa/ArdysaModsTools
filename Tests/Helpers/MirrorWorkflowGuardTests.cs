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
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Helpers
{
    [TestFixture]
    public class MirrorWorkflowGuardTests
    {
        private static string WorkflowPath()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "build.txt")))
                dir = dir.Parent;
            Assert.That(dir, Is.Not.Null, "could not locate the repo root (no build.txt in any parent)");
            var path = Path.Combine(dir!.FullName, ".github", "workflows", "mirror-r2-to-b2.yml");
            Assert.That(File.Exists(path), Is.True, $"expected workflow at {path}");
            return path;
        }

        [Test]
        public void MirrorWorkflow_SyncsTheFullBucketExactlyOnce()
        {
            var text = File.ReadAllText(WorkflowPath());
            var fullBucketSyncCount = Regex.Matches(text, @"rclone sync ""r2:\$\{R2_BUCKET\}"" ""b2:\$\{B2_BUCKET\}""").Count;

            Assert.That(fullBucketSyncCount, Is.EqualTo(1),
                "expected exactly one full-bucket `rclone sync` — a duplicate (e.g. a pre-flight " +
                "dry-run pass before the real sync) doubles the list+compare cost against R2/B2 for " +
                "every run");
        }

        [Test]
        public void MirrorWorkflow_FullBucketSyncUsesSizeOnly()
        {
            var text = File.ReadAllText(WorkflowPath());
            var syncBlockStart = text.IndexOf(@"rclone sync ""r2:${R2_BUCKET}"" ""b2:${B2_BUCKET}""", StringComparison.Ordinal);
            Assert.That(syncBlockStart, Is.GreaterThanOrEqualTo(0), "full-bucket rclone sync not found");

            var syncBlockEnd = Math.Min(text.Length, syncBlockStart + 1200);
            var syncBlock = text.Substring(syncBlockStart, syncBlockEnd - syncBlockStart);

            Assert.That(syncBlock, Does.Contain("--size-only"),
                "the full-bucket sync must compare by size only — R2 and B2 don't share a modtime " +
                "clock, so the default size+modtime compare degrades to a per-object round-trip");
        }

        [Test]
        public void MirrorWorkflow_HasNoDuplicatePreflightPass()
        {
            var text = File.ReadAllText(WorkflowPath());
            Assert.That(text, Does.Not.Contain("Pre-flight Safety Check"),
                "the duplicate pre-flight dry-run pass was removed — --max-delete on the real sync " +
                "is the safety net now, same as it always effectively was");
        }

        [Test]
        public void MirrorWorkflow_ScheduleFiresAtMostOncePerDay()
        {
            var text = File.ReadAllText(WorkflowPath());
            var match = Regex.Match(text, @"cron:\s*""([^""]+)""");
            Assert.That(match.Success, Is.True, "no cron schedule found in mirror-r2-to-b2.yml");

            var cron = match.Groups[1].Value;
            var fields = cron.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Assert.That(fields, Has.Length.EqualTo(5), $"unexpected cron field count: '{cron}'");

            Assert.That(fields[1], Does.Not.Contain("/"),
                $"cron '{cron}' fires more than once a day — each run is a full-bucket list+compare " +
                "against a bucket that only changes a few times a week");
        }
    }
}
