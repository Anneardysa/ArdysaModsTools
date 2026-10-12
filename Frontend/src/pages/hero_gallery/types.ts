export type SetNotice = { type: string; anim?: string | null; text?: string | null };

export type SetEntry = {
   name: string;
   category?: "legacyset" | "customset" | "persona" | "item" | "basehero" | "prismatic" | "abilityeffect";
   isCustom?: boolean;
   thumbnailUrl?: string | null;
   tag?: string | null;
   abilityIconUrl?: string | null;
   displayName?: string | null;
   conflicts?: number[];
   carriedBy?: number[] | null;
   notices?: SetNotice[];
   locks?: string[] | null;
   styleGroup?: string | null;
   styleLabel?: string | null;
   styleGroupThumbnail?: string | null;
};

export type Hero = {
   id: string;
   name: string;
   displayName?: string;
   attribute?: string;
   thumbnail: string;
   sets: SetEntry[];
   abilityNames?: Record<string, string> | null;
   abilityIcons?: Record<string, string> | null;
   particleHosts?: ParticleHost[] | null;
};

export type ParticleHost = { id: string; label: string };

export type ParticleEntry = {
   id: string;
   name: string;
   source: "unusual" | "courier";
   fit?: "model" | "ground" | "hang";
   courier?: boolean;
   pools?: string | null;
   thumbnail?: string | null;
};

export type ParticleSlots = { slots: { id: string; label: string; hasModel: boolean }[]; preferred: string | null };

export type ParticlePick = { id: string; host: string };

export type HeroSelectionState = {
   set: number | null;
   items: number[];
   base: number | null;
   prismatic: number | null;
   effects?: number[];
   particles?: ParticlePick[];
};

export type Selections = Record<string, HeroSelectionState>;

export type LatestUpdate = {
   heroId: string;
   heroName: string;
   setIndex: number;
   setThumbnail?: string | null;
   daysAgo: number;
};

export type FilterCategory = "all" | "str" | "agi" | "int" | "universal" | "favorites";

export type TileType = "set" | "persona" | "item" | "base" | "prismatic" | "effect";

export type VisualTile = { idx: number; tileType: TileType };

export type AlertType = "success" | "warning" | "info";

export type ConfirmItem = { heroName: string; setName: string; thumbnail: string };

export type CooldownState = {
   active: boolean;
   remainingSeconds: number;
   totalSeconds: number;
   dailyUsed: number;
   dailyMax: number;
   isDailyLimit: boolean;
};

export type DetectionState = { phase: "scanning" } | { phase: "done"; found: boolean; count: number } | { phase: "failed" };
