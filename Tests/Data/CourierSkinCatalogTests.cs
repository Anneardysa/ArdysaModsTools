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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ArdysaModsTools.Core.Data;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Data
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class CourierSkinCatalogTests
    {
        #region GetMaterialSwaps Tests

        [Test]
        public void GetMaterialSwaps_GoldenBabyRoshan_ReturnsExpectedSwap()
        {
            const string itemId = "10096";

            var swaps = CourierSkinCatalog.GetMaterialSwaps(itemId);

            Assert.That(swaps, Is.Not.Null);
            Assert.That(swaps.Count, Is.EqualTo(1));
            Assert.That(swaps[0].SourceMaterial, Is.EqualTo("materials/models/courier/baby_rosh/babyrosh_gold_color.vmat_c"));
            Assert.That(swaps[0].TargetMaterial, Is.EqualTo("materials/models/courier/baby_rosh/babyrosh_color.vmat_c"));
        }

        [Test]
        public void GetMaterialSwaps_PlatinumBabyRoshan_ReturnsExpectedSwap()
        {
            const string itemId = "10374";

            var swaps = CourierSkinCatalog.GetMaterialSwaps(itemId);

            Assert.That(swaps, Is.Not.Null);
            Assert.That(swaps.Count, Is.EqualTo(1));
            Assert.That(swaps[0].SourceMaterial, Is.EqualTo("materials/models/courier/baby_rosh/babyrosh_platinum_color.vmat_c"));
            Assert.That(swaps[0].TargetMaterial, Is.EqualTo("materials/models/courier/baby_rosh/babyrosh_color.vmat_c"));
        }

        [Test]
        public void GetMaterialSwaps_IceBabyRoshan_ReturnsExpectedSwap()
        {
            const string itemId = "10479";

            var swaps = CourierSkinCatalog.GetMaterialSwaps(itemId);

            Assert.That(swaps, Is.Not.Null);
            Assert.That(swaps.Count, Is.EqualTo(1));
            Assert.That(swaps[0].SourceMaterial, Is.EqualTo("materials/models/courier/baby_rosh/babyrosh_ice_color.vmat_c"));
            Assert.That(swaps[0].TargetMaterial, Is.EqualTo("materials/models/courier/baby_rosh/babyrosh_color.vmat_c"));
        }

        [Test]
        public void GetMaterialSwaps_LavaBabyRoshan_ReturnsExpectedSwap()
        {
            const string itemId = "10478";

            var swaps = CourierSkinCatalog.GetMaterialSwaps(itemId);

            Assert.That(swaps, Is.Not.Null);
            Assert.That(swaps.Count, Is.EqualTo(1));
            Assert.That(swaps[0].SourceMaterial, Is.EqualTo("materials/models/courier/baby_rosh/babyrosh_lava_color.vmat_c"));
            Assert.That(swaps[0].TargetMaterial, Is.EqualTo("materials/models/courier/baby_rosh/babyrosh_color.vmat_c"));
        }

        [Test]
        public void GetMaterialSwaps_GroundAndFlyingCourier_ReturnsMultipleSwaps()
        {
            const string itemId = "11764";

            var swaps = CourierSkinCatalog.GetMaterialSwaps(itemId);

            Assert.That(swaps, Is.Not.Null);
            Assert.That(swaps.Count, Is.EqualTo(2));
            Assert.That(swaps.Any(s => s.SourceMaterial.Contains("golden_devourling.vmat_c")), Is.True);
            Assert.That(swaps.Any(s => s.SourceMaterial.Contains("golden_devourling_flying.vmat_c")), Is.True);
        }

        [Test]
        public void GetMaterialSwaps_StyledCourier_ResolvesStyleKeyFirst()
        {
            const string itemId = "10174";
            const int styleIndex = 1;

            var swaps = CourierSkinCatalog.GetMaterialSwaps(itemId, styleIndex);

            Assert.That(swaps, Is.Not.Null);
            Assert.That(swaps.Count, Is.EqualTo(2));
            Assert.That(swaps.Any(s => s.SourceMaterial.Contains("smeevil_red.vmat_c")), Is.True);
            Assert.That(swaps.Any(s => s.SourceMaterial.Contains("smeevil_crab_red.vmat_c")), Is.True);
        }

        [Test]
        public void GetMaterialSwaps_UnskinnedCourier_ReturnsEmptyList()
        {
            const string drodoId = "10003";

            var swaps = CourierSkinCatalog.GetMaterialSwaps(drodoId);

            Assert.That(swaps, Is.Not.Null);
            Assert.That(swaps.Count, Is.EqualTo(0));
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("invalid_id")]
        public void GetMaterialSwaps_InvalidOrEmptyId_ReturnsEmptyList(string? itemId)
        {
            var swaps = CourierSkinCatalog.GetMaterialSwaps(itemId!);

            Assert.That(swaps, Is.Not.Null);
            Assert.That(swaps.Count, Is.EqualTo(0));
        }

        #endregion

        #region GetMaterialExtractionDirs Tests

        [Test]
        public void GetMaterialExtractionDirs_ValidSwaps_ReturnsUniqueParentDirectories()
        {
            var swaps = new List<CourierMaterialSwap>
            {
                new()
                {
                    SourceMaterial = "materials/models/courier/baby_rosh/babyrosh_gold_color.vmat_c",
                    TargetMaterial = "materials/models/courier/baby_rosh/babyrosh_color.vmat_c"
                },
                new()
                {
                    SourceMaterial = "materials/models/courier/baby_rosh/babyrosh_platinum_color.vmat_c",
                    TargetMaterial = "materials/models/courier/baby_rosh/babyrosh_color.vmat_c"
                },
                new()
                {
                    SourceMaterial = "materials/models/items/courier/other/other_skin.vmat_c",
                    TargetMaterial = "materials/models/items/courier/other/other_base.vmat_c"
                }
            };

            var dirs = CourierSkinCatalog.GetMaterialExtractionDirs(swaps);

            Assert.That(dirs, Is.Not.Null);
            Assert.That(dirs.Count, Is.EqualTo(2));
            Assert.That(dirs, Does.Contain("materials/models/courier/baby_rosh"));
            Assert.That(dirs, Does.Contain("materials/models/items/courier/other"));
        }

        [Test]
        public void GetMaterialExtractionDirs_NullOrEmptySwaps_ReturnsEmptyList()
        {
            Assert.That(CourierSkinCatalog.GetMaterialExtractionDirs(null!), Is.Empty);
            Assert.That(CourierSkinCatalog.GetMaterialExtractionDirs(new List<CourierMaterialSwap>()), Is.Empty);
        }

        #endregion
    }
}
