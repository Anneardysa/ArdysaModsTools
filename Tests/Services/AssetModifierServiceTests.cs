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
using System.Threading.Tasks;
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
    public void KnownMiscDefaultItemIds_ContainsAllBaseItems()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AssetModifierService.KnownMiscDefaultItemIds, Does.Contain("202"));
            Assert.That(AssetModifierService.KnownMiscDefaultItemIds, Does.Contain("555"));
            Assert.That(AssetModifierService.KnownMiscDefaultItemIds, Does.Contain("586"));
            Assert.That(AssetModifierService.KnownMiscDefaultItemIds, Does.Contain("587"));
            Assert.That(AssetModifierService.KnownMiscDefaultItemIds, Does.Contain("588"));
            Assert.That(AssetModifierService.KnownMiscDefaultItemIds, Does.Contain("590"));
            Assert.That(AssetModifierService.KnownMiscDefaultItemIds, Does.Contain("595"));
            Assert.That(AssetModifierService.KnownMiscDefaultItemIds, Does.Contain("596"));
            Assert.That(AssetModifierService.KnownMiscDefaultItemIds, Does.Contain("660"));
            Assert.That(AssetModifierService.KnownMiscDefaultItemIds, Does.Contain("661"));
            Assert.That(AssetModifierService.KnownMiscDefaultItemIds, Does.Contain("677"));
            Assert.That(AssetModifierService.KnownMiscDefaultItemIds, Does.Contain("678"));
            Assert.That(AssetModifierService.KnownMiscDefaultItemIds, Does.Contain("801"));
            Assert.That(AssetModifierService.KnownMiscDefaultItemIds, Does.Contain("11173"));
            Assert.That(AssetModifierService.KnownMiscDefaultItemIds, Does.Contain("12970"));
            Assert.That(AssetModifierService.KnownMiscDefaultItemIds, Does.Contain("34462"));
            Assert.That(AssetModifierService.KnownMiscDefaultItemIds, Does.Contain("34463"));
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
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}

