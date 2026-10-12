const ASSETS = "https://amt-assets/rank_tier_icons";

export type Medal = {
   id: number;
   tier: number;
   lb: number;
   key: string;
   name: string;
   art: string;
   from: number;
   stars: readonly number[];
};

export const MEDALS = [
   { id: 0, tier: 0, lb: 0, key: "rank.tier.uncalibrated", name: "Uncalibrated", art: "rank0", from: 0, stars: [] },
   { id: 1, tier: 1, lb: 0, key: "rank.tier.herald", name: "Herald", art: "rank1", from: 0, stars: [0, 150, 300, 460, 610] },
   { id: 2, tier: 2, lb: 0, key: "rank.tier.guardian", name: "Guardian", art: "rank2", from: 770, stars: [770, 920, 1080, 1230, 1400] },
   { id: 3, tier: 3, lb: 0, key: "rank.tier.crusader", name: "Crusader", art: "rank3", from: 1540, stars: [1540, 1700, 1850, 2000, 2150] },
   { id: 4, tier: 4, lb: 0, key: "rank.tier.archon", name: "Archon", art: "rank4", from: 2310, stars: [2310, 2450, 2610, 2770, 2930] },
   { id: 5, tier: 5, lb: 0, key: "rank.tier.legend", name: "Legend", art: "rank5", from: 3080, stars: [3080, 3230, 3390, 3540, 3700] },
   { id: 6, tier: 6, lb: 0, key: "rank.tier.ancient", name: "Ancient", art: "rank6", from: 3850, stars: [3850, 4000, 4150, 4300, 4460] },
   { id: 7, tier: 7, lb: 0, key: "rank.tier.divine", name: "Divine", art: "rank7", from: 4620, stars: [4620, 4820, 5020, 5220, 5420] },
   { id: 8, tier: 8, lb: 0, key: "rank.tier.immortal", name: "Immortal", art: "rank8", from: 5620, stars: [] },
   { id: 9, tier: 8, lb: 1, key: "rank.tier.immortal1000", name: "Immortal Top 1000", art: "rank8a", from: 8620, stars: [] },
   { id: 10, tier: 8, lb: 2, key: "rank.tier.immortal100", name: "Immortal Top 100", art: "rank8b", from: 10620, stars: [] },
   { id: 11, tier: 8, lb: 3, key: "rank.tier.immortal10", name: "Immortal Top 10", art: "rank8c", from: 12620, stars: [] },
] as const satisfies readonly Medal[];

export const STAR_COUNTS = [1, 2, 3, 4, 5] as const;

export const MMR_MAX = 13000;

export const LB_RANK_MAX: Record<number, number> = { 1: 5000, 2: 100, 3: 10 };

export const clampLbRank = (rank: number, lb: number): number => {
   const max = LB_RANK_MAX[lb];
   return max ? Math.min(Math.max(rank, 1), max) : rank;
};

export const tierHasStars = (tier: number) => tier > 0 && tier < 8;

export const medalSrc = (medal: Medal) => `${ASSETS}/${medal.art}.png`;
export const pipSrc = (stars: number) => `${ASSETS}/pip${stars}.png`;

export const medalOf = (tier: number, lb = 0): Medal =>
   MEDALS.find((m) => m.tier === tier && (tier !== 8 || m.lb === (lb || 0))) ?? MEDALS[4];

export const mmrOf = (medal: Medal, stars: number): number =>
   medal.stars[Math.min(Math.max(stars, 1), 5) - 1] ?? medal.from;

export function medalForMmr(mmr: number): { medal: Medal; stars: number } {
   let medal: Medal = MEDALS[1];
   for (const m of MEDALS) if (m.id > 0 && mmr >= m.from) medal = m;

   let stars = 1;
   medal.stars.forEach((at, i) => {
      if (mmr >= at) stars = i + 1;
   });
   return { medal, stars };
}
