/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

namespace ArdysaModsTools.Core.Data
{
    /// <summary>
    /// Static dictionary of all Ethereal (unusual) particle effects for couriers.
    /// Each effect is a particle_create entry that gets appended to the courier's visuals block.
    /// 
    /// Slot rules:
    ///   - If the courier has 0 existing particle_create entries → max 2 Ethereal effects
    ///   - If the courier has 1 existing particle_create entry  → max 1 Ethereal effect
    ///   - If the courier has 2+ existing particle_create entries → Ethereal is unavailable
    /// </summary>
    public static class EtherealEffects
    {
        /// <summary>
        /// Maximum total particle_create slots allowed on a courier.
        /// </summary>
        public const int MaxParticleSlots = 2;

        /// <summary>
        /// All available Ethereal effects: display name → particle VPF path.
        /// </summary>
        public static readonly Dictionary<string, string> Effects = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Touch of Midas (Midas Gold)"] = "particles/econ/courier/courier_golden_roshan/golden_roshan_ambient.vpcf",
            ["Touch of Flame (Pyroclastic Flow)"] = "particles/econ/courier/courier_roshan_lava/courier_roshan_lava.vpcf",
            ["Touch of Frost (Glacial Flow)"] = "particles/econ/courier/courier_roshan_frost/courier_roshan_frost_ambient.vpcf",
            ["Desert Sands"] = "particles/econ/courier/courier_roshan_desert_sands/baby_roshan_desert_sands_ambient.vpcf",
            ["Dark Moon"] = "particles/econ/courier/courier_roshan_darkmoon/courier_roshan_darkmoon.vpcf",
            ["Ionic Vapor"] = "particles/econ/courier/courier_platinum_roshan/platinum_roshan_ambient.vpcf",
            ["Trail of Burning Doom"] = "particles/econ/courier/courier_trail_lava/courier_trail_lava.vpcf",
            ["Butterfly Romp (Plushy Shag)"] = "particles/econ/courier/courier_shagbark/courier_shagbark_ambient.vpcf",
            ["New Bloom Celebration"] = "particles/econ/courier/courier_trail_fireworks/courier_trail_fireworks.vpcf",
            ["Blossom Red"] = "particles/econ/courier/courier_trail_01/blossom_red_courier_trail_01.vpcf",
            ["Glacial Flow"] = "particles/econ/courier/courier_trail_01/glacial_flow_courier_trail_01.vpcf",
            ["Resonant Energy (ShipsInTheNight)"] = "particles/econ/courier/courier_trail_02/courier_trail_02.vpcf",
            ["Felicity's Blessing (Light Green)"] = "particles/econ/courier/courier_trail_03/courier_trail_03.vpcf",
            ["Affliction of Vermin"] = "particles/econ/courier/courier_trail_04/courier_trail_04.vpcf",
            ["Trail of the Amanita"] = "particles/econ/courier/courier_trail_fungal/courier_trail_fungal.vpcf",
            ["Sunfire"] = "particles/econ/courier/courier_trail_05/courier_trail_05.vpcf",
            ["Spirit of Earth"] = "particles/econ/courier/courier_trail_earth/courier_trail_earth.vpcf",
            ["Spirit of Ember (Ember Flame)"] = "particles/econ/courier/courier_trail_ember/courier_trail_ember.vpcf",
            ["Orbital Decay"] = "particles/econ/courier/courier_trail_orbit/courier_trail_orbit.vpcf",
            ["Bleak Hallucination (Creator's Light)"] = "particles/econ/courier/courier_trail_spirit/courier_trail_spirit.vpcf",
            ["Rubiline Sheen"] = "particles/econ/courier/courier_trail_ruby/courier_trail_ruby.vpcf",
            ["Deep Green"] = "particles/econ/courier/courier_polycount_01/courier_trail_polycount_01.vpcf",
            ["Divine Essence (Creator's Light)"] = "particles/econ/courier/courier_trail_divine/courier_trail_divine_ambient.vpcf",
            ["Cursed Essence"] = "particles/econ/courier/courier_trail_cursed/courier_trail_cursed_ambient.vpcf",
            ["Crystal Rift (Crystalline Blue)"] = "particles/econ/courier/courier_crystal_rift/courier_ambient_crystal_rift.vpcf",
            ["Blossom Red (Int 2014)"] = "particles/econ/courier/courier_trail_blossoms/courier_trail_blossoms.vpcf",
            ["Luminous Gaze (Explosive Burst)"] = "particles/econ/courier/courier_eye_glow_01/courier_eye_glow_01.vpcf",
            ["Searing Essence (ShipsInTheNight)"] = "particles/econ/courier/courier_eye_glow_02/courier_eye_glow_02.vpcf",
            ["Burning Animus (Gold)"] = "particles/econ/courier/courier_eye_glow_03/courier_eye_glow_03.vpcf",
            ["Piercing Beams (Blue)"] = "particles/econ/courier/courier_eye_glow_04/courier_eye_glow_04.vpcf",
            ["Triumph of Champions"] = "particles/econ/courier/courier_eye_glow_defense_01/courier_eye_glow_defense_01.vpcf",
            ["Frostivus Frost"] = "particles/econ/courier/courier_trail_winter_2012/courier_trail_winter_2012.vpcf",
            ["Diretide Corruption"] = "particles/econ/courier/courier_trail_hw_2012/courier_trail_hw_2012.vpcf",
            ["Diretide Blight"] = "particles/econ/courier/courier_trail_hw_2013/courier_trail_hw_2013.vpcf",
            ["Champion's Aura 2012 (Champion's Blue)"] = "particles/econ/courier/courier_trail_int_2012/champ_blue_courier_trail_international_2012.vpcf",
            ["Champion's Aura 2013 (Champion's Green)"] = "particles/econ/courier/courier_trail_international_2013/champ_green_courier_international_2013.vpcf",
            ["Champion's Aura 2014 (Champion's Purple)"] = "particles/econ/courier/courier_trail_international_2014/champ_purple_courier_international_2014.vpcf",
            ["Blossom Red (Int 2014 Alt)"] = "particles/econ/courier/courier_trail_international_2014/blossom_red_courier_international_2014.vpcf",
            ["Plushy Shag"] = "particles/econ/courier/courier_trail_international_2014/plushy_shag_courier_international_2014.vpcf",
        };

        /// <summary>
        /// Returns how many Ethereal slots are available given the existing particle_create count.
        /// </summary>
        public static int GetAvailableSlots(int existingParticleCount)
        {
            int available = MaxParticleSlots - existingParticleCount;
            return Math.Max(0, available);
        }

        /// <summary>
        /// Validates a particle path is a known Ethereal effect.
        /// </summary>
        public static bool IsValidEffect(string particlePath)
        {
            return Effects.Values.Any(v => v.Equals(particlePath, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Look up particle path by effect name.
        /// Returns null if not found.
        /// </summary>
        public static string? GetParticlePath(string effectName)
        {
            return Effects.TryGetValue(effectName, out var path) ? path : null;
        }
    }
}
