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

namespace ArdysaModsTools.Core.Services
{
    /// <summary>
    /// Provides all ComboBox item lists for MiscForm UI.
    /// Mirrors dictionary keys in MiscGenerationService (display names only).
    /// </summary>
    public static class ComboBoxDataService
    {
        public static List<string> GetWeatherOptions() => new()
        {
            "Default Weather", "Weather Ash", "Weather Aurora", "Weather Harvest",
            "Weather Moonbeam", "Weather Pestilence", "Weather Rain", "Weather Sirocco",
            "Weather Snow", "Weather Spring"
        };

        public static List<string> GetMapOptions() => new()
        {
            "Default Terrain", "Seasonal - Autumn", "Seasonal - Spring", "Seasonal - Summer",
            "Seasonal - Winter", "Desert Terrain", "Immortal Gardens", "Reef's Edge",
            "Emerald Abyss", "Overgrown Empire", "Sanctum of the Divine", "The King's New Journey"
        };

        public static List<string> GetMusicOptions() => new()
        {
            "Default Music", "Deadmau5 Music", "Elemental Fury Music", "Desert Music",
            "Magic Sticks of Dynamite", "The FatRat Warrior Songs", "Heroes Within Music",
            "Humanitys Last Breath Void", "Northern Winds Music", "JJ Lins Timekeeper Music"
        };

        public static List<string> GetEmblemOptions() => new()
        {
            "Disable Emblem", "Aghanim 2021 Emblem", "BattlePass 2022 Emblem",
            "Crystal Echeron Emblem", "Divinity Emblem", "Diretide Green",
            "Diretide Blue", "Diretide Red", "Diretide Yellow", "Nemestice Emblem",
            "Overgrown Emblem", "Sunken Emblem"
        };

        public static List<string> GetShaderOptions() => new()
        {
            "Disable Shader", "Aghanim Labyrinth", "Diretide"
        };

        public static List<string> GetAtkModifierOptions() => new()
        {
            "Disable Attack Modifier", "Aghanim's Labyrinth", "Diretide - Blue", "Diretide - Green",
            "Diretide - Red", "Diretide - Yellow", "Nemestice", "The International 11",
            "The International 10", "The International 9"
        };

        public static List<string> GetRadiantCreepOptions() => new()
        {
            "Default Radiant Creep", "Cavernite", "Crownfall", "Nemestice",
            "Reptilian Refuge", "Woodland Warbands"
        };

        public static List<string> GetDireCreepOptions() => new()
        {
            "Default Dire Creep", "Cavernite", "Crownfall", "Nemestice",
            "Reptilian Refuge", "Woodland Warbands"
        };

        public static List<string> GetRadiantSiegeOptions() => new()
        {
            "Default Radiant Siege", "Crownfall", "Woodland Warbands"
        };

        public static List<string> GetDireSiegeOptions() => new()
        {
            "Default Dire Siege", "Crownfall", "Woodland Warbands"
        };

        public static List<string> GetHudOptions() => new()
        {
            "Default HUD", "Direstone", "Portal", "Radiant Ore", "Triumph", "Valor"
        };

        public static List<string> GetVersusOptions() => new()
        {
            "Default Versus Screen", "The International 2019 - 1", "The International 2019 - 2",
            "The International 2020", "Battlepass 2022 - Diretide", "Battlepass 2022 - Nemestice",
            "The International 2024", "Winter Versus Screen"
        };

        public static List<string> GetRiverOptions() => new()
        {
            "Default Vial", "Blood Vial", "Chrome Vial", "Dry Vial", "Electrifield Vial", "Oil Vial"
        };

        public static List<string> GetRadiantTowerOptions() => new()
        {
            "Default Radiant Tower", "Declaration of the Divine Light", "Grasp of the Elder Gods",
            "Guardians of the Lost Path", "Stoneclaw Scavengers", "The Eyes of Avilliva"
        };

        public static List<string> GetDireTowerOptions() => new()
        {
            "Default Dire Tower", "Declaration of the Divine Shadow", "Grasp of the Elder Gods",
            "Guardians of the Lost Path", "Stoneclaw Scavengers", "The Gaze of Scree'Auk"
        };

        public static List<string> GetEffectOptions() => new()
        {
            "Disable Effect", "Aghanim", "Nemestice", "Quarteros Curios",
            "TI 2016", "TI 2017", "TI 2018", "TI 2019", "TI 2021", "TI 2022"
        };
    }
}

