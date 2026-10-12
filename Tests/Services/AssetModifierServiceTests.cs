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
using System.Threading.Tasks;
using ArdysaModsTools.Core.Constants;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Misc;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services;

[TestFixture]
public class AssetModifierServiceTests
{
    [Test]
    public void ResolveItemIdsForSelections_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AssetModifierService.ResolveItemIdsForSelections(null), Is.Empty);
            Assert.That(AssetModifierService.ResolveItemIdsForSelections(new Dictionary<string, string>()), Is.Empty);
        });
    }

    [Test]
    public void ResolveItemIdsForSelections_ResolvesStandardCategories()
    {
        var selections = new Dictionary<string, string>
        {
            { "Weather", "Ash" },
            { "Map", "Immortal Gardens" },
            { "Music", "The FatRat" },
            { "HUD", "Scifi" },
            { "Courier", "10746:https://cdn.example.com/courier.vpk" },
            { "Ward", "10747:https://cdn.example.com/ward.vpk" },
            { "Announcer", "RickAndMorty" },
            { "MegaKill", "GabeNewell" },
            { "Roshan", "Golden" },
            { "Cursor", "ChaosKnight" },
            { "Ancient", "CustomAncient" }
        };

        var resolved = AssetModifierService.ResolveItemIdsForSelections(selections);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Does.Contain("555"), "Weather");
            Assert.That(resolved, Does.Contain("590"), "Map");
            Assert.That(resolved, Does.Contain("588"), "Music");
            Assert.That(resolved, Does.Contain("587"), "HUD");
            Assert.That(resolved, Does.Contain("595"), "Courier default");
            Assert.That(resolved, Does.Contain("10746"), "Courier custom item id");
            Assert.That(resolved, Does.Contain("596"), "Ward default");
            Assert.That(resolved, Does.Contain("10747"), "Ward custom item id");
            Assert.That(resolved, Does.Contain("11173"), "Default Announcer");
            Assert.That(resolved, Does.Contain("586"), "Default Mega-Kills");
            Assert.That(resolved, Does.Contain("801"), "Default Roshan");
            Assert.That(resolved, Does.Contain("202"), "Default Cursor");
            Assert.That(resolved, Does.Contain("679"), "Ancient 1");
            Assert.That(resolved, Does.Contain("680"), "Ancient 2");
        });
    }

    [Test]
    public void MiscItemIds_ContainsAllBaseItems()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MiscItemIds.Defaults, Does.Contain("202"));
            Assert.That(MiscItemIds.Defaults, Does.Contain("555"));
            Assert.That(MiscItemIds.Defaults, Does.Contain("586"));
            Assert.That(MiscItemIds.Defaults, Does.Contain("587"));
            Assert.That(MiscItemIds.Defaults, Does.Contain("588"));
            Assert.That(MiscItemIds.Defaults, Does.Contain("590"));
            Assert.That(MiscItemIds.Defaults, Does.Contain("595"));
            Assert.That(MiscItemIds.Defaults, Does.Contain("596"));
            Assert.That(MiscItemIds.Defaults, Does.Contain("660"));
            Assert.That(MiscItemIds.Defaults, Does.Contain("661"));
            Assert.That(MiscItemIds.Defaults, Does.Contain("677"));
            Assert.That(MiscItemIds.Defaults, Does.Contain("678"));
            Assert.That(MiscItemIds.Defaults, Does.Contain("801"));
            Assert.That(MiscItemIds.Defaults, Does.Contain("11173"));
            Assert.That(MiscItemIds.Defaults, Does.Contain("12970"));
            Assert.That(MiscItemIds.Defaults, Does.Contain("34462"));
            Assert.That(MiscItemIds.Defaults, Does.Contain("34463"));
        });
    }

    [Test]
    public void ResolveItemIdsForSelections_MultikillBanner_TracksBothBannerIds()
    {
        var picked = AssetModifierService.ResolveItemIdsForSelections(
            new Dictionary<string, string> { { "multikill_banner", "Battle Glory" } });
        var dflt = AssetModifierService.ResolveItemIdsForSelections(
            new Dictionary<string, string> { { "multikill_banner", "Default Kill Banner" } });

        Assert.Multiple(() =>
        {
            Assert.That(picked, Is.EquivalentTo(new[] { "10731", "11548" }));
            Assert.That(dflt, Is.Empty);
        });
    }

    [Test]
    public void ResolveItemIdsForSelections_DefaultWardWithEthereal_TracksDefaultWard()
    {
        var withEth = AssetModifierService.ResolveItemIdsForSelections(
            new Dictionary<string, string> { { "Ward", "Default Ward" }, { "WardEthereal", "Sunfire" } });
        var plain = AssetModifierService.ResolveItemIdsForSelections(
            new Dictionary<string, string> { { "Ward", "Default Ward" } });

        Assert.Multiple(() =>
        {
            Assert.That(withEth, Does.Contain("596"));
            Assert.That(plain, Is.Empty);
        });
    }

    [Test]
    public void ApplyEthereal_WardBlock_ReplacesParticlesWithPickedEffects()
    {
        const string ward = "\"596\"\n{\n\t\"prefab\"\t\"ward\"\n\t\"visuals\"\n\t{\n\t\t\"asset_modifier0\"\n\t\t{\n\t\t\t\"type\"\t\"particle_create\"\n\t\t\t\"modifier\"\t\"particles/old.vpcf\"\n\t\t}\n\t}\n}";

        var result = AssetModifierService.ApplyEthereal(ward, "Sunfire, Unknown Effect", "Ward", _ => { }, out bool applied);

        Assert.Multiple(() =>
        {
            Assert.That(applied, Is.True);
            Assert.That(result, Does.Contain("courier_trail_05.vpcf"));
            Assert.That(result, Does.Not.Contain("particles/old.vpcf"));
        });
    }

    [Test]
    public void ApplyEthereal_NoKnownEffect_LeavesBlockUntouched()
    {
        const string ward = "\"596\"\n{\n\t\"visuals\"\n\t{\n\t}\n}";

        var result = AssetModifierService.ApplyEthereal(ward, "Nope", "Ward", _ => { }, out bool applied);

        Assert.Multiple(() =>
        {
            Assert.That(applied, Is.False);
            Assert.That(result, Is.EqualTo(ward));
        });
    }

    [Test]
    public void ResolveItemIdsForSelections_IgnoresDisabledAndDefaultOptions()
    {
        var selections = new Dictionary<string, string>
        {
            { "Weather", "Default" },
            { "Map", "Disable" },
            { "Music", "default" }
        };

        var resolved = AssetModifierService.ResolveItemIdsForSelections(selections);

        Assert.That(resolved, Is.Empty);
    }

    [Test]
    public void ResolveItemIdsForSelections_WithSpecialActive_ExcludesMapId590()
    {
        var selections = new Dictionary<string, string>
        {
            { "Special", "LowPolyMap" },
            { "Map", "Desert Terrain" },
            { "Weather", "Ash" }
        };

        var resolved = AssetModifierService.ResolveItemIdsForSelections(selections);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Does.Contain("555"), "Weather should be tracked");
            Assert.That(resolved, Does.Not.Contain("590"), "Map ID 590 must be excluded when Special (Low Poly) is active");
        });
    }

    [Test]
    public void ResolveItemIdsForSelections_WhenSpecialDisabled_AllowsMapId590()
    {
        var selections = new Dictionary<string, string>
        {
            { "Special", "Disable Special" },
            { "Map", "Desert Terrain" },
            { "Weather", "Ash" }
        };

        var resolved = AssetModifierService.ResolveItemIdsForSelections(selections);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Does.Contain("555"), "Weather should be tracked");
            Assert.That(resolved, Does.Contain("590"), "Map ID 590 should be tracked when Special is disabled");
        });
    }

    [Test]
    public void GetProtectedPaths_InitialState_IsEmpty()
    {
        var service = new AssetModifierService();
        Assert.That(service.GetProtectedPaths(), Is.Empty);
    }

    [Test]
    public async Task ApplyModificationsAsync_WithEncryptedEmblem_DecryptsAndTracksInProtectedPaths()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "AmtMiscProtectedTests_" + Guid.NewGuid().ToString("N"));
        string extractDir = Path.Combine(tempDir, "extract");
        Directory.CreateDirectory(Path.Combine(extractDir, "scripts", "items"));
        string itemsGamePath = Path.Combine(extractDir, "scripts", "items", "items_game.txt");
        await File.WriteAllTextAsync(itemsGamePath, "\"items_game\"\n{\n\t\"items\"\n\t{\n\t}\n}\n");

        try
        {
            byte[] rawVpcf = new byte[] { 0x11, 0x22, 0x33, 0x44 };
            string cdnUrl = "https://cdn.example.com/Assets/misc/emblems/selected_ring.vpcf_c";
            string assetPath = "Assets/misc/emblems/selected_ring.vpcf_c";
            byte[] encryptedBytes = Core.Services.Security.AssetCipher.Encrypt(rawVpcf, assetPath);

            var handler = new Helpers.FakeHttpMessageHandler((req, idx) =>
            {
                return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new System.Net.Http.ByteArrayContent(encryptedBytes)
                };
            });

            var httpClient = new System.Net.Http.HttpClient(handler);
            var service = new AssetModifierService(httpClient);

            var selections = new Dictionary<string, string>
            {
                { "Emblems", "Fire Emblem" }
            };

            RemoteMiscConfigService.SetLoadedConfigForTesting(new RemoteMiscConfig
            {
                Options = new List<RemoteMiscOption>
                {
                    new()
                    {
                        Id = "Emblems",
                        Choices = new List<RemoteMiscChoice>
                        {
                            new() { Name = "Fire Emblem", Url = cdnUrl }
                        }
                    }
                }
            });

            bool ok = await service.ApplyModificationsAsync(
                Path.Combine(tempDir, "game", "_ArdysaMods", "pak01_dir.vpk"),
                extractDir,
                selections,
                _ => { });

            Assert.That(ok, Is.True);
            Assert.That(service.GetProtectedPaths(), Does.Contain("particles/ui_mouseactions/selected_ring.vpcf_c"));

            string destFile = Path.Combine(extractDir, "particles", "ui_mouseactions", "selected_ring.vpcf_c");
            Assert.That(File.Exists(destFile), Is.True);
            Assert.That(await File.ReadAllBytesAsync(destFile), Is.EqualTo(rawVpcf));
        }
        finally
        {
            RemoteMiscConfigService.InvalidateCache();
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Test]
    public async Task ApplyModificationsAsync_WithEncryptedZipMod_DecryptsAndTracksOnlyProtectableEntries()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "AmtMiscProtectedZipTests_" + Guid.NewGuid().ToString("N"));
        string extractDir = Path.Combine(tempDir, "extract");
        Directory.CreateDirectory(Path.Combine(extractDir, "scripts", "items"));
        string itemsGamePath = Path.Combine(extractDir, "scripts", "items", "items_game.txt");
        await File.WriteAllTextAsync(itemsGamePath, "\"items_game\"\n{\n\t\"items\"\n\t{\n\t}\n}\n");

        try
        {
            byte[] zipPlaintext;
            using (var ms = new MemoryStream())
            {
                using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
                {
                    var modelEntry = zip.CreateEntry("models/roshan/roshan.vmdl_c");
                    using (var w = modelEntry.Open())
                        w.Write(new byte[] { 0xAA, 0xBB, 0xCC });

                    var txtEntry = zip.CreateEntry("patch.txt");
                    using (var sw = new StreamWriter(txtEntry.Open()))
                        sw.Write("\"801\"\n{\n\t\"name\"\t\"Custom Roshan\"\n}\n");
                }
                zipPlaintext = ms.ToArray();
            }

            string cdnUrl = "https://cdn.example.com/Assets/misc/roshan/custom_roshan.zip";
            string assetPath = "Assets/misc/roshan/custom_roshan.zip";
            byte[] encryptedZip = Core.Services.Security.AssetCipher.Encrypt(zipPlaintext, assetPath);

            var handler = new Helpers.FakeHttpMessageHandler((req, idx) =>
            {
                return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new System.Net.Http.ByteArrayContent(encryptedZip)
                };
            });

            var httpClient = new System.Net.Http.HttpClient(handler);
            var service = new AssetModifierService(httpClient);

            var selections = new Dictionary<string, string>
            {
                { "Roshan", "Custom Roshan" }
            };

            RemoteMiscConfigService.SetLoadedConfigForTesting(new RemoteMiscConfig
            {
                Options = new List<RemoteMiscOption>
                {
                    new()
                    {
                        Id = "roshan",
                        Choices = new List<RemoteMiscChoice>
                        {
                            new() { Name = "Custom Roshan", Url = cdnUrl }
                        }
                    }
                }
            });

            bool ok = await service.ApplyModificationsAsync(
                Path.Combine(tempDir, "game", "_ArdysaMods", "pak01_dir.vpk"),
                extractDir,
                selections,
                _ => { });

            Assert.That(ok, Is.True);
            var protectedPaths = service.GetProtectedPaths();

            Assert.Multiple(() =>
            {
                Assert.That(protectedPaths, Does.Contain("models/roshan/roshan.vmdl_c"));
                Assert.That(protectedPaths, Does.Not.Contain("scripts/items/items_game.txt"));
                Assert.That(protectedPaths, Does.Not.Contain("patch.txt"));
            });

            string extractedModel = Path.Combine(extractDir, "models", "roshan", "roshan.vmdl_c");
            Assert.That(File.Exists(extractedModel), Is.True);
            Assert.That(await File.ReadAllBytesAsync(extractedModel), Is.EqualTo(new byte[] { 0xAA, 0xBB, 0xCC }));
        }
        finally
        {
            RemoteMiscConfigService.InvalidateCache();
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }


    private const string AnniversaryId = "29339";
    private const string FrostivusId = "29588";
    private const string ObsceneEyeBallId = "34576";
    private static readonly string[] TormentorIds = { AnniversaryId, FrostivusId, ObsceneEyeBallId };

    private static string Tormentors(string? flagged = null) =>
        string.Concat(TormentorIds.Select(id => TormentorBlock(id, id == flagged)));

    private static string TormentorBlock(string id, bool baseitem) =>
        $"\"{id}\"\n{{\n\t\"name\"\t\t\"Tormentor {id}\"\n\t\"prefab\"\t\t\"tormentor\"\n" +
        (baseitem ? "\t\"baseitem\"\t\t\"1\"\n" : "") +
        $"\t\"item_name\"\t\t\"#DOTA_Item_Tormentor_{id}\"\n" +
        "\t\"visuals\"\n\t{\n" +
        "\t\t\"asset_modifier\"\n\t\t{\n\t\t\t\"type\"\t\t\"entity_clientside_model\"\n\t\t\t\"asset\"\t\t\"npc_dota_miniboss\"\n\t\t}\n" +
        "\t\t\"skip_model_combine\"\t\t\"1\"\n" +
        "\t\t\"asset_modifier\"\n\t\t{\n\t\t\t\"type\"\t\t\"particle\"\n\t\t\t\"asset\"\t\t\"particles/neutral_fx/miniboss_shield.vpcf\"\n\t\t}\n" +
        "\t}\n}\n";

    private static string ItemsGame(params string[] blocks) =>
        "\"items_game\"\n{\n\t\"items\"\n\t{\n" + string.Concat(blocks) + "\t}\n}\n";

    private static bool HasBaseitem(string itemsGame, string id)
    {
        var block = Core.Helpers.KeyValuesBlockHelper.ExtractBlockById(itemsGame, id);
        Assert.That(block, Is.Not.Null, $"block {id} missing from items_game");
        return Core.Helpers.KeyValuesBlockHelper.TryGetTopLevelValue(block!, "baseitem", out var v) && v == "1";
    }

    private static async Task<(AssetModifierService Service, string ItemsGame, Helpers.FakeHttpMessageHandler Http)> RunMiscAsync(
        string optionId, string choice, string url, byte[] payload, string itemsGame, Core.Interfaces.IAppLogger? logger = null)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "AmtMiscSlotTests_" + Guid.NewGuid().ToString("N"));
        string extractDir = Path.Combine(tempDir, "extract");
        string itemsGamePath = Path.Combine(extractDir, "scripts", "items", "items_game.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(itemsGamePath)!);
        await File.WriteAllTextAsync(itemsGamePath, itemsGame);

        var http = new Helpers.FakeHttpMessageHandler((_, _) =>
            new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new System.Net.Http.ByteArrayContent(payload)
            });
        var service = new AssetModifierService(new System.Net.Http.HttpClient(http), logger);

        RemoteMiscConfigService.SetLoadedConfigForTesting(new RemoteMiscConfig
        {
            Options = new List<RemoteMiscOption>
            {
                new() { Id = optionId, Choices = new List<RemoteMiscChoice> { new() { Name = choice, Url = url } } }
            }
        });

        try
        {
            bool ok = await service.ApplyModificationsAsync(
                Path.Combine(tempDir, "game", "_ArdysaMods", "pak01_dir.vpk"),
                extractDir,
                new Dictionary<string, string> { { optionId, choice } },
                _ => { });
            Assert.That(ok, Is.True);
            return (service, await File.ReadAllTextAsync(itemsGamePath), http);
        }
        finally
        {
            RemoteMiscConfigService.InvalidateCache();
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static byte[] Utf8(string s) => System.Text.Encoding.UTF8.GetBytes(s);

    private const string TormentorUrl = "https://cdn.example.com/Assets/misc/tormentor/unit_test_pick.txt";

    [Test]
    public void MiscItemIds_SlotExclusive_IsWellFormed()
    {
        var seen = new HashSet<string>();
        foreach (var (category, (displayName, ids)) in MiscItemIds.SlotExclusive)
        {
            Assert.Multiple(() =>
            {
                Assert.That(displayName, Is.Not.Empty, category);
                Assert.That(ids, Has.Length.GreaterThanOrEqualTo(2), $"{category}: a slot with one item needs no flip");
                foreach (var id in ids)
                {
                    Assert.That(int.TryParse(id, out _), Is.True, $"{category}: '{id}' is not an item id");
                    Assert.That(seen.Add(id), Is.True, $"{category}: id {id} belongs to two slots");
                    Assert.That(MiscItemIds.Defaults, Does.Not.Contain(id),
                        $"{category}: {id} must not be verbatim-merged — the overlay has to keep Valve's later keys");
                }
            });
        }
        Assert.That(MiscItemIds.SlotExclusive.Keys, Is.SupersetOf(new[] { "multikill_banner", "tormentor" }));
    }

    [Test]
    public void ResolveItemIdsForSelections_Tormentor_TracksEveryTormentorId()
    {
        var picked = AssetModifierService.ResolveItemIdsForSelections(
            new Dictionary<string, string> { { "tormentor", "Obscene Eye Ball" } });
        var dflt = AssetModifierService.ResolveItemIdsForSelections(
            new Dictionary<string, string> { { "tormentor", "Default Tormentor" } });

        Assert.Multiple(() =>
        {
            Assert.That(picked, Is.EquivalentTo(TormentorIds));
            Assert.That(dflt, Is.Empty);
        });
    }

    [Test]
    public async Task Tormentor_PickObsceneEyeBall_MovesBaseitemOffPreviousPick()
    {
        var (service, result, _) = await RunMiscAsync("tormentor", "Obscene Eye Ball", TormentorUrl,
            Utf8(Tormentors(flagged: ObsceneEyeBallId)), ItemsGame(Tormentors(flagged: AnniversaryId)));

        Assert.Multiple(() =>
        {
            Assert.That(HasBaseitem(result, ObsceneEyeBallId), Is.True);
            Assert.That(HasBaseitem(result, AnniversaryId), Is.False, "two baseitems in one slot");
            Assert.That(HasBaseitem(result, FrostivusId), Is.False);
            Assert.That(service.GetWarnings(), Is.Empty);
            Assert.That(service.GetModifiedItemIds(), Is.EquivalentTo(TormentorIds));
            Assert.That(service.GetUnpatchedItemIds(), Is.Empty);
        });
    }

    [Test]
    public async Task Tormentor_AssetMissingSlotBlock_SkipsWholeCategory()
    {
        var before = ItemsGame(Tormentors(flagged: ObsceneEyeBallId));
        var (service, result, _) = await RunMiscAsync("tormentor", "Frostivus 2024 Tormentor", TormentorUrl,
            Utf8(TormentorBlock(AnniversaryId, false) + TormentorBlock(FrostivusId, true)), before);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(before));
            Assert.That(service.GetWarnings(), Has.Some.Contains("Tormentor").And.Contains(ObsceneEyeBallId));
        });
    }

    [Test]
    public async Task Tormentor_AssetWithTwoBaseitems_SkipsWholeCategory()
    {
        var before = ItemsGame(Tormentors());
        var asset = TormentorBlock(AnniversaryId, true) + TormentorBlock(FrostivusId, true) + TormentorBlock(ObsceneEyeBallId, false);
        var (service, result, _) = await RunMiscAsync("tormentor", "Frostivus 2024 Tormentor", TormentorUrl, Utf8(asset), before);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(before));
            Assert.That(service.GetWarnings(), Has.Some.Contains("Tormentor"));
        });
    }

    [Test]
    public async Task Tormentor_HtmlInsteadOfBlocks_LeavesPackageAndWarns()
    {
        var before = ItemsGame(Tormentors());
        var (service, result, _) = await RunMiscAsync("tormentor", "Frostivus 2024 Tormentor", TormentorUrl,
            Utf8("<html><head><script>challenge()</script></head><body>Just a moment...</body></html>"), before);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(before));
            Assert.That(service.GetWarnings(), Has.Some.Contains("Tormentor"));
        });
    }

    [Test]
    public async Task Tormentor_PackageHasSlotItemTheTableLacks_LogsItForTheMaintainer()
    {
        const string unknownId = "39999";
        var logger = new Moq.Mock<Core.Interfaces.IAppLogger>();
        var (service, result, _) = await RunMiscAsync("tormentor", "Obscene Eye Ball", TormentorUrl,
            Utf8(Tormentors(flagged: ObsceneEyeBallId)),
            ItemsGame(Tormentors(), TormentorBlock(unknownId, false)), logger.Object);

        Assert.Multiple(() =>
        {
            Assert.That(HasBaseitem(result, ObsceneEyeBallId), Is.True, "an unknown item must not block the pick");
            Assert.That(service.GetWarnings(), Is.Empty, "nothing the user can act on");
            logger.Verify(l => l.Log(Moq.It.Is<string>(m => m.Contains(unknownId) && m.Contains("SlotExclusive")),
                Moq.It.IsAny<Core.Interfaces.LogLevel>()), Moq.Times.Once);
        });
    }

    [Test]
    public void FindSlotItemIds_ReturnsOnlyItemsWhoseOwnPrefabOrSlotMatches()
    {
        const string content =
            "\"items_game\"\n{\n" +
            "\t\"loadout_slots\"\n\t{\n\t\t\"29\"\t\t\"tormentor\"\n\t}\n" +
            "\t\"prefabs\"\n\t{\n\t\t\"tormentor\"\n\t\t{\n\t\t\t\"item_slot\"\t\t\"tormentor\"\n\t\t}\n\t}\n" +
            "\t\"items\"\n\t{\n" +
            "\t\t\"29339\"\n\t\t{\n\t\t\t\"name\"\t\t\"a\"\n\t\t\t\"prefab\"\t\t\"tormentor\"\n\t\t}\n" +
            "\t\t\"10731\"\n\t\t{\n\t\t\t\"prefab\"\t\t\"misc\"\n\t\t\t\"item_slot\"\t\t\"multikill_banner\"\n\t\t}\n" +
            "\t\t\"500\"\n\t\t{\n\t\t\t\"visuals\"\n\t\t\t{\n\t\t\t\t\"x\"\t\t\"1\"\n\t\t\t}\n\t\t\t\"prefab\"\t\t\"tormentor\"\n\t\t}\n" +
            "\t\t\"600\"\n\t\t{\n\t\t\t\"styles\"\n\t\t\t{\n\t\t\t\t\"prefab\"\t\t\"tormentor\"\n\t\t\t}\n\t\t}\n" +
            "\t}\n}\n";

        Assert.Multiple(() =>
        {
            Assert.That(AssetModifierService.FindSlotItemIds(content, "tormentor"), Is.EqualTo(new[] { "29339" }));
            Assert.That(AssetModifierService.FindSlotItemIds(content, "multikill_banner"), Is.EqualTo(new[] { "10731" }));
            Assert.That(AssetModifierService.FindSlotItemIds("", "tormentor"), Is.Empty);
        });
    }

    [Test]
    public async Task DefaultTormentor_RestoresEveryBlock_AndReportsThemUnpatched()
    {
        var (service, result, _) = await RunMiscAsync("tormentor", "Default Tormentor",
            "https://cdn.example.com/Assets/misc/tormentor/unit_test_default.txt",
            Utf8(Tormentors()), ItemsGame(Tormentors(flagged: FrostivusId)));

        Assert.Multiple(() =>
        {
            Assert.That(TormentorIds.Where(id => HasBaseitem(result, id)), Is.Empty);
            Assert.That(service.GetUnpatchedItemIds(), Is.EquivalentTo(TormentorIds),
                "a restored block left 'patched' in the baseline keeps a stale copy after the next Dota patch");
            Assert.That(service.GetModifiedItemIds(), Is.Empty);
        });
    }

    [Test]
    public async Task DefaultRoshanZip_ReportsRestoredBlockUnpatched()
    {
        const string vanilla = "\"801\"\n{\n\t\"name\"\t\t\"Default Roshan\"\n\t\"prefab\"\t\t\"roshan\"\n\t\"baseitem\"\t\t\"1\"\n}\n";
        byte[] zip;
        using (var ms = new MemoryStream())
        {
            using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            using (var sw = new StreamWriter(archive.CreateEntry("default.txt").Open()))
                sw.Write(vanilla);
            zip = ms.ToArray();
        }

        var (service, _, _) = await RunMiscAsync("roshan", "Default Roshan",
            "https://cdn.example.com/Assets/misc/roshan/unit_test_default.zip", zip,
            ItemsGame(vanilla.Replace("Default Roshan", "Cosmic Roshan")));

        Assert.Multiple(() =>
        {
            Assert.That(service.GetUnpatchedItemIds(), Does.Contain("801"));
            Assert.That(service.GetModifiedItemIds(), Does.Not.Contain("801"));
        });
    }

    [Test]
    public async Task DefaultAncient_PlaceholderUrl_DownloadsNothingAndWarnsNothing()
    {
        var (service, _, http) = await RunMiscAsync("ancient", "Default Ancient",
            "https://cdn.example.com/Assets/misc/shader/Diretide/materials/dev/root.md",
            Utf8("# placeholder"), ItemsGame());

        Assert.Multiple(() =>
        {
            Assert.That(http.CallCount, Is.Zero);
            Assert.That(service.GetWarnings(), Is.Empty);
        });
    }
}

