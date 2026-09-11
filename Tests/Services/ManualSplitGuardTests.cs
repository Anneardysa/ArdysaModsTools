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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Interfaces;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Mods;
using ArdysaModsTools.Tests.Helpers;
using Moq;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services;

[TestFixture]
public class ManualSplitGuardTests
{
    private const string InstalledSentinel = "AMT_INSTALLED_PACK_SENTINEL";

    private string _root = null!;
    private string _targetPath = null!;
    private string _modsDir = null!;
    private string _mainVpk = null!;

    [SetUp]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "AmtManualSplit_" + Guid.NewGuid().ToString("N"));
        _targetPath = Path.Combine(_root, "dota");
        Directory.CreateDirectory(_targetPath);

        string gameInfoDir = Path.Combine(_targetPath, "game", "dota");
        Directory.CreateDirectory(gameInfoDir);
        File.WriteAllText(Path.Combine(gameInfoDir, "gameinfo_branchspecific.gi"),
            "\t\t\tGame\t\t\t\t_ArdysaMods\r\n\t\t\tGame\t\t\t\tmod\r\n\t\t\tGame\t\t\t\tdota\r\n");

        _modsDir = Path.Combine(_targetPath, "game", "_ArdysaMods");
        Directory.CreateDirectory(_modsDir);
        _mainVpk = Path.Combine(_modsDir, "pak01_dir.vpk");

        File.WriteAllBytes(_mainVpk, TestVpk.Build(
            TestVpk.ItemsGame($"\"items_game\"\n{{\n\t\"{InstalledSentinel}\" \"1\"\n}}\n"),
            TestVpk.Blob("models/heroes/axe", "axe", "vmdl_c", 64)));
    }

    private static bool PackageContains(string vpkPath, string needle) =>
        System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(vpkPath)).Contains(needle, StringComparison.Ordinal);

    [TearDown]
    public void TearDown()
    {
        try
        {
            string protectedVpk = ProtectedVpkStore.VpkPath(_targetPath);
            if (File.Exists(protectedVpk)) File.SetAttributes(protectedVpk, FileAttributes.Normal);
            string dir = ProtectedVpkStore.Dir(_targetPath);
            if (Directory.Exists(dir)) File.SetAttributes(dir, FileAttributes.Normal);
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
        catch {  }
    }

    private static void RequireRealTools()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var hl = new FileInfo(Path.Combine(baseDir, "HLExtract.exe"));
        var vpk = new FileInfo(Path.Combine(baseDir, "vpk.exe"));
        if (!hl.Exists || !vpk.Exists || hl.Length < 4096 || vpk.Length < 4096)
            Assert.Ignore("Real HLExtract.exe / vpk.exe not present in the test output.");
    }

    private static Mock<IVpkRecompiler> Recompiler(Func<string, byte[]> bytesFor, List<string> built)
    {
        var mock = new Mock<IVpkRecompiler>();
        mock.Setup(r => r.RecompileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>(),
                It.IsAny<IProgress<SpeedMetrics>?>()))
            .Returns<string, string, string, string, Action<string>, CancellationToken, IProgress<SpeedMetrics>?>(
                (tool, inputDir, build, temp, log, ct, sp) =>
                {
                    string key = Path.GetFileName(inputDir);
                    built.Add(key);
                    string outVpk = Path.Combine(temp, key + ".vpk");
                    File.WriteAllBytes(outVpk, bytesFor(key));
                    return Task.FromResult<string?>(outVpk);
                });
        return mock;
    }

    [Test]
    public async Task WhenTheProtectedDeployFails_TheInstalledPackageIsNeverSwapped()
    {
        RequireRealTools();

        Assert.That(PackageContains(_mainVpk, InstalledSentinel), Is.True, "precondition: item data is installed");
        var built = new List<string>();

        var service = new ModInstallerService(
            recompiler: Recompiler(key => key == "protected"
                ? new byte[] { 1, 2, 3, 4 }
                : TestVpk.Minimal(), built).Object);

        bool result = await service.SplitAndSecureInstalledPackageAsync(
            _targetPath, _mainVpk, null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(built, Does.Contain("protected"),
                "the split must actually have reached the protected build for this to be the case under test");
            Assert.That(PackageContains(_mainVpk, InstalledSentinel), Is.True,
                "the installed package must KEEP its item data — swapping in the stripped rebuild "
                + "without the protected half live leaves no item definitions anywhere");
            Assert.That(File.Exists(ProtectedVpkStore.VpkPath(_targetPath)), Is.False,
                "a package the validator rejected must not be left behind as the protected package");
            Assert.That(result, Is.True,
                "the install itself still succeeded — only the split was skipped");
        });
    }

    [Test]
    public async Task WhenEverythingBuilds_TheSplitCompletesAndBothPackagesLand()
    {
        RequireRealTools();

        var built = new List<string>();

        var service = new ModInstallerService(
            recompiler: Recompiler(_ => TestVpk.Minimal(), built).Object);

        bool result = await service.SplitAndSecureInstalledPackageAsync(
            _targetPath, _mainVpk, null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(built, Is.EquivalentTo(new[] { "root", "protected" }),
                "both halves must be built");
            Assert.That(File.Exists(ProtectedVpkStore.VpkPath(_targetPath)), Is.True,
                "the protected package must be live");
            Assert.That(PackageContains(_mainVpk, InstalledSentinel), Is.False,
                "the installed package must have been replaced by the stripped rebuild");
        });
    }

    [Test]
    public async Task WhenTheGameInfoDoesNotMountTheProtectedPath_TheInstalledPackageIsUntouched()
    {
        RequireRealTools();

        File.WriteAllText(Path.Combine(_targetPath, "game", "dota", "gameinfo_branchspecific.gi"),
            "\t\t\tGame\t\t\t\t_ArdysaMods\r\n\t\t\tGame\t\t\t\tdota\r\n");

        var built = new List<string>();
        var service = new ModInstallerService(
            recompiler: Recompiler(_ => TestVpk.Minimal(), built).Object);

        bool result = await service.SplitAndSecureInstalledPackageAsync(
            _targetPath, _mainVpk, null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(built, Is.Empty, "a legacy gameinfo must not trigger any rebuild");
            Assert.That(PackageContains(_mainVpk, InstalledSentinel), Is.True,
                "the installed package keeps its item data when the split cannot be mounted");
        });
    }
}
