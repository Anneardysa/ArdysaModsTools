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
using ArdysaModsTools.Core.Services;
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
}

