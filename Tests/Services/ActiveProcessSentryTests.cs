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
using System.Diagnostics;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Services.Security;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services;

[TestFixture]
public class ActiveProcessSentryTests
{
    [Test]
    public void BlacklistedProcesses_ContainsMajorDecompilerTools()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ActiveProcessSentry.BlacklistedProcesses.Contains("vrf"), Is.True);
            Assert.That(ActiveProcessSentry.BlacklistedProcesses.Contains("source2viewer"), Is.True);
            Assert.That(ActiveProcessSentry.BlacklistedProcesses.Contains("gcfscape"), Is.True);
            Assert.That(ActiveProcessSentry.BlacklistedProcesses.Contains("hlextract"), Is.True);
            Assert.That(ActiveProcessSentry.BlacklistedProcesses.Contains("vpkedit"), Is.True);
            Assert.That(ActiveProcessSentry.BlacklistedProcesses.Contains("python"), Is.True);
            Assert.That(ActiveProcessSentry.BlacklistedProcesses.Contains("py"), Is.True);
            Assert.That(ActiveProcessSentry.BlacklistedProcesses.Contains("quickbms"), Is.True);
            Assert.That(ActiveProcessSentry.BlacklistedProcesses.Contains("source2gen"), Is.True);
            Assert.That(ActiveProcessSentry.BlacklistedProcesses.Contains("ghidra"), Is.True);
            Assert.That(ActiveProcessSentry.BlacklistedProcesses.Contains("x64dbg"), Is.True);
            Assert.That(ActiveProcessSentry.BlacklistedProcesses.Contains("ninjaripper"), Is.True);
            Assert.That(ActiveProcessSentry.BlacklistedProcesses.Contains("renderdoc"), Is.True);
        });
    }

    [Test]
    public void DetectRunningThreat_WithMockProcess_ReturnsMatchingName()
    {
        string currentProcess = Process.GetCurrentProcess().ProcessName;
        var customList = new[] { "nonexistent_fake_tool_xyz", currentProcess };

        string? detected = ActiveProcessSentry.DetectRunningThreat(customList);

        Assert.That(detected, Is.EqualTo(currentProcess));
    }

    [Test]
    public void DetectRunningThreat_WithNoMatches_ReturnsNull()
    {
        var customList = new[] { "completely_fictional_unregistered_app_99999" };
        string? detected = ActiveProcessSentry.DetectRunningThreat(customList);

        Assert.That(detected, Is.Null);
    }

    [Test]
    public void Sentry_Dispose_StopsCleanlyWithoutErrors()
    {
        using var sentry = new ActiveProcessSentry();
        sentry.Start(pollIntervalMs: 50);
        Assert.DoesNotThrow(() => sentry.Stop());
    }
}
