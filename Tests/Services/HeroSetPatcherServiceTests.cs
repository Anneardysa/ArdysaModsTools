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
using ArdysaModsTools.Core.Services;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class HeroSetPatcherServiceTests
    {
        private HeroSetPatcherService _service = null!;

        [SetUp]
        public void Setup()
        {
            _service = new HeroSetPatcherService();
        }

        #region Service Instance Tests

        [Test]
        public void Constructor_CreatesInstance()
        {
            var service = new HeroSetPatcherService();

            Assert.That(service, Is.Not.Null);
        }

        [Test]
        public void Service_ImplementsInterface()
        {
            var service = new HeroSetPatcherService();

            Assert.That(service, Is.InstanceOf<IHeroSetPatcher>());
        }

        #endregion

        #region ParseIndexFile Parameter Validation Tests

        [Test]
        public void ParseIndexFile_WithNullContent_ReturnsNull()
        {
            var heroId = "test_hero";
            var itemIds = new List<int> { 12345 };

            var result = _service.ParseIndexFile(null!, heroId, itemIds);

            Assert.That(result, Is.Null);
        }

        [Test]
        public void ParseIndexFile_WithEmptyHeroId_ReturnsNull()
        {
            var content = "test content";
            var itemIds = new List<int> { 12345 };

            var result = _service.ParseIndexFile(content, "", itemIds);

            Assert.That(result, Is.Null);
        }

        [Test]
        public void ParseIndexFile_WithNullItemIds_ReturnsNull()
        {
            var content = "test content";
            var heroId = "test_hero";

            var result = _service.ParseIndexFile(content, heroId, null!);

            Assert.That(result, Is.Null);
        }

        [Test]
        public void ParseIndexFile_WithEmptyItemIds_ReturnsNull()
        {
            var content = "test content";
            var heroId = "test_hero";
            var itemIds = new List<int>();

            var result = _service.ParseIndexFile(content, heroId, itemIds);

            Assert.That(result, Is.Null);
        }

        #endregion

        #region ParseIndexText Tests

        [Test]
        public void ParseIndexText_ReturnsBlockForRequestedId()
        {
            var indexText = @"""855""
{
	""name""		""Test Item""
	""used_by_heroes""
	{
		""npc_dota_hero_earthshaker""		""1""
	}
}";
            var result = _service.ParseIndexText(indexText, "npc_dota_hero_earthshaker", new List<int> { 855 });

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.ContainsKey("855"), Is.True);
            Assert.That(result["855"].block, Does.Contain("Test Item"));
            Assert.That(result["855"].heroId, Is.EqualTo("npc_dota_hero_earthshaker"));
        }

        [Test]
        public void ParseIndexText_UnrequestedId_ReturnsNull()
        {
            var indexText = @"""855""
{
	""item_slot""		""hero_base""
}";
            var result = _service.ParseIndexText(indexText, "npc_dota_hero_earthshaker", new List<int> { 999 });
            Assert.That(result, Is.Null);
        }

        #endregion
    }
}

