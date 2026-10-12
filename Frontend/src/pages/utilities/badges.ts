const ASSETS = "https://amt-assets/hero_badges";

export const BADGE_TIERS = [
   { tier: 0, key: "badge.tier.bronze", name: "Bronze", from: 1 },
   { tier: 1, key: "badge.tier.silver", name: "Silver", from: 6 },
   { tier: 2, key: "badge.tier.gold", name: "Gold", from: 12 },
   { tier: 3, key: "badge.tier.platinum", name: "Platinum", from: 18 },
   { tier: 4, key: "badge.tier.master", name: "Master", from: 25 },
   { tier: 5, key: "badge.tier.grandmaster", name: "Grandmaster", from: 30 },
] as const;

export type BadgeTier = (typeof BADGE_TIERS)[number];

export const LEVEL_MIN = 1;
export const LEVEL_MAX = 30;

export const LEVEL_XP = [50, 300, 400, 500, 600, 900, 1000, 1100, 1200, 1300, 1400, 1700, 1800, 1900, 2000, 2100, 2200, 2500, 2600, 2700, 2800, 2900, 3000, 3100, 6800, 4000, 4200, 4400, 4600, 8000] as const;

export const tierTo = (tier: number): number => (tier < BADGE_TIERS.length - 1 ? BADGE_TIERS[tier + 1]!.from - 1 : LEVEL_MAX);

export const tierForLevel = (level: number): number => {
   let tier = 0;
   for (const b of BADGE_TIERS) if (level >= b.from) tier = b.tier;
   return tier;
};

export const xpNeed = (level: number): number => LEVEL_XP[Math.min(level, LEVEL_MAX - 1)]!;

export const BAR_COLORS: Record<number, readonly [string, string]> = {
   [-1]: ["#ae9249", "#e0c59a"],
   0: ["#97472A", "#CF9F68"],
   1: ["#59748A", "#C8E9FF"],
   2: ["#ae9249", "#e0c59a"],
   3: ["#4867C5", "#85D4EF"],
   4: ["#48165E", "#918BD0"],
   5: ["#4b141d", "#fe8650"],
};

export const badgeSrc = (tier: number) => `${ASSETS}/badge${tier}.png`;

export const tierOf = (tier: number): BadgeTier => BADGE_TIERS.find((b) => b.tier === tier) ?? BADGE_TIERS[5];
