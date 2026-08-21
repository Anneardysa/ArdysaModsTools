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
using ArdysaModsTools.Core.Services.Security;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services;

[TestFixture]
public class FileSecurityGuardTests
{
    private string _testDir = null!;
    private string _testFile = null!;

    [SetUp]
    public void SetUp()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "FileSecurityGuardTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _testFile = Path.Combine(_testDir, "pak01_dir.vpk");
        File.WriteAllBytes(_testFile, new byte[] { 1, 2, 3, 4, 5 });
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (File.Exists(_testFile))
            {
                FileSecurityGuard.ReleaseDaclLock(_testFile);
                File.SetAttributes(_testFile, FileAttributes.Normal);
            }

            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch { }
    }

    [Test]
    public void ApplyRuntimeDaclLock_BlocksDirectReadAccess()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Ignore("NTFS DACL tests only run on Windows.");

        Assert.That(File.ReadAllBytes(_testFile), Is.EqualTo(new byte[] { 1, 2, 3, 4, 5 }));

        bool locked = FileSecurityGuard.ApplyRuntimeDaclLock(_testFile);
        Assert.That(locked, Is.True, "DACL Lock successfully applied");

        Assert.Throws<UnauthorizedAccessException>(() =>
        {
            File.ReadAllBytes(_testFile);
        });

        bool released = FileSecurityGuard.ReleaseDaclLock(_testFile);
        Assert.That(released, Is.True, "DACL Lock successfully released");

        Assert.That(File.ReadAllBytes(_testFile), Is.EqualTo(new byte[] { 1, 2, 3, 4, 5 }));
    }

    [Test]
    public void ExistingOpenHandle_CanStillReadAfterDaclLock()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Ignore("NTFS DACL tests only run on Windows.");

        using var dotaHandle = new FileStream(_testFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        FileSecurityGuard.ApplyRuntimeDaclLock(_testFile);

        Assert.Throws<UnauthorizedAccessException>(() =>
        {
            using var externalHandle = new FileStream(_testFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        });

        byte[] buffer = new byte[5];
        int read = dotaHandle.Read(buffer, 0, buffer.Length);
        Assert.That(read, Is.EqualTo(5));
        Assert.That(buffer, Is.EqualTo(new byte[] { 1, 2, 3, 4, 5 }));
    }
}
