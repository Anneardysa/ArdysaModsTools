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
using ArdysaModsTools.Core.Helpers;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Helpers;

[TestFixture]
public class SafeTempPathSweepTests
{
    private string _root = null!;

    [SetUp]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "AmtSweepTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }

    private string MakeDir(params string[] parts)
    {
        string p = Path.Combine(_root, Path.Combine(parts));
        Directory.CreateDirectory(p);
        File.WriteAllText(Path.Combine(p, "decrypted_asset.vmdl_c"), "plaintext payload");
        return p;
    }

    [Test]
    public void Sweep_RemovesEveryPerRunWorkDirectory()
    {
        string[] stale =
        {
            MakeDir("ArdysaHero_abc123"),
            MakeDir("ArdysaMisc_def456"),
            MakeDir("ArdysaMods_789abc"),
            MakeDir("ArdysaMods_Installer_0f0f0f"),
            MakeDir("ArdysaModsPack_112233"),
            MakeDir("ArdysaMerge_445566"),
            MakeDir("ArdysaItemsGame_778899"),
            MakeDir("ArdysaSkinManifest_aabbcc"),
            MakeDir("amt_manual_split_ddeeff"),
        };

        int removed = SafeTempPathHelper.SweepStaleWorkDirs(_root);

        Assert.Multiple(() =>
        {
            foreach (var dir in stale)
                Assert.That(Directory.Exists(dir), Is.False, $"{Path.GetFileName(dir)} must be swept");
            Assert.That(removed, Is.EqualTo(stale.Length));
        });
    }

    [Test]
    public void Sweep_DropsDecryptedSetsButKeepsThePristineBaseCache()
    {
        string heroSets = MakeDir("ArdysaSelectHero", "HeroSets", "npc_dota_hero_axe_someset");
        string dec = MakeDir("ArdysaSelectHero", "dec");
        string cache = MakeDir("ArdysaSelectHero", "cache", "original", "vpk_extracted");
        string marker = Path.Combine(_root, "ArdysaSelectHero", "cache", "original", "vpk_extracted.ready2");
        File.WriteAllText(marker, "ready");

        SafeTempPathHelper.SweepStaleWorkDirs(_root);

        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(Path.Combine(_root, "ArdysaSelectHero", "HeroSets")), Is.False,
                "decrypted hero sets must not survive a restart");
            Assert.That(Directory.Exists(dec), Is.False, "decrypted zip staging must not survive a restart");

            Assert.That(Directory.Exists(cache), Is.True,
                "the pristine vanilla base extraction is kept on purpose — sweeping it costs a multi-GB re-extract");
            Assert.That(File.Exists(marker), Is.True, "the .ready2 marker must survive with its cache");
            Assert.That(heroSets, Is.Not.Null);
        });
    }

    [Test]
    public void Sweep_LeavesUnrelatedDirectoriesAlone()
    {
        string foreign = MakeDir("SomeOtherApp_cache");
        string lookalike = MakeDir("ArdysaModsToolsSetup");

        int removed = SafeTempPathHelper.SweepStaleWorkDirs(_root);

        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(foreign), Is.True, "another app's temp folder is not ours to delete");
            Assert.That(Directory.Exists(lookalike), Is.True, "a prefix look-alike must not be swept");
            Assert.That(removed, Is.Zero);
        });
    }

    [Test]
    public void Sweep_RemovesSuperhiddenDirectories()
    {
        string hidden = MakeDir("ArdysaHero_hidden01");
        SafeTempPathHelper.HideDirectory(hidden);
        File.SetAttributes(Path.Combine(hidden, "decrypted_asset.vmdl_c"),
            FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReadOnly);

        int removed = SafeTempPathHelper.SweepStaleWorkDirs(_root);

        Assert.That(Directory.Exists(hidden), Is.False, "superhidden and read-only leftovers must still be swept");
        Assert.That(removed, Is.EqualTo(1));
    }

    [Test]
    public void Sweep_OnMissingRoot_IsANoOp()
    {
        Assert.That(SafeTempPathHelper.SweepStaleWorkDirs(Path.Combine(_root, "does_not_exist")), Is.Zero);
        Assert.That(SafeTempPathHelper.SweepStaleWorkDirs(""), Is.Zero);
    }
}
