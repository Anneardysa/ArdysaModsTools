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
using System.Diagnostics;
using System.IO;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Helpers;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services;

[TestFixture]
public class SkinTesterSandboxTests
{
    private string _tmp = null!;
    private string _real = null!;
    private string _sandbox = null!;

    [SetUp]
    public void SetUp()
    {
        _tmp = Path.Combine(Path.GetTempPath(), "amt_sandbox_" + Guid.NewGuid().ToString("N"));
        _real = Path.Combine(_tmp, "dota 2 beta");
        Write(_real, "game/dota/pak01_dir.vpk", "dir");
        Write(_real, "game/dota/pak01_000.vpk", "chunk0");
        Write(_real, "game/dota/gameinfo_branchspecific.gi", "gi");
        Write(_real, "game/dota/gameinfo.gi", "gameinfo");
        Write(_real, "game/dota/steam.inf", "PatchVersion=1");
        Write(_real, "game/bin/win64/dota.signatures", "DIGEST:abc");
        _sandbox = SkinTesterSandbox.Root(_real);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_tmp, true); } catch { }
    }

    private static string Write(string root, string rel, string text)
    {
        string p = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllText(p, text);
        return p;
    }

    private static string In(string root, string rel) => Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));

    [Test]
    public void Root_IsASiblingOfTheDotaFolder()
    {
        Assert.That(SkinTesterSandbox.Root(_real), Is.EqualTo(Path.Combine(_tmp, SkinTesterSandbox.FolderName)));
        Assert.That(SkinTesterSandbox.Root(_real + Path.DirectorySeparatorChar), Is.EqualTo(_sandbox));
    }

    [Test]
    public void Prepare_LinksVanillaPackages_TheyShareTheRealData()
    {
        Assert.That(SkinTesterSandbox.Prepare(_real, _sandbox), Is.Null);

        Assert.That(File.ReadAllText(In(_sandbox, "game/dota/pak01_dir.vpk")), Is.EqualTo("dir"));
        File.AppendAllText(In(_real, "game/dota/pak01_000.vpk"), "+patched");
        Assert.That(File.ReadAllText(In(_sandbox, "game/dota/pak01_000.vpk")), Is.EqualTo("chunk0+patched"),
            "a hardlink, not a copy - no 30 GB duplicate");
    }

    [Test]
    public void Prepare_CopiesWhatGenerationRewrites_TheRealFilesNeverChange()
    {
        Assert.That(SkinTesterSandbox.Prepare(_real, _sandbox), Is.Null);

        foreach (string rel in new[] { "game/dota/gameinfo_branchspecific.gi", "game/bin/win64/dota.signatures",
                                       "game/dota/gameinfo.gi", "game/dota/steam.inf" })
        {
            string before = File.ReadAllText(In(_real, rel));
            Assert.That(File.ReadAllText(In(_sandbox, rel)), Is.EqualTo(before), rel);
            File.WriteAllText(In(_sandbox, rel), "rewritten by generation");
            Assert.That(File.ReadAllText(In(_real, rel)), Is.EqualTo(before), rel);
        }
    }

    [Test]
    public void Prepare_ALinkLeftWhereACopyBelongs_IsReplacedNotWrittenThrough()
    {
        string gi = In(_sandbox, "game/dota/gameinfo_branchspecific.gi");
        Directory.CreateDirectory(Path.GetDirectoryName(gi)!);
        using (var mk = Process.Start(new ProcessStartInfo("cmd", $"/c mklink /H \"{gi}\" \"{In(_real, "game/dota/gameinfo_branchspecific.gi")}\"")
               { CreateNoWindow = true, UseShellExecute = false }))
            mk!.WaitForExit();
        if (!File.Exists(gi)) Assert.Ignore("cannot create a hardlink here");

        Assert.That(SkinTesterSandbox.Prepare(_real, _sandbox), Is.Null);
        File.WriteAllText(gi, "rewritten by generation");

        Assert.That(File.ReadAllText(In(_real, "game/dota/gameinfo_branchspecific.gi")), Is.EqualTo("gi"));
    }

    [Test]
    public void Prepare_OverlappingFolders_Refused_NothingCreated()
    {
        Assert.That(SkinTesterSandbox.Prepare(_real, _real), Is.Not.Null);
        Assert.That(SkinTesterSandbox.Prepare(_real, Path.Combine(_real, "sandbox")), Is.Not.Null, "sandbox inside Dota");
        Assert.That(SkinTesterSandbox.Prepare(_real, _tmp), Is.Not.Null, "Dota inside the sandbox");
        Assert.That(Directory.Exists(Path.Combine(_real, "sandbox")), Is.False);
        Assert.That(Directory.Exists(In(_tmp, "game/_ArdysaMods")), Is.False);
    }

    [TestCase("game")]
    [TestCase("_ArdysaMods")]
    [TestCase("my_game")]
    public void Prepare_DotaLikeFolderName_Refused(string leaf)
    {
        string sandbox = Path.Combine(_tmp, "x", leaf);
        Assert.That(SkinTesterSandbox.Prepare(_real, sandbox), Does.Contain("named like a Dota folder"));
        Assert.That(Directory.Exists(sandbox), Is.False);
    }

    [Test]
    public void Prepare_OtherDrive_Refused_NeverCopied()
    {
        string? error = SkinTesterSandbox.Prepare(_real, _sandbox, (_, _) => 17);

        Assert.That(error, Does.Contain("same drive"));
        Assert.That(File.Exists(In(_sandbox, "game/dota/pak01_dir.vpk")), Is.False);
    }

    [Test]
    public void Prepare_LinkFailure_Refused_WithTheWin32Code()
    {
        Assert.That(SkinTesterSandbox.Prepare(_real, _sandbox, (_, _) => 5), Does.Contain("Win32 error 5"));
    }

    [TestCase("game/dota/pak01_dir.vpk")]
    [TestCase("game/bin/win64/dota.signatures")]
    [TestCase("game/dota/gameinfo_branchspecific.gi")]
    public void Prepare_RequiredInstallFileMissing_Refused(string rel)
    {
        File.Delete(In(_real, rel));
        Assert.That(SkinTesterSandbox.Prepare(_real, _sandbox), Is.Not.Null);
    }

    [Test]
    public void Prepare_OptionalFilesMissing_StillReady()
    {
        File.Delete(In(_real, "game/dota/steam.inf"));
        File.Delete(In(_real, "game/dota/gameinfo.gi"));
        Assert.That(SkinTesterSandbox.Prepare(_real, _sandbox), Is.Null);
    }

    [Test]
    public void Prepare_GenerationKeepsTheSandboxAsItsTarget()
    {
        Assert.That(SkinTesterSandbox.Prepare(_real, _sandbox), Is.Null);
        Assert.That(PathUtility.NormalizeTargetPath(_sandbox), Is.EqualTo(_sandbox).IgnoreCase,
            "a re-rooted target would be a build into somewhere else");
        Assert.That(Directory.Exists(In(_sandbox, "game/_ArdysaMods")), Is.True);
    }

    [Test]
    public void Prepare_Again_KeepsGoodLinks_RefreshesStaleOnes_DropsGoneOnes()
    {
        var linked = new List<string>();
        int Fake(string dst, string src)
        {
            linked.Add(Path.GetFileName(dst));
            File.Copy(src, dst);
            return 0;
        }

        Assert.That(SkinTesterSandbox.Prepare(_real, _sandbox, Fake), Is.Null);
        Assert.That(linked, Is.EquivalentTo(new[] { "pak01_dir.vpk", "pak01_000.vpk" }));

        linked.Clear();
        Assert.That(SkinTesterSandbox.Prepare(_real, _sandbox, Fake), Is.Null);
        Assert.That(linked, Is.Empty, "unchanged packages keep their link");

        File.Delete(In(_real, "game/dota/pak01_000.vpk"));
        Write(_real, "game/dota/pak01_000.vpk", "chunk0 v2");
        Write(_real, "game/dota/pak01_001.vpk", "chunk1");
        Write(_sandbox, "game/dota/pak01_009.vpk", "stale");
        Assert.That(SkinTesterSandbox.Prepare(_real, _sandbox, Fake), Is.Null);

        Assert.That(linked, Is.EquivalentTo(new[] { "pak01_000.vpk", "pak01_001.vpk" }));
        Assert.That(File.ReadAllText(In(_sandbox, "game/dota/pak01_000.vpk")), Is.EqualTo("chunk0 v2"));
        Assert.That(File.Exists(In(_sandbox, "game/dota/pak01_009.vpk")), Is.False);
        Assert.That(File.ReadAllText(In(_real, "game/dota/pak01_dir.vpk")), Is.EqualTo("dir"));
    }

    [Test]
    public void VerifyUntouched_ReportsEveryChangedOrMissingFile()
    {
        Write(_real, "game/_ArdysaMods/pak01_dir.vpk", "installed mods");
        var before = SkinTesterSandbox.Snapshot(_real);
        Assert.That(before.Keys, Has.Some.EndsWith("pak01_000.vpk"));
        Assert.That(before.Keys, Has.Some.EndsWith(Path.Combine("_ArdysaMods", "pak01_dir.vpk")));
        Assert.That(SkinTesterSandbox.VerifyUntouched(before), Is.Null);

        File.AppendAllText(In(_real, "game/dota/gameinfo_branchspecific.gi"), " patched");
        File.Delete(In(_real, "game/_ArdysaMods/pak01_dir.vpk"));

        Assert.That(SkinTesterSandbox.VerifyUntouched(before),
            Does.Contain("gameinfo_branchspecific.gi").And.Contain("pak01_dir.vpk"));
    }
}
