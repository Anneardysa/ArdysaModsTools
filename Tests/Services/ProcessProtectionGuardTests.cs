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
using System.Runtime.InteropServices;
using ArdysaModsTools.Core.Services.Security;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services;

[TestFixture]
public class ProcessProtectionGuardTests
{
    [Test]
    public void ProtectAndUnprotect_OnWindows_TogglesProtectionStateCleanly()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Pass("Skipped on non-Windows OS.");
            return;
        }

        try
        {
            bool protectedOk = ProcessProtectionGuard.ProtectCurrentProcess();
            Assert.That(protectedOk, Is.True);
            Assert.That(ProcessProtectionGuard.IsProtected, Is.True);

            bool secondProtect = ProcessProtectionGuard.ProtectCurrentProcess();
            Assert.That(secondProtect, Is.True);
            Assert.That(ProcessProtectionGuard.IsProtected, Is.True);
        }
        finally
        {
            bool unprotectOk = ProcessProtectionGuard.UnprotectCurrentProcess();
            Assert.That(unprotectOk, Is.True);
            Assert.That(ProcessProtectionGuard.IsProtected, Is.False);
        }
    }

    [Test]
    public void Unprotect_WhenNotProtected_ReturnsTrueWithoutErrors()
    {
        bool result = ProcessProtectionGuard.UnprotectCurrentProcess();
        Assert.That(result, Is.True);
        Assert.That(ProcessProtectionGuard.IsProtected, Is.False);
    }
}
