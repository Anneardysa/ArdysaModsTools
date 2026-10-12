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
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Security;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services;

[TestFixture]
public class SkinTesterTests
{
    private const string Jugg = "npc_dota_hero_juggernaut";
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "amt_skintester_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "Juggernaut"));
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_root, true); } catch { }
    }

    private static string Index(string hero) =>
        "\"9059\"\n{\n\t\"used_by_heroes\"\n\t{\n\t\t\"" + hero + "\"\t\t\"1\"\n\t}\n}\n";

    private string Zip(string folder, string stem, string? index, params (string Name, string Body)[] extra)
    {
        string dir = Path.Combine(_root, folder);
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, stem + ".zip");
        using var z = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, body) in (index == null ? extra : extra.Prepend(("index.txt", index))))
        {
            using var w = new StreamWriter(z.CreateEntry(name).Open(), new UTF8Encoding(false));
            w.Write(body);
        }
        return path;
    }

    private static List<HeroModel> Heroes() => new()
    {
        new HeroModel
        {
            HeroId = Jugg, Name = Jugg, ItemIds = new List<int> { 6, 7, 8 },
            Sets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Set 10"] = new() { $"{CdnConfig.R2BaseUrl}/Assets/models/Juggernaut/base_arcana_1.zip" },
            },
        },
        new HeroModel { HeroId = "npc_dota_hero_zuus", Name = "npc_dota_hero_zuus", ItemIds = new List<int> { 1 } },
    };

    #region SkinTesterPlan

    [Test]
    public void Plan_ArcanaAndWeapon_OneHeroTwoLayers_OnCdnShapedUrls()
    {
        string arc = Zip("Juggernaut", "base_arcana_2", Index(Jugg));
        string weapon = Zip("Juggernaut", "item_weapon_exile_odachi", Index(Jugg));

        var plan = SkinTesterPlan.Build(new[] { arc, weapon }, Heroes());

        Assert.That(plan.Errors, Is.Empty);
        Assert.That(plan.Selections.Select(s => s.Hero.Id).Distinct(), Is.EqualTo(new[] { Jugg }));
        var hero = plan.Selections[0].Hero;
        Assert.That(hero.ItemIds, Is.EqualTo(new[] { 6, 7, 8 }), "ItemIds come from heroes.json");
        Assert.That(hero.Sets.Keys, Is.EquivalentTo(plan.Selections.Select(s => s.SetName)),
            "only the zips under test - the published sets are not layered in");
        var urls = hero.Sets.Values.SelectMany(u => u).ToList();
        Assert.That(urls.Select(CdnConfig.ExtractAssetPath), Is.EquivalentTo(new[]
        {
            "Assets/models/Juggernaut/base_arcana_2.zip",
            "Assets/models/Juggernaut/item_weapon_exile_odachi.zip",
        }), "the asset path an encrypted container's key is derived from");
        Assert.That(plan.LocalZips[urls[0]], Is.EqualTo(arc).Or.EqualTo(weapon));
        Assert.That(plan.LocalZips.Values, Is.EquivalentTo(new[] { arc, weapon }));
    }

    [Test]
    public void Plan_ZipNamingNoHero_IsAnError_NotACrash()
    {
        string noIndex = Zip("Juggernaut", "item_misc", null, ("models/x.vmdl_c", "x"));
        string noHero = Zip("Somewhere", "item_misc2", "\"1\"\n{\n}\n");

        var plan = SkinTesterPlan.Build(new[] { noIndex, noHero }, Heroes());

        Assert.That(plan.Selections.Select(s => s.Hero.Id), Is.EqualTo(new[] { Jugg }));
        Assert.That(plan.Errors, Has.Count.EqualTo(1));
        Assert.That(plan.Errors[0], Does.Contain("item_misc2.zip"));
    }

    [Test]
    public void Plan_ArcanaMinimapIconRows_AreNotOwners()
    {
        string index = "\"9059\"\n{\n\t\"used_by_heroes\"\n\t{\n\t\t\"" + Jugg + "\"\t\t\"1\"\n\t}\n" +
            "\t\"visuals\"\n\t{\n\t\t\"asset_modifier1\"\n\t\t{\n" +
            "\t\t\t\"type\"\t\t\"icon_replacement_hero_minimap\"\n" +
            "\t\t\t\"asset\"\t\t\"" + Jugg + "\"\n" +
            "\t\t\t\"modifier\"\t\t\"npc_dota_hero_juggernaut_alt1\"\n\t\t}\n\t}\n}\n";
        string arc = Zip("Juggernaut", "base_arcana_2", index);

        var plan = SkinTesterPlan.Build(new[] { arc }, Heroes());

        Assert.That(plan.Errors, Is.Empty);
        Assert.That(plan.Selections.Single().Hero.Id, Is.EqualTo(Jugg));
    }

    private static string Block(string id, params string[] heroes) =>
        $"\"{id}\"\n{{\n\t\"used_by_heroes\"\n\t{{\n" +
        string.Concat(heroes.Select(h => $"\t\t\"{h}\"\t\t\"1\"\n")) + "\t}\n}\n";

    [Test]
    public void Plan_SharedItemBlock_DoesNotOutvoteTheSetsOwnHero()
    {
        string shared = Block("12345", "npc_dota_hero_axe", "npc_dota_hero_sven", Jugg, "npc_dota_hero_zuus");
        string zip = Zip("Juggernaut", "exile", Block("6", Jugg) + Block("7", Jugg) + shared);

        var plan = SkinTesterPlan.Build(new[] { zip }, Heroes());

        Assert.That(plan.Errors, Is.Empty);
        Assert.That(plan.Selections.Single().Hero.Id, Is.EqualTo(Jugg));
    }

    [Test]
    public void Plan_TiedHeroes_SettledByTheModsPackFolder_OrRefused()
    {
        string tie = Block("7000", Jugg, "npc_dota_hero_zuus");
        string inFolder = Zip("Juggernaut", "item_weapon_awaleb", tie);
        string loose = Zip("Downloads", "item_weapon_awaleb2", tie);

        var plan = SkinTesterPlan.Build(new[] { inFolder, loose }, Heroes());

        Assert.That(plan.Selections.Single().Hero.Id, Is.EqualTo(Jugg), "Juggernaut publishes from that folder");
        Assert.That(plan.Errors.Single(), Does.Contain("item_weapon_awaleb2.zip").And.Contain("Downloads"));
    }

    [Test]
    public void Plan_HeroMissingFromHeroesJson_IsAnError()
    {
        string zip = Zip("Axe", "base_arcana", Index("npc_dota_hero_axe"));
        var plan = SkinTesterPlan.Build(new[] { zip }, Heroes());
        Assert.That(plan.Selections, Is.Empty);
        Assert.That(plan.Errors.Single(), Does.Contain("npc_dota_hero_axe").And.Contain("heroes.json"));
    }

    [Test]
    public void Plan_SameStemTwiceForOneHero_IsAnError()
    {
        string a = Zip("Juggernaut", "base_arcana_2", Index(Jugg));
        string b = Zip("Juggernaut_copy", "base_arcana_2", Index(Jugg));
        var plan = SkinTesterPlan.Build(new[] { a, b }, Heroes());
        Assert.That(plan.Selections, Has.Count.EqualTo(1));
        Assert.That(plan.Errors.Single(), Does.Contain("base_arcana_2"));
    }

    [Test]
    public void Plan_EncryptedZip_ResolvesThroughItsFolder()
    {
        string plain = Zip("Juggernaut", "base_arcana_2", Index(Jugg));
        string enc = Path.Combine(_root, "Juggernaut", "base_arcana_2_enc.zip");
        File.WriteAllBytes(enc, AssetCipher.Encrypt(File.ReadAllBytes(plain), "Assets/models/Juggernaut/base_arcana_2_enc.zip"));
        File.Delete(plain);

        var plan = SkinTesterPlan.Build(new[] { enc }, Heroes());
        Assert.That(plan.Errors, Is.Empty);
        Assert.That(plan.Selections.Single().Hero.Id, Is.EqualTo(Jugg));
    }

    #endregion

    #region Guards - a plan never throws; every problem lands on its own zip

    [Test]
    public void Plan_ZipGoneFromDisk_IsAnErrorOnThatPath()
    {
        string gone = Path.Combine(_root, "Juggernaut", "base_gone.zip");

        var plan = SkinTesterPlan.Build(new[] { gone }, Heroes());

        Assert.That(plan.Selections, Is.Empty);
        Assert.That(plan.ErrorsByPath[gone], Does.Contain("not found"));
        Assert.That(plan.Errors.Single(), Does.Contain("base_gone.zip"));
    }

    [Test]
    public void Plan_NotAZip_IsAnErrorOnThatPath()
    {
        string junk = Path.Combine(_root, "Juggernaut", "item_junk.zip");
        File.WriteAllText(junk, "this is not a zip");

        var plan = SkinTesterPlan.Build(new[] { junk }, Heroes());

        Assert.That(plan.Selections, Is.Empty);
        Assert.That(plan.ErrorsByPath[junk], Does.Contain("not a readable zip"));
    }

    [Test]
    public void Plan_LockedZip_IsAnErrorOnThatPath()
    {
        string zip = Zip("Juggernaut", "item_locked", Index(Jugg));
        using var lockIt = new FileStream(zip, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var plan = SkinTesterPlan.Build(new[] { zip }, Heroes());

        Assert.That(plan.Selections, Is.Empty);
        Assert.That(plan.ErrorsByPath[zip], Does.Contain("cannot read"));
    }

    [Test]
    public async Task FromFiles_BrokenHeroesJson_IsAnError_NotAThrow()
    {
        string json = Path.Combine(_root, "heroes.json");
        File.WriteAllText(json, "[{ \"id\": [6, ");
        string zip = Zip("Juggernaut", "base_arcana_2", Index(Jugg));

        var plan = await SkinTesterPlan.FromFilesAsync(new[] { zip }, json);

        Assert.That(plan.Selections, Is.Empty);
        Assert.That(plan.Errors.Single(), Does.Contain("heroes.json"));
    }

    [Test]
    public void Plan_EncryptedZip_OwnerReadFromItsDecryptedIndex()
    {
        string plain = Zip("Elsewhere", "item_new_weapon", Index(Jugg));
        File.WriteAllBytes(plain, AssetCipher.Encrypt(File.ReadAllBytes(plain), "Assets/models/Elsewhere/item_new_weapon.zip"));

        var plan = SkinTesterPlan.Build(new[] { plain }, Heroes());

        Assert.That(plan.Errors, Is.Empty);
        Assert.That(plan.Selections.Single().Hero.Id, Is.EqualTo(Jugg));
    }

    [Test]
    public void Plan_EncryptedZipRenamed_FailsAtPlan_NotMidBuild()
    {
        string plain = Zip("Juggernaut", "base_arcana_3", Index(Jugg));
        File.WriteAllBytes(plain, AssetCipher.Encrypt(File.ReadAllBytes(plain), "Assets/models/Juggernaut/base_arcana_2.zip"));

        var plan = SkinTesterPlan.Build(new[] { plain }, Heroes());

        Assert.That(plan.Selections, Is.Empty);
        Assert.That(plan.ErrorsByPath[plain], Does.Contain("Assets/models/Juggernaut/base_arcana_3.zip"));
    }

    #endregion

    #region Priority preview - the order HeroGenerationService applies the layers in

    private static Dictionary<string, SkinTesterRow> ByStem(SkinTesterPlan plan) =>
        plan.Rows.ToDictionary(r => Path.GetFileNameWithoutExtension(r.File));

    [TestCase(1, new[] { "base_arcana_2", "item_weapon_x", "prismatic_gold" }, "base wins")]
    [TestCase(2, new[] { "item_weapon_x", "base_arcana_2", "prismatic_gold" }, "sets win")]
    public void Priority_FollowsTheHeroMethod_LastRowIsTop(int method, string[] expected, string mode)
    {
        var heroes = Heroes();
        heroes[0].BasePriority.Default = method;
        var zips = new[] { "prismatic_gold", "item_weapon_x", "base_arcana_2" }
            .Select(s => Zip("Juggernaut", s, Index(Jugg))).ToArray();

        var plan = SkinTesterPlan.Build(zips, heroes);

        Assert.That(plan.Errors, Is.Empty);
        Assert.That(plan.Rows.OrderBy(r => r.Order).Select(r => Path.GetFileNameWithoutExtension(r.File)), Is.EqualTo(expected));
        Assert.That(plan.Rows.Single(r => r.Top).File, Is.EqualTo("prismatic_gold.zip"));
        Assert.That(plan.Rows.Select(r => r.Mode).Distinct().Single(), Is.EqualTo(mode));
    }

    [Test]
    public void Priority_PerSetOverride_AppliesUnderThePublishedSetName_AndReadsMixed()
    {
        var heroes = Heroes();
        heroes[0].Sets["Exile Odachi"] = new() { $"{CdnConfig.R2BaseUrl}/Assets/models/Juggernaut/item_weapon_exile_odachi.zip" };
        heroes[0].BasePriority.Default = 1;
        heroes[0].BasePriority.Sets["Exile Odachi"] = 2;
        var zips = new[] { "mix_bladeform", "item_weapon_exile_odachi", "base_arcana_2" }
            .Select(s => Zip("Juggernaut", s, Index(Jugg))).ToArray();

        var plan = SkinTesterPlan.Build(zips, heroes);

        Assert.That(plan.Errors, Is.Empty);
        Assert.That(plan.Selections.Select(s => s.SetName), Does.Contain("Exile Odachi"));
        var rows = ByStem(plan);
        Assert.That(new[] { rows["item_weapon_exile_odachi"].Order, rows["base_arcana_2"].Order, rows["mix_bladeform"].Order },
            Is.EqualTo(new[] { 1, 2, 3 }), "odachi under the Base (method 2), the mix on top of it (method 1)");
        Assert.That(rows["mix_bladeform"].Top, Is.True);
        Assert.That(rows.Values.Select(r => r.Mode).Distinct().Single(), Is.EqualTo("mixed"));
    }

    [TestCase(true, "base_arcana_2")]
    [TestCase(false, "item_weapon_x")]
    public void Priority_NoMethod_DetectsHeroBaseSlotFromTheBaseZip(bool heroBaseSlot, string foundation)
    {
        var heroes = Heroes();
        heroes[0].BasePriority.Default = null;
        string baseIndex = heroBaseSlot
            ? "\"9059\"\n{\n\t\"item_slot\"\t\t\"hero_base\"\n\t\"used_by_heroes\"\n\t{\n\t\t\"" + Jugg + "\"\t\t\"1\"\n\t}\n}\n"
            : Index(Jugg);
        var zips = new[] { Zip("Juggernaut", "item_weapon_x", Index(Jugg)), Zip("Juggernaut", "base_arcana_2", baseIndex) };

        var plan = SkinTesterPlan.Build(zips, heroes);

        Assert.That(plan.Errors, Is.Empty);
        Assert.That(plan.Rows.Single(r => r.Order == 1).File, Is.EqualTo(foundation + ".zip"));
    }

    [Test]
    public void Priority_OrderMatchesThePipelineWeights()
    {
        var heroes = Heroes();
        heroes[0].BasePriority.Default = 2;
        var zips = new[] { "effect_blink", "mix_a", "base_b", "item_c", "persona_d", "prismatic_e" }
            .Select(s => Zip("Juggernaut", s, Index(Jugg))).ToArray();

        var plan = SkinTesterPlan.Build(zips, heroes);

        var expected = plan.Selections
            .OrderByDescending(s => HeroGenerationService.LayerWeight(heroes[0].BasePriority,
                HeroModelMapper.ClassifySet(s.Hero.Sets, s.SetName), s.SetName, false))
            .Select(s => s.SetName).ToList();
        var rowsBySet = plan.Rows.Zip(plan.Selections, (r, s) => (r, s.SetName)).OrderBy(x => x.r.Order).Select(x => x.SetName);
        Assert.That(rowsBySet, Is.EqualTo(expected));
    }

    [Test]
    public void Warnings_TwoBasesOrPersonaWithOthers_DoNotBlockThePlan()
    {
        var zips = new[] { "base_a", "base_b", "persona_c", "item_d" }
            .Select(s => Zip("Juggernaut", s, Index(Jugg))).ToArray();

        var plan = SkinTesterPlan.Build(zips, Heroes());

        Assert.That(plan.Errors, Is.Empty);
        Assert.That(plan.Selections, Has.Count.EqualTo(4));
        Assert.That(plan.Warnings, Has.Some.Contains("2 Base"));
        Assert.That(plan.Warnings, Has.Some.Contains("Persona"));
    }

    [Test]
    public void Warnings_OneOfEachLayer_None()
    {
        var zips = new[] { "base_a", "item_b", "prismatic_c" }.Select(s => Zip("Juggernaut", s, Index(Jugg))).ToArray();
        Assert.That(SkinTesterPlan.Build(zips, Heroes()).Warnings, Is.Empty);
    }

    #endregion

    #region LocalZipSetDownloader

    [Test]
    public async Task Downloader_PlainZip_ExtractsIndex()
    {
        string zip = Zip("Juggernaut", "item_weapon_exile_odachi", Index(Jugg), ("variants/juggernaut_arcana.skin1/a.txt", "v"));
        string url = $"{CdnConfig.R2BaseUrl}/Assets/models/Juggernaut/item_weapon_exile_odachi.zip";
        bool encrypted = false;

        string dir = await new LocalZipSetDownloader(new Dictionary<string, string> { [url] = zip })
            .DownloadAndExtractAsync(Jugg, "Test", url, _ => { }, onEncryptedDetected: () => encrypted = true);
        try
        {
            Assert.That(File.ReadAllText(Path.Combine(dir, "index.txt")), Does.Contain(Jugg));
            Assert.That(File.Exists(Path.Combine(dir, "variants", "juggernaut_arcana.skin1", "a.txt")));
            Assert.That(encrypted, Is.False);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Test]
    public async Task Downloader_EncryptedZip_DecryptsWithTheUrlAssetPath()
    {
        string plain = Zip("Juggernaut", "base_arcana_2", Index(Jugg));
        string enc = Path.Combine(_root, "enc.bin");
        File.WriteAllBytes(enc, AssetCipher.Encrypt(File.ReadAllBytes(plain), "Assets/models/Juggernaut/base_arcana_2.zip"));
        string url = $"{CdnConfig.R2BaseUrl}/Assets/models/Juggernaut/base_arcana_2.zip";
        bool encrypted = false;

        string dir = await new LocalZipSetDownloader(new Dictionary<string, string> { [url] = enc })
            .DownloadAndExtractAsync(Jugg, "Test", url, _ => { }, onEncryptedDetected: () => encrypted = true);
        try
        {
            Assert.That(encrypted, Is.True);
            Assert.That(File.ReadAllText(Path.Combine(dir, "index.txt")), Does.Contain(Jugg));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Test]
    public void Downloader_UnmappedUrl_NamesIt()
    {
        var ex = Assert.ThrowsAsync<FileNotFoundException>(() => new LocalZipSetDownloader(new Dictionary<string, string>())
            .DownloadAndExtractAsync(Jugg, "Test", "https://x/Assets/models/Juggernaut/nope.zip", _ => { }));
        Assert.That(ex!.Message, Does.Contain("nope.zip"));
    }

    #endregion
}
