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
using NUnit.Framework;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Services;

namespace ArdysaModsTools.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="ActiveModsService"/>.
    /// Validates the unified mod query layer across hero and misc extraction logs.
    /// </summary>
    [TestFixture]
    public class ActiveModsServiceTests
    {
        private ActiveModsService _service = null!;
        private string _tempDir = null!;

        [SetUp]
        public void Setup()
        {
            _service = new ActiveModsService();
            _tempDir = Path.Combine(Path.GetTempPath(), $"amt_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                try { Directory.Delete(_tempDir, true); } catch { }
            }
        }

        #region GetActiveModsAsync — Input Validation

        [Test]
        public async Task GetActiveModsAsync_NullPath_ReturnsNotChecked()
        {
            var result = await _service.GetActiveModsAsync(null!);

            Assert.That(result.OverallStatus, Is.EqualTo(ModStatus.NotChecked));
            Assert.That(result.HasActiveMods, Is.False);
            Assert.That(result.TotalModCount, Is.Zero);
        }

        [Test]
        public async Task GetActiveModsAsync_EmptyPath_ReturnsNotChecked()
        {
            var result = await _service.GetActiveModsAsync("");

            Assert.That(result.OverallStatus, Is.EqualTo(ModStatus.NotChecked));
        }

        [Test]
        public async Task GetActiveModsAsync_WhitespacePath_ReturnsNotChecked()
        {
            var result = await _service.GetActiveModsAsync("   ");

            Assert.That(result.OverallStatus, Is.EqualTo(ModStatus.NotChecked));
        }

        #endregion

        #region GetActiveModsAsync — No Logs

        [Test]
        public async Task GetActiveModsAsync_NoLogs_ReturnsNotInstalled()
        {
            var result = await _service.GetActiveModsAsync(_tempDir);

            Assert.That(result.OverallStatus, Is.EqualTo(ModStatus.NotInstalled));
            Assert.That(result.HeroMods, Is.Empty);
            Assert.That(result.MiscMods, Is.Empty);
            Assert.That(result.TotalModCount, Is.Zero);
            Assert.That(result.HasActiveMods, Is.False);
        }

        #endregion

        #region GetActiveModsAsync — Hero Mods

        [Test]
        public async Task GetActiveModsAsync_WithHeroLog_ReturnsHeroMods()
        {
            // Arrange: create a hero extraction log
            var heroLog = new HeroExtractionLog
            {
                InstalledSets = new List<HeroSetEntry>
                {
                    new HeroSetEntry
                    {
                        HeroId = "npc_dota_hero_antimage",
                        SetName = "Mage Slayer",
                        Files = new List<string> { "models/hero_am.vmdl" }
                    },
                    new HeroSetEntry
                    {
                        HeroId = "npc_dota_hero_invoker",
                        SetName = "Dark Artistry",
                        Files = new List<string> { "models/hero_inv.vmdl", "materials/inv_cape.vmat" }
                    }
                }
            };
            heroLog.Save(_tempDir);

            // Act
            var result = await _service.GetActiveModsAsync(_tempDir);

            // Assert
            Assert.That(result.OverallStatus, Is.EqualTo(ModStatus.Ready));
            Assert.That(result.HeroMods, Has.Count.EqualTo(2));
            Assert.That(result.HeroMods[0].HeroId, Is.EqualTo("npc_dota_hero_antimage"));
            Assert.That(result.HeroMods[0].SetName, Is.EqualTo("Mage Slayer"));
            Assert.That(result.HeroMods[1].HeroId, Is.EqualTo("npc_dota_hero_invoker"));
            Assert.That(result.HasActiveMods, Is.True);
        }

        [Test]
        public async Task GetActiveModsAsync_HeroLogWithEmptyHeroId_SkipsEntry()
        {
            var heroLog = new HeroExtractionLog
            {
                InstalledSets = new List<HeroSetEntry>
                {
                    new HeroSetEntry { HeroId = "", SetName = "Invalid" },
                    new HeroSetEntry { HeroId = "npc_dota_hero_axe", SetName = "Red Mist" }
                }
            };
            heroLog.Save(_tempDir);

            var result = await _service.GetActiveModsAsync(_tempDir);

            Assert.That(result.HeroMods, Has.Count.EqualTo(1));
            Assert.That(result.HeroMods[0].HeroId, Is.EqualTo("npc_dota_hero_axe"));
        }

        #endregion

        #region GetActiveModsAsync — Misc Mods

        [Test]
        public async Task GetActiveModsAsync_WithMiscLog_ReturnsMiscMods()
        {
            var miscLog = new MiscExtractionLog
            {
                GeneratedAt = new DateTime(2026, 2, 14, 12, 0, 0, DateTimeKind.Utc),
                Selections = new Dictionary<string, string>
                {
                    ["Weather"] = "Rain",
                    ["HUD"] = "Immortal Gardens"
                },
                InstalledFiles = new Dictionary<string, List<string>>
                {
                    ["Weather"] = new List<string> { "particles/rain.vpcf" }
                }
            };
            miscLog.Save(_tempDir);

            var result = await _service.GetActiveModsAsync(_tempDir);

            Assert.That(result.OverallStatus, Is.EqualTo(ModStatus.Ready));
            Assert.That(result.MiscMods, Has.Count.EqualTo(2));
            Assert.That(result.MiscMods[0].Category, Is.EqualTo("Weather"));
            Assert.That(result.MiscMods[0].SelectedChoice, Is.EqualTo("Rain"));
            Assert.That(result.MiscMods[0].InstalledFiles, Has.Count.EqualTo(1));
            Assert.That(result.MiscMods[1].Category, Is.EqualTo("HUD"));
            Assert.That(result.MiscMods[1].InstalledFiles, Is.Empty);
            Assert.That(result.LastGeneratedAt, Is.Not.Null);
        }

        [Test]
        public async Task GetActiveModsAsync_MiscLogWithEmptyChoice_SkipsEntry()
        {
            var miscLog = new MiscExtractionLog
            {
                Selections = new Dictionary<string, string>
                {
                    ["Weather"] = "",
                    ["HUD"] = "Custom HUD"
                }
            };
            miscLog.Save(_tempDir);

            var result = await _service.GetActiveModsAsync(_tempDir);

            Assert.That(result.MiscMods, Has.Count.EqualTo(1));
            Assert.That(result.MiscMods[0].Category, Is.EqualTo("HUD"));
        }

        #endregion

        #region GetActiveModsAsync — Combined

        [Test]
        public async Task GetActiveModsAsync_BothLogs_ReturnsCombined()
        {
            var heroLog = new HeroExtractionLog
            {
                InstalledSets = new List<HeroSetEntry>
                {
                    new HeroSetEntry { HeroId = "npc_dota_hero_axe", SetName = "Red Mist" }
                }
            };
            heroLog.Save(_tempDir);

            var miscLog = new MiscExtractionLog
            {
                Selections = new Dictionary<string, string> { ["Weather"] = "Snow" }
            };
            miscLog.Save(_tempDir);

            var result = await _service.GetActiveModsAsync(_tempDir);

            Assert.That(result.OverallStatus, Is.EqualTo(ModStatus.Ready));
            Assert.That(result.TotalModCount, Is.EqualTo(2));
            Assert.That(result.HeroMods, Has.Count.EqualTo(1));
            Assert.That(result.MiscMods, Has.Count.EqualTo(1));
        }

        #endregion

        #region GetActiveCategories

        [Test]
        public async Task GetActiveCategories_CombinedLogs_ReturnsAllCategories()
        {
            var heroLog = new HeroExtractionLog
            {
                InstalledSets = new List<HeroSetEntry>
                {
                    new HeroSetEntry { HeroId = "npc_dota_hero_axe", SetName = "Red Mist" }
                }
            };
            heroLog.Save(_tempDir);

            var miscLog = new MiscExtractionLog
            {
                Selections = new Dictionary<string, string>
                {
                    ["Weather"] = "Rain",
                    ["HUD"] = "Custom"
                }
            };
            miscLog.Save(_tempDir);

            var result = await _service.GetActiveModsAsync(_tempDir);
            var categories = result.GetActiveCategories();

            Assert.That(categories, Contains.Item("Hero"));
            Assert.That(categories, Contains.Item("Weather"));
            Assert.That(categories, Contains.Item("HUD"));
        }

        #endregion

        #region Single-Item Lookups

        [Test]
        public async Task GetActiveHeroModAsync_ExistingHero_ReturnsMod()
        {
            var heroLog = new HeroExtractionLog
            {
                InstalledSets = new List<HeroSetEntry>
                {
                    new HeroSetEntry { HeroId = "npc_dota_hero_axe", SetName = "Red Mist" }
                }
            };
            heroLog.Save(_tempDir);

            var result = await _service.GetActiveHeroModAsync(_tempDir, "npc_dota_hero_axe");

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.SetName, Is.EqualTo("Red Mist"));
        }

        [Test]
        public async Task GetActiveHeroModAsync_NonExistentHero_ReturnsNull()
        {
            var result = await _service.GetActiveHeroModAsync(_tempDir, "npc_dota_hero_pudge");

            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task GetActiveHeroModAsync_NullHeroId_ReturnsNull()
        {
            var result = await _service.GetActiveHeroModAsync(_tempDir, null!);

            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task GetActiveMiscModAsync_ExistingCategory_ReturnsMod()
        {
            var miscLog = new MiscExtractionLog
            {
                Selections = new Dictionary<string, string> { ["Weather"] = "Rain" }
            };
            miscLog.Save(_tempDir);

            var result = await _service.GetActiveMiscModAsync(_tempDir, "Weather");

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.SelectedChoice, Is.EqualTo("Rain"));
        }

        [Test]
        public async Task GetActiveMiscModAsync_CaseInsensitive_ReturnsMod()
        {
            var miscLog = new MiscExtractionLog
            {
                Selections = new Dictionary<string, string> { ["Weather"] = "Rain" }
            };
            miscLog.Save(_tempDir);

            var result = await _service.GetActiveMiscModAsync(_tempDir, "weather");

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.SelectedChoice, Is.EqualTo("Rain"));
        }

        [Test]
        public async Task GetActiveMiscModAsync_NonExistentCategory_ReturnsNull()
        {
            var result = await _service.GetActiveMiscModAsync(_tempDir, "Terrain");

            Assert.That(result, Is.Null);
        }

        #endregion

        #region Cancellation

        [Test]
        public void GetActiveModsAsync_CancelledToken_ThrowsOperationCancelled()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(
                () => _service.GetActiveModsAsync(_tempDir, cts.Token));
        }

        #endregion

        #region ActiveModInfo Properties

        [Test]
        public void ActiveModInfo_DefaultValues_AreCorrect()
        {
            var info = new ActiveModInfo();

            Assert.That(info.OverallStatus, Is.EqualTo(ModStatus.NotChecked));
            Assert.That(info.HeroMods, Is.Empty);
            Assert.That(info.MiscMods, Is.Empty);
            Assert.That(info.TotalModCount, Is.Zero);
            Assert.That(info.HasActiveMods, Is.False);
            Assert.That(info.LastGeneratedAt, Is.Null);
        }

        [Test]
        public void ActiveModInfo_GetActiveCategories_EmptyWhenNoMods()
        {
            var info = new ActiveModInfo();
            Assert.That(info.GetActiveCategories(), Is.Empty);
        }

        #endregion
    }
}
