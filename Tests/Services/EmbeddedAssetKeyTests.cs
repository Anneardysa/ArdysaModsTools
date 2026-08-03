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
using ArdysaModsTools.Core.Services.Security;
using NUnit.Framework;

namespace ArdysaModsTools.Tests.Services
{
    [TestFixture]
    public class EmbeddedAssetKeyTests
    {
        private const string Asset = "Assets/models/Drow_Ranger/dread.zip";

        [Test]
        public void DeriveKey_ProducesA256BitKey()
        {
            Assert.That(EmbeddedAssetKey.DeriveKey(Asset).Length, Is.EqualTo(32));
        }

        [Test]
        public void DeriveKey_IsDeterministic()
        {
            Assert.That(EmbeddedAssetKey.DeriveKey(Asset),
                Is.EqualTo(EmbeddedAssetKey.DeriveKey(Asset)));
        }

        [Test]
        public void DeriveKey_DiffersPerAsset()
        {
            Assert.That(EmbeddedAssetKey.DeriveKey(Asset),
                Is.Not.EqualTo(EmbeddedAssetKey.DeriveKey("Assets/models/Axe/blade.zip")));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void DeriveKey_WithoutAssetPath_Throws(string? assetPath)
        {
            Assert.Throws<ArgumentException>(() => EmbeddedAssetKey.DeriveKey(assetPath!));
        }


        [Test]
        public void KeyMaterial_Epoch1_IsTheBareAssetPath()
        {
            Assert.That(EmbeddedAssetKey.KeyMaterial(Asset, 1), Is.EqualTo(Asset));
        }

        [Test]
        public void KeyMaterial_LaterEpochs_ArePrefixed()
        {
            Assert.That(EmbeddedAssetKey.KeyMaterial(Asset, 2), Is.EqualTo("e2:" + Asset));
            Assert.That(EmbeddedAssetKey.KeyMaterial(Asset, 17), Is.EqualTo("e17:" + Asset));
        }

        [Test]
        public void DeriveKey_UsesTheShippedEpoch()
        {
            Assert.That(EmbeddedAssetKey.DeriveKey(Asset),
                Is.EqualTo(EmbeddedAssetKey.DeriveKey(Asset, EmbeddedAssetKey.AssetEpoch)));
        }

        [Test]
        public void DeriveKey_DiffersAcrossEpochs()
        {
            Assert.That(EmbeddedAssetKey.DeriveKey(Asset, 1),
                Is.Not.EqualTo(EmbeddedAssetKey.DeriveKey(Asset, 2)));
            Assert.That(EmbeddedAssetKey.DeriveKey(Asset, 2),
                Is.Not.EqualTo(EmbeddedAssetKey.DeriveKey(Asset, 3)));
        }

        [Test]
        public void KeyMaterial_RejectsColonInAssetPath()
        {
            Assert.Throws<ArgumentException>(() => EmbeddedAssetKey.KeyMaterial("e2:" + Asset, 1));
            Assert.Throws<ArgumentException>(() => EmbeddedAssetKey.DeriveKey("e2:" + Asset));
        }
    }
}
