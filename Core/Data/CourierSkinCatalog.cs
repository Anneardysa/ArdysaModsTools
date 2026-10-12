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
using System.IO;
using System.Linq;

namespace ArdysaModsTools.Core.Data
{
    public record CourierMaterialSwap
    {
        public string SourceMaterial { get; init; } = string.Empty;

        public string TargetMaterial { get; init; } = string.Empty;
    }

    public static class CourierSkinCatalog
    {
        private static readonly Dictionary<string, CourierMaterialSwap[]> Catalog = new(StringComparer.OrdinalIgnoreCase)
        {
            ["10096"] = new[]
            {
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/baby_rosh/babyrosh_gold_color.vmat_c",
                    TargetMaterial = "materials/models/courier/baby_rosh/babyrosh_color.vmat_c"
                }
            },

            ["10374"] = new[]
            {
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/baby_rosh/babyrosh_platinum_color.vmat_c",
                    TargetMaterial = "materials/models/courier/baby_rosh/babyrosh_color.vmat_c"
                }
            },

            ["10479"] = new[]
            {
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/baby_rosh/babyrosh_ice_color.vmat_c",
                    TargetMaterial = "materials/models/courier/baby_rosh/babyrosh_color.vmat_c"
                }
            },

            ["10478"] = new[]
            {
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/baby_rosh/babyrosh_lava_color.vmat_c",
                    TargetMaterial = "materials/models/courier/baby_rosh/babyrosh_color.vmat_c"
                }
            },

            ["11647"] = new[]
            {
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/baby_rosh/babyrosh_darkmoon_color.vmat_c",
                    TargetMaterial = "materials/models/courier/baby_rosh/babyrosh_color.vmat_c"
                }
            },

            ["12007"] = new[]
            {
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/baby_rosh/babyrosh_desert_sands_color.vmat_c",
                    TargetMaterial = "materials/models/courier/baby_rosh/babyrosh_color.vmat_c"
                }
            },

            ["12202"] = new[]
            {
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/baby_rosh/babyrosh_ti8_color.vmat_c",
                    TargetMaterial = "materials/models/courier/baby_rosh/babyrosh_color.vmat_c"
                }
            },

            ["13475"] = new[]
            {
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/baby_rosh/babyroshan_alt_default/babyroshan_alt_gold.vmat_c",
                    TargetMaterial = "materials/models/courier/baby_rosh/babyroshan_alt_default/babyroshan_alt_default.vmat_c"
                }
            },

            ["13481"] = new[]
            {
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/baby_rosh/babyroshan_alt_default/babyroshan_alt_silver.vmat_c",
                    TargetMaterial = "materials/models/courier/baby_rosh/babyroshan_alt_default/babyroshan_alt_default.vmat_c"
                }
            },

            ["10702"] = new[]
            {
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/doom_demihero_courier/doom_demihero_courier_gold.vmat_c",
                    TargetMaterial = "materials/models/courier/doom_demihero_courier/doom_demihero_courier.vmat_c"
                }
            },

            ["10993"] = new[]
            {
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/flopjaw/golden_flopjaw.vmat_c",
                    TargetMaterial = "materials/models/courier/flopjaw/flopjaw.vmat_c"
                }
            },

            ["11092"] = new[]
            {
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/venoling/venoling_gold_color.vmat_c",
                    TargetMaterial = "materials/models/courier/venoling/venoling_color.vmat_c"
                }
            },

            ["11522"] = new[]
            {
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/huntling/huntling_golden.vmat_c",
                    TargetMaterial = "materials/models/courier/huntling/huntling.vmat_c"
                }
            },

            ["11643"] = new[]
            {
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/beetlejaws/beetlejaws_golden.vmat_c",
                    TargetMaterial = "materials/models/courier/beetlejaws/beetlejaws.vmat_c"
                }
            },

            ["11740"] = new[]
            {
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/seekling_golden/seekling_golden.vmat_c",
                    TargetMaterial = "materials/models/courier/seekling/seekling.vmat_c"
                },
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/seekling_golden/seekling_golden_flying.vmat_c",
                    TargetMaterial = "materials/models/courier/seekling/seekling_flying.vmat_c"
                }
            },

            ["11764"] = new[]
            {
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/items/courier/golden_devourling/golden_devourling.vmat_c",
                    TargetMaterial = "materials/models/items/courier/devourling/devourling.vmat_c"
                },
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/items/courier/golden_devourling/golden_devourling_flying.vmat_c",
                    TargetMaterial = "materials/models/items/courier/devourling/devourling_flying.vmat_c"
                }
            },

            ["11127"] = new[]
            {
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/ram/ram_gold.vmat_c",
                    TargetMaterial = "materials/models/courier/ram/ram.vmat_c"
                }
            },

            ["11126"] = new[]
            {
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/ram/ram_jade.vmat_c",
                    TargetMaterial = "materials/models/courier/ram/ram.vmat_c"
                }
            },

            ["10432"] = new[]
            {
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/godhorse/jadehoof.vmat_c",
                    TargetMaterial = "materials/models/courier/godhorse/redhoof.vmat_c"
                }
            },

            ["10019"] = new[]
            {
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/donkey_zebra/donkey_zebra_base_color.vmat_c",
                    TargetMaterial = "materials/models/props_gameplay/donkey_base.vmat_c"
                },
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/donkey_zebra/donkey_zebra_backpack_color.vmat_c",
                    TargetMaterial = "materials/models/props_gameplay/donkey_backpack.vmat_c"
                }
            },

            ["10033"] = new[]
            {
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/juggernaut_dog/juggernaut_dog_defense.vmat_c",
                    TargetMaterial = "materials/models/courier/juggernaut_dog/juggernaut_dog.vmat_c"
                }
            },

            ["23755"] = new[]
            {
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/f2p_courier/f2p_courier_10th_anniversary_color.vmat_c",
                    TargetMaterial = "materials/models/courier/f2p_courier/f2p_courier_color.vmat_c"
                }
            },

            ["10194:1"] = new[]
            {
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/items/courier/el_gato_beyond_the_summit/elgato_silver_owl.vmat_c",
                    TargetMaterial = "materials/models/items/courier/el_gato_beyond_the_summit/el_gato_beyond_the_summit.vmat_c"
                }
            },

            ["10450:1"] = new[]
            {
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/skippy_parrot/skippy_parrot_green_color.vmat_c",
                    TargetMaterial = "materials/models/courier/skippy_parrot/skippy_parrot_color.vmat_c"
                }
            },

            ["10450:2"] = new[]
            {
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/skippy_parrot/skippy_parrot_red_color.vmat_c",
                    TargetMaterial = "materials/models/courier/skippy_parrot/skippy_parrot_color.vmat_c"
                }
            },

            ["10174:1"] = new[]
            {
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/smeevil/smeevil_red.vmat_c",
                    TargetMaterial = "materials/models/courier/smeevil/smeevil.vmat_c"
                },
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/smeevil_crab/smeevil_crab_red.vmat_c",
                    TargetMaterial = "materials/models/courier/smeevil_crab/smeevil_crab.vmat_c"
                }
            },

            ["10174:2"] = new[]
            {
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/smeevil/smeevil_yellow.vmat_c",
                    TargetMaterial = "materials/models/courier/smeevil/smeevil.vmat_c"
                }
            },

            ["10174:3"] = new[]
            {
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/smeevil/smeevil_purple.vmat_c",
                    TargetMaterial = "materials/models/courier/smeevil/smeevil.vmat_c"
                }
            },

            ["10174:7"] = new[]
            {
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/smeevil/smeevil_red.vmat_c",
                    TargetMaterial = "materials/models/courier/smeevil/smeevil.vmat_c"
                },
                new CourierMaterialSwap
                {
                    SourceMaterial = "materials/models/courier/smeevil_crab/smeevil_crab_red.vmat_c",
                    TargetMaterial = "materials/models/courier/smeevil_crab/smeevil_crab.vmat_c"
                }
            }
        };

        public static IReadOnlyList<CourierMaterialSwap> GetMaterialSwaps(string courierId, int? styleIndex = null)
        {
            if (string.IsNullOrWhiteSpace(courierId))
                return Array.Empty<CourierMaterialSwap>();

            if (styleIndex.HasValue)
            {
                string styleKey = $"{courierId}:{styleIndex.Value}";
                if (Catalog.TryGetValue(styleKey, out var styleSwaps))
                    return styleSwaps;
            }

            if (Catalog.TryGetValue(courierId, out var baseSwaps))
                return baseSwaps;

            return Array.Empty<CourierMaterialSwap>();
        }

        public static List<string> GetMaterialExtractionDirs(IEnumerable<CourierMaterialSwap> swaps)
        {
            var dirs = new List<string>();
            if (swaps == null) return dirs;

            foreach (var swap in swaps)
            {
                string path = swap.SourceMaterial.Replace('\\', '/').TrimStart('/');
                int lastSlash = path.LastIndexOf('/');
                if (lastSlash > 0)
                {
                    string dir = path.Substring(0, lastSlash);
                    if (!dirs.Contains(dir, StringComparer.OrdinalIgnoreCase))
                        dirs.Add(dir);
                }
            }

            return dirs;
        }
    }
}
