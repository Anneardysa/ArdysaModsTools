/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */
using NUnit.Framework;
using ArdysaModsTools.Core.Models;
using ArdysaModsTools.Core.Constants;

namespace ArdysaModsTools.Tests.Services
{
    /// <summary>
    /// Tests for Special VPK option: model flags, config propagation, and constants.
    /// </summary>
    [TestFixture]
    public class SpecialVpkTests
    {
        #region RemoteMiscOption.IsSpecialVpk Tests

        [Test]
        public void IsSpecialVpk_TypeVpk_ReturnsTrue()
        {
            var option = new RemoteMiscOption { Type = "vpk" };
            Assert.That(option.IsSpecialVpk, Is.True);
        }

        [Test]
        public void IsSpecialVpk_TypeVpkUpperCase_ReturnsTrue()
        {
            var option = new RemoteMiscOption { Type = "VPK" };
            Assert.That(option.IsSpecialVpk, Is.True);
        }

        [Test]
        public void IsSpecialVpk_TypeNull_ReturnsFalse()
        {
            var option = new RemoteMiscOption { Type = null };
            Assert.That(option.IsSpecialVpk, Is.False);
        }

        [Test]
        public void IsSpecialVpk_TypeEmpty_ReturnsFalse()
        {
            var option = new RemoteMiscOption { Type = "" };
            Assert.That(option.IsSpecialVpk, Is.False);
        }

        [Test]
        public void IsSpecialVpk_TypeOther_ReturnsFalse()
        {
            var option = new RemoteMiscOption { Type = "rar" };
            Assert.That(option.IsSpecialVpk, Is.False);
        }

        [Test]
        public void IsSpecialVpk_DefaultOption_ReturnsFalse()
        {
            var option = new RemoteMiscOption();
            Assert.That(option.IsSpecialVpk, Is.False);
        }

        #endregion

        #region MiscOption.IsSpecialVpk Tests

        [Test]
        public void MiscOption_IsSpecialVpk_DefaultFalse()
        {
            var option = new MiscOption();
            Assert.That(option.IsSpecialVpk, Is.False);
        }

        [Test]
        public void MiscOption_IsSpecialVpk_CanBeSetTrue()
        {
            var option = new MiscOption { IsSpecialVpk = true };
            Assert.That(option.IsSpecialVpk, Is.True);
        }

        #endregion

        #region DotaPaths Constants Tests

        [Test]
        public void ModsVpk_IsMainVpkPath()
        {
            Assert.That(DotaPaths.ModsVpk, Is.EqualTo("game/_ArdysaMods/pak01_dir.vpk"));
        }

        #endregion

        #region JSON Deserialization Tests

        [Test]
        public void RemoteMiscOption_DeserializesTypeField()
        {
            var json = """{"id":"Special","displayName":"Low Poly Map","category":"Environment","type":"vpk","choices":[]}""";
            var option = System.Text.Json.JsonSerializer.Deserialize<RemoteMiscOption>(json);

            Assert.That(option, Is.Not.Null);
            Assert.That(option!.Type, Is.EqualTo("vpk"));
            Assert.That(option.IsSpecialVpk, Is.True);
        }

        [Test]
        public void RemoteMiscOption_DeserializesWithoutTypeField()
        {
            var json = """{"id":"Weather","displayName":"Weather","category":"Environment","choices":[]}""";
            var option = System.Text.Json.JsonSerializer.Deserialize<RemoteMiscOption>(json);

            Assert.That(option, Is.Not.Null);
            Assert.That(option!.Type, Is.Null);
            Assert.That(option.IsSpecialVpk, Is.False);
        }

        #endregion

        #region ExcludesWith Tests

        [Test]
        public void RemoteMiscOption_ExcludesWith_DefaultEmpty()
        {
            var option = new RemoteMiscOption();
            Assert.That(option.ExcludesWith, Is.Not.Null);
            Assert.That(option.ExcludesWith, Is.Empty);
        }

        [Test]
        public void RemoteMiscOption_ExcludesWith_DeserializesFromJson()
        {
            var json = """{"id":"Special","displayName":"Low Poly Map","category":"Environment","type":"vpk","excludesWith":["Map"],"choices":[]}""";
            var option = System.Text.Json.JsonSerializer.Deserialize<RemoteMiscOption>(json);

            Assert.That(option, Is.Not.Null);
            Assert.That(option!.ExcludesWith, Has.Count.EqualTo(1));
            Assert.That(option.ExcludesWith[0], Is.EqualTo("Map"));
        }

        [Test]
        public void RemoteMiscOption_ExcludesWith_BidirectionalConfig()
        {
            var jsonMap = """{"id":"Map","excludesWith":["Special"],"choices":[]}""";
            var jsonSpecial = """{"id":"Special","excludesWith":["Map"],"choices":[]}""";

            var map = System.Text.Json.JsonSerializer.Deserialize<RemoteMiscOption>(jsonMap);
            var special = System.Text.Json.JsonSerializer.Deserialize<RemoteMiscOption>(jsonSpecial);

            Assert.That(map!.ExcludesWith, Contains.Item("Special"));
            Assert.That(special!.ExcludesWith, Contains.Item("Map"));
        }

        [Test]
        public void MiscOption_ExcludesWith_DefaultEmpty()
        {
            var option = new MiscOption();
            Assert.That(option.ExcludesWith, Is.Not.Null);
            Assert.That(option.ExcludesWith, Is.Empty);
        }

        [Test]
        public void RemoteMiscOption_WithoutExcludesWith_DeserializesEmpty()
        {
            var json = """{"id":"Weather","displayName":"Weather","category":"Environment","choices":[]}""";
            var option = System.Text.Json.JsonSerializer.Deserialize<RemoteMiscOption>(json);

            Assert.That(option, Is.Not.Null);
            Assert.That(option!.ExcludesWith, Is.Not.Null);
            Assert.That(option.ExcludesWith, Is.Empty);
        }

        #endregion
    }
}
