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
using ArdysaModsTools.Core.Services;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class HeroGenerationServiceTests
    {
        #region GetSortWeight Tests

        [Test]
        public void GetSortWeight_WhenBaseHasHeroBaseSlotIsTrue_ReturnsCorrectWeights()
        {
            // BaseHero should be highest priority (wins)
            int weightBase = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.BaseHero, true);
            Assert.That(weightBase, Is.EqualTo(3));

            // LegacySet, CustomSet, Persona should be middle priority
            int weightLegacy = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.LegacySet, true);
            int weightCustom = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.CustomSet, true);
            int weightPersona = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.Persona, true);
            Assert.That(weightLegacy, Is.EqualTo(2));
            Assert.That(weightCustom, Is.EqualTo(2));
            Assert.That(weightPersona, Is.EqualTo(2));

            // Item should be lowest priority
            int weightItem = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.Item, true);
            Assert.That(weightItem, Is.EqualTo(1));
        }

        [Test]
        public void GetSortWeight_WhenBaseHasHeroBaseSlotIsFalse_ReturnsCorrectWeights()
        {
            // LegacySet, CustomSet, Persona should be highest priority (wins)
            int weightLegacy = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.LegacySet, false);
            int weightCustom = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.CustomSet, false);
            int weightPersona = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.Persona, false);
            Assert.That(weightLegacy, Is.EqualTo(3));
            Assert.That(weightCustom, Is.EqualTo(3));
            Assert.That(weightPersona, Is.EqualTo(3));

            // Item should be middle priority
            int weightItem = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.Item, false);
            Assert.That(weightItem, Is.EqualTo(2));

            // BaseHero should be lowest priority
            int weightBase = HeroGenerationService.GetSortWeight(HeroModelMapper.SkinCategory.BaseHero, false);
            Assert.That(weightBase, Is.EqualTo(1));
        }

        #endregion

        #region IndexFileHasHeroBaseSlot Tests

        [Test]
        public void IndexFileHasHeroBaseSlot_NoIndexFile_ReturnsFalse()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                // Act
                bool result = HeroGenerationService.IndexFileHasHeroBaseSlot(tempDir);

                // Assert
                Assert.That(result, Is.False);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Test]
        public void IndexFileHasHeroBaseSlot_ContainsHeroBaseSlot_ReturnsTrue()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                string indexPath = Path.Combine(tempDir, "index.txt");
                string indexContent = @"""855""
{
	""name""		""Earthshaker's Base""
	""item_slot""		""hero_base""
}";
                File.WriteAllText(indexPath, indexContent);

                // Act
                bool result = HeroGenerationService.IndexFileHasHeroBaseSlot(tempDir);

                // Assert
                Assert.That(result, Is.True);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Test]
        public void IndexFileHasHeroBaseSlot_DoesNotContainHeroBaseSlot_ReturnsFalse()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                string indexPath = Path.Combine(tempDir, "index.txt");
                string indexContent = @"""460""
{
	""name""		""Earthshaker's Bracers""
	""item_slot""		""arms""
}";
                File.WriteAllText(indexPath, indexContent);

                // Act
                bool result = HeroGenerationService.IndexFileHasHeroBaseSlot(tempDir);

                // Assert
                Assert.That(result, Is.False);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Test]
        public void IndexFileHasHeroBaseSlot_CaseInsensitiveAndWhitespaceFlexible_ReturnsTrue()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                string indexPath = Path.Combine(tempDir, "index.txt");
                string indexContent = @"""855""
{
	""ITEM_SLOT""	   	""HERO_BASE""
}";
                File.WriteAllText(indexPath, indexContent);

                // Act
                bool result = HeroGenerationService.IndexFileHasHeroBaseSlot(tempDir);

                // Assert
                Assert.That(result, Is.True);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        #endregion
    }
}
