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
using System.IO;
using System.Linq;
using NUnit.Framework;
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Core.Services.Misc;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class MiscCategoryServiceTests
    {
        private string? _backupPath;

        [SetUp]
        public void SetUp()
        {
            var path = RemoteMiscConfigService.CacheFilePath;
            if (File.Exists(path))
            {
                _backupPath = path + ".testbak";
                File.Copy(path, _backupPath, overwrite: true);
            }
            else
            {
                _backupPath = null;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            }

            RemoteMiscConfigService.InvalidateCache();
        }

        [TearDown]
        public void TearDown()
        {
            var path = RemoteMiscConfigService.CacheFilePath;
            if (_backupPath != null && File.Exists(_backupPath))
            {
                File.Move(_backupPath, path, overwrite: true);
                _backupPath = null;
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }

            RemoteMiscConfigService.InvalidateCache();
        }

        private static void WriteConfig(string json)
        {
            File.WriteAllText(RemoteMiscConfigService.CacheFilePath, json);
            RemoteMiscConfigService.InvalidateCache();
        }

        #region thumbnailId override mapping

        [Test]
        public void GetAllOptions_TopLevelThumbnailId_ResolvesToOverrideStem()
        {
            WriteConfig("""
            {
              "version": "test",
              "thumbnailBaseUrl": "https://cdn.ardysamods.my.id/Assets/misc",
              "categories": ["X"],
              "options": [{
                "id": "DireCreep", "displayName": "Dire Creep", "category": "X",
                "thumbnailFolder": "dire_creep",
                "choices": [{ "name": "Crownfall", "thumbnailId": "cavernite" }]
              }]
            }
            """);

            var option = MiscCategoryService.GetAllOptions().Single(o => o.Id == "DireCreep");

            Assert.That(option.GetThumbnailUrl("Crownfall"),
                Is.EqualTo("https://cdn.ardysamods.my.id/Assets/misc/dire_creep/cavernite.webp"));
        }

        [Test]
        public void GetAllOptions_StyleThumbnailId_FlowsThroughToOverride()
        {
            WriteConfig("""
            {
              "version": "test",
              "thumbnailBaseUrl": "https://cdn.ardysamods.my.id/Assets/misc",
              "categories": ["X"],
              "options": [{
                "id": "Courier", "displayName": "Courier", "category": "X",
                "thumbnailFolder": "courier",
                "choices": [{
                  "name": "Some Courier",
                  "styles": [{ "name": "Golden Variant", "thumbnailId": "shared_courier_art" }]
                }]
              }]
            }
            """);

            var option = MiscCategoryService.GetAllOptions().Single(o => o.Id == "Courier");

            Assert.Multiple(() =>
            {
                Assert.That(option.ChoiceThumbnailIds.ContainsKey("Golden Variant"), Is.True);
                Assert.That(option.GetThumbnailUrl("Golden Variant"),
                    Is.EqualTo("https://cdn.ardysamods.my.id/Assets/misc/courier/shared_courier_art.webp"));
            });
        }

        [Test]
        public void GetAllOptions_StyleWithoutThumbnailId_FallsBackToSanitizedName()
        {
            WriteConfig("""
            {
              "version": "test",
              "thumbnailBaseUrl": "https://cdn.ardysamods.my.id/Assets/misc",
              "categories": ["X"],
              "options": [{
                "id": "Courier", "displayName": "Courier", "category": "X",
                "thumbnailFolder": "courier",
                "choices": [{
                  "name": "Some Courier",
                  "styles": [{ "name": "Plain Variant" }]
                }]
              }]
            }
            """);

            var option = MiscCategoryService.GetAllOptions().Single(o => o.Id == "Courier");

            Assert.That(option.GetThumbnailUrl("Plain Variant"),
                Is.EqualTo("https://cdn.ardysamods.my.id/Assets/misc/courier/plain_variant.webp"));
        }

        #endregion
    }
}
