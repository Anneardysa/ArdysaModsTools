/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */
using ArdysaModsTools.Core.Services;
using ArdysaModsTools.Models;

namespace ArdysaModsTools.Core.Data
{
    public sealed record HeroParticle(string Id, string Name, string Path, string Source,
        string Fit = "model", bool Courier = false, string? Pools = null);

    public sealed record ParticleSlot(string Id, string Label, bool HasModel);

    public sealed record ParticleSlots(IReadOnlyList<ParticleSlot> Slots, string? Preferred);

    public static class HeroParticleCatalog
    {
        public const int MaxPerHero = 2;

        public static readonly IReadOnlyList<HeroParticle> All = new HeroParticle[]
        {
            new("22", "Affliction of Vermin", "particles/econ/courier/courier_trail_04/courier_trail_04.vpcf", "unusual", "hang", false),
            new("159", "Bleak Hallucination", "particles/econ/courier/courier_trail_spirit/courier_trail_spirit.vpcf", "unusual", "hang", false),
            new("196", "Butterfly Romp", "particles/econ/courier/courier_shagbark/courier_shagbark_ambient.vpcf", "unusual", "model", true),
            new("827", "Candy Caster", "particles/econ/seasonal/seasonal_ambient_candy_mint.vpcf", "unusual", "model", false, "Frostivus 2023, Frostivus 2024"),
            new("37", "Champion's Aura 2012", "particles/econ/courier/courier_trail_int_2012/courier_trail_international_2012.vpcf", "unusual", "model", true),
            new("109", "Champion's Aura 2013", "particles/econ/courier/courier_trail_international_2013/courier_international_2013.vpcf", "unusual", "hang", false),
            new("268", "Champion's Aura 2014", "particles/econ/courier/courier_trail_international_2014/courier_international_2014.vpcf", "unusual", "hang", false),
            new("68", "Crystal Rift", "particles/econ/courier/courier_crystal_rift/courier_ambient_crystal_rift.vpcf", "unusual", "model", false),
            new("73", "Cursed Essence", "particles/econ/courier/courier_trail_cursed/courier_cursed_ambient.vpcf", "unusual", "model", false),
            new("155", "Diretide Blight", "particles/econ/courier/courier_trail_hw_2013/courier_trail_hw_2013.vpcf", "unusual", "hang", false),
            new("46", "Diretide Corruption", "particles/econ/courier/courier_trail_hw_2012/courier_trail_hw_2012.vpcf", "unusual", "hang", false),
            new("74", "Divine Essence", "particles/econ/courier/courier_trail_divine/courier_divine_ambient.vpcf", "unusual", "model", false),
            new("138", "Emerald Ectoplasm", "particles/econ/courier/courier_polycount_01/courier_trail_polycount_01.vpcf", "unusual", "hang", false),
            new("15", "Ethereal Flame", "particles/econ/courier/courier_trail_01/courier_trail_01.vpcf", "unusual", "hang", false),
            new("836", "Featherfall", "particles/econ/seasonal/crownfall/seasonal_ambient_screeauk.vpcf", "unusual", "model", false, "Crownfall"),
            new("21", "Felicity's Blessing", "particles/econ/courier/courier_trail_03/courier_trail_03.vpcf", "unusual", "hang", false),
            new("826", "Frostbloom", "particles/econ/seasonal/seasonal_ambient_silver.vpcf", "unusual", "model", false, "Frostivus 2023, Frostivus 2024"),
            new("57", "Frostivus Frost", "particles/econ/courier/courier_trail_winter_2012/courier_trail_winter_2012.vpcf", "unusual", "hang", false),
            new("833", "Gold Drop", "particles/econ/seasonal/new_bloom/seasonal_ambient_gold_drop.vpcf", "unusual", "model", false, "New Bloom 2024"),
            new("834", "Gold Glow", "particles/econ/seasonal/new_bloom/seasonal_ambient_gold_glow.vpcf", "unusual", "model", false, "New Bloom 2024"),
            new("163", "Ionic Vapor", "particles/econ/courier/courier_platinum_roshan/platinum_roshan_ambient.vpcf", "unusual", "hang", true),
            new("158", "Orbital Decay", "particles/econ/courier/courier_trail_orbit/courier_trail_orbit.vpcf", "unusual", "model", false),
            new("837", "Ravenblight", "particles/econ/seasonal/crownfall/seasonal_ambient_screeauk_v2.vpcf", "unusual", "model", false, "Crownfall"),
            new("129", "Rubiline Sheen", "particles/econ/courier/courier_trail_ruby/courier_trail_ruby.vpcf", "unusual", "model", false),
            new("835", "Smoke", "particles/econ/seasonal/new_bloom/seasonal_ambient_smoke.vpcf", "unusual", "model", false, "New Bloom 2024"),
            new("828", "Snowdrift", "particles/econ/seasonal/seasonal_ambient_snow.vpcf", "unusual", "model", false, "Frostivus 2023, Frostivus 2024"),
            new("157", "Spirit of Earth", "particles/econ/courier/courier_trail_earth/courier_trail_earth.vpcf", "unusual", "hang", false),
            new("156", "Spirit of Ember", "particles/econ/courier/courier_trail_ember/courier_trail_ember.vpcf", "unusual", "model", false),
            new("32", "Sunfire", "particles/econ/courier/courier_trail_05/courier_trail_05.vpcf", "unusual", "hang", false),
            new("206", "Touch of Flame", "particles/econ/courier/courier_roshan_lava/courier_roshan_lava.vpcf", "unusual", "hang", true),
            new("205", "Touch of Frost", "particles/econ/courier/courier_roshan_frost/courier_roshan_frost_ambient.vpcf", "unusual", "hang", true),
            new("47", "Touch of Midas", "particles/econ/courier/courier_golden_roshan/golden_roshan_ambient.vpcf", "unusual", "hang", true),
            new("96", "Trail of Burning Doom", "particles/econ/courier/courier_trail_lava/courier_trail_lava.vpcf", "unusual", "hang", false),
            new("76", "Trail of the Amanita", "particles/econ/courier/courier_trail_fungal/courier_trail_fungal.vpcf", "unusual", "hang", false),
            new("61", "Trail of the Lotus Blossom", "particles/econ/courier/courier_trail_blossoms/courier_trail_blossoms.vpcf", "unusual", "hang", false),
            new("i11321.1", "Wyvern Hatchling", "particles/econ/courier/courier_wyvern_hatchling/courier_wyvern_hatchling_fire.vpcf", "courier", "model", true),
        };

        private static readonly Dictionary<string, HeroParticle> ById =
            All.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);

        public static readonly IReadOnlySet<string> WithPreview = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "15", "21", "22", "32", "37", "46", "47", "57", "61", "68", "73", "74", "76", "96", "109", "129", "138", "155",
            "156", "157", "158", "159", "163", "196", "205", "206", "268", "826", "827", "828", "833", "834", "835", "836",
            "837", "i11321.1",
        };

        public static bool TryGet(string? id, out HeroParticle particle)
        {
            if (!string.IsNullOrWhiteSpace(id) && ById.TryGetValue(id.Trim(), out var found))
            {
                particle = found;
                return true;
            }
            particle = null!;
            return false;
        }

        public static IReadOnlyList<(string Id, string Label)> HostsFor(HeroModel? hero)
        {
            if (hero?.ItemIds == null || string.IsNullOrWhiteSpace(hero.Id))
                return Array.Empty<(string, string)>();

            var hosts = new List<(string Id, string Label)>();
            foreach (var itemId in hero.ItemIds.Distinct())
            {
                if (!HeroDefaultItemRegistry.TryGetItem(itemId, out var info)
                    || info.Slot.Contains("persona", StringComparison.OrdinalIgnoreCase)
                    || !info.HeroId.Split(',').Any(h => h.Trim().Equals(hero.Id, StringComparison.OrdinalIgnoreCase)))
                    continue;
                hosts.Add((info.Id, info.SlotDisplayName));
            }
            return hosts;
        }
    }
}
