import { translate } from "../../bridge/i18n";
import type { Hero, HeroSelectionState, ParticleEntry, ParticlePick, SetEntry, SetNotice, Selections, FilterCategory, TileType } from "./types";

const NOTICE_CONFLICT_NAMES = 3;

export function getSetNoticeLines(hero: Hero, indices: number[]): string[] {
   const lines = new Set<string>();
   const conflicts = new Set<number>();
   for (const idx of indices) {
      const set = hero.sets[idx];
      if (!set) continue;
      for (const n of set.notices ?? []) {
         const line = noticeLine(n);
         if (line) lines.add(line);
      }
      for (const c of set.conflicts ?? []) if (!indices.includes(c)) conflicts.add(c);
      if (getSetCategory(set) === "basehero" && set.locks?.length) {
         const slots = set.locks.map((s) => slotLabel(normalizeSlot(s))).join(", ");
         lines.add(translate("hero.notice.baseLocks",
            `${slots} always comes from this arcana (model, effects, particles, animations). A set's piece and items in that slot are replaced.`,
            { slots }));
      }
   }
   if (conflicts.size > 0) {
      const all = [...conflicts].map((i) => getSetDisplayName(hero.sets[i], i));
      const names = all.slice(0, NOTICE_CONFLICT_NAMES).join(", ") + (all.length > NOTICE_CONFLICT_NAMES ? ` +${all.length - NOTICE_CONFLICT_NAMES}` : "");
      lines.add(translate("hero.notice.noCombine", `Cannot be combined with ${names}.`, { names }));
   }
   return [...lines];
}

function noticeLine(n: SetNotice): string | null {
   switch (n?.type) {
      case "anim_demo_only":
         return translate("hero.notice.animDemoOnly", "Animation is bugged in Demo Mode only; public matches are fine.");
      case "anim_bug":
         return n.anim
            ? translate("hero.notice.animBug", `${n.anim} animation is bugged.`, { anim: n.anim })
            : translate("hero.notice.animBugGeneric", "An animation on this item is bugged.");
      case "custom":
         return n.text?.trim() || null;
      default:
         return null;
   }
}

export const EMPTY_SELECTION: HeroSelectionState = { set: null, items: [], base: null, prismatic: null, effects: [], particles: [] };

export const MAX_PARTICLES = 2;

export function particleCarriers(hero: Hero, sel: HeroSelectionState | undefined): { idx: number; kind: "set" | "item" | "base" }[] {
   if (!sel || isPersonaActive(hero, sel)) return [];
   const out: { idx: number; kind: "set" | "item" | "base" }[] = [];
   const set = sel.set != null ? hero.sets[sel.set] : undefined;
   if (set && !isDefaultSetEntry(set) && ["legacyset", "customset"].includes(getSetCategory(set))) out.push({ idx: sel.set!, kind: "set" });
   for (const i of sel.items) if (getSetCategory(hero.sets[i]) === "item") out.push({ idx: i, kind: "item" });
   if (sel.base != null && getSetCategory(hero.sets[sel.base]) === "basehero") out.push({ idx: sel.base, kind: "base" });
   return out;
}

export function canCarryParticles(hero: Hero, sel: HeroSelectionState | undefined): boolean {
   return particleCarriers(hero, sel).length > 0;
}

export function findParticle(catalog: ParticleEntry[], id: string | null | undefined): ParticleEntry | undefined {
   const key = (id ?? "").trim().toLowerCase();
   return key ? catalog.find((p) => p.id.toLowerCase() === key) : undefined;
}

export function sanitizeParticles(
   hero: Hero,
   sel: HeroSelectionState,
   catalog: ParticleEntry[],
): { particles: ParticlePick[]; dropped: string[] } {
   const picks = sel.particles ?? [];
   const nameOf = (p: ParticlePick | null | undefined) => findParticle(catalog, p?.id)?.name ?? `particle ${p?.id ?? "?"}`;
   if (picks.length === 0) return { particles: [], dropped: [] };
   if (!canCarryParticles(hero, sel)) return { particles: [], dropped: picks.map(nameOf) };

   const hosts = new Set((hero.particleHosts ?? []).map((h) => h.id));
   const particles: ParticlePick[] = [];
   const dropped: string[] = [];
   for (const pick of picks) {
      const entry = findParticle(catalog, pick?.id);
      const host = (pick?.host ?? "").trim();
      if (particles.length >= MAX_PARTICLES || !entry || !hosts.has(host)
         || particles.some((k) => k.id === entry.id && k.host === host)) {
         dropped.push(nameOf(pick));
         continue;
      }
      particles.push({ id: entry.id, host });
   }
   return { particles, dropped };
}

export function getHeroSelection(selections: Selections, heroId: string): HeroSelectionState {
   return selections[heroId] ?? EMPTY_SELECTION;
}

export function hasAnySelection(sel: HeroSelectionState | undefined): boolean {
   if (!sel) return false;
   return sel.set !== null || sel.items.length > 0 || sel.base !== null || sel.prismatic != null
      || (sel.effects?.length ?? 0) > 0;
}

export function getSetCategory(set: SetEntry | undefined | null): string {
   return set?.category || "legacyset";
}

export function extractItemTag(set: SetEntry | undefined | null): string | null {
   return set?.tag ?? null;
}

export function getSetDisplayName(set: SetEntry | undefined | null, fallbackIndex?: number): string {
   if (!set) return fallbackIndex != null ? `Set ${fallbackIndex + 1}` : "";
   if (set.displayName) return set.displayName;
   if (set.styleGroup && set.styleLabel) return `${set.styleGroup} · ${set.styleLabel}`;
   return set.name || (fallbackIndex != null ? `Set ${fallbackIndex + 1}` : "");
}

export function isPersonaActive(hero: Hero | undefined, sel: HeroSelectionState | undefined): boolean {
   if (!hero || !sel || sel.set === null) return false;
   return getSetCategory(hero.sets[sel.set]) === "persona";
}

export function isBaseActive(sel: HeroSelectionState | undefined): boolean {
   return sel?.base != null;
}

export function getSelectionSummary(hero: Hero, sel: HeroSelectionState | undefined): string | null {
   if (!sel) return null;
   const parts: string[] = [];
   const setObj = sel.set !== null ? hero.sets[sel.set] : undefined;
   if (setObj) {
      const isPersona = getSetCategory(setObj) === "persona";
      const setName = getSetDisplayName(setObj, sel.set ?? undefined);
      parts.push(isPersona ? `Persona: ${setName}` : setName);
   }
   if (sel.items.length > 0) {
      parts.push(`${sel.items.length} item${sel.items.length > 1 ? "s" : ""}`);
   }
   if (sel.base !== null) parts.push("Base");
   const prismObj = sel.prismatic != null ? hero.sets[sel.prismatic] : undefined;
   if (prismObj) {
      const prismName = getSetDisplayName(prismObj, sel.prismatic ?? undefined);
      parts.push(`Prismatic: ${prismName}`);
   }
   const effects = sel.effects?.length ?? 0;
   if (effects > 0) parts.push(`${effects} effect${effects > 1 ? "s" : ""}`);
   const particles = sel.particles?.length ?? 0;
   if (particles > 0) parts.push(`${particles} particle${particles > 1 ? "s" : ""}`);
   return parts.length > 0 ? parts.join(" + ") : null;
}

export function getItemsWithSameTag(hero: Hero, targetIdx: number): number[] {
   const targetTag = extractItemTag(hero.sets[targetIdx]);
   if (!targetTag) return [];
   const sameTag: number[] = [];
   hero.sets.forEach((set, idx) => {
      if (idx !== targetIdx && getSetCategory(set) === "item" && extractItemTag(set) === targetTag) {
         sameTag.push(idx);
      }
   });
   return sameTag;
}

export function getEffectsWithSameTag(hero: Hero, targetIdx: number): number[] {
   const targetTag = extractItemTag(hero.sets[targetIdx]);
   if (!targetTag) return [];
   const sameTag: number[] = [];
   hero.sets.forEach((set, idx) => {
      if (idx !== targetIdx && getSetCategory(set) === "abilityeffect" && extractItemTag(set) === targetTag) {
         sameTag.push(idx);
      }
   });
   return sameTag;
}

export function abilityRank(tag: string): number {
   const m = /^ability([1-9])$/.exec(tag);
   return m ? Number(m[1]) : 99;
}

export function abilityLabel(hero: Hero, tag: string): string {
   const named = hero.abilityNames?.[tag];
   if (named) return named;
   const m = /^ability([1-9])$/.exec(tag);
   return m ? `Ability ${m[1]}` : tag;
}

const SLOT_ALIASES: Record<string, string> = {
   arm: "arms",
   offweapon: "offhand",
   offweap: "offhand",
   offhand_weapon: "offhand",
   hand: "hands",
   gloves: "hands",
   shoes: "legs",
};

const SLOT_ORDER = [
   "weapon", "offhand", "head", "shoulder", "back", "arms", "hands",
   "belt", "legs", "neck", "tail", "mount", "armor", "summon", "misc",
];

const SLOT_LABELS: Record<string, string> = {
   offhand: "Off-hand",
   arms: "Arms",
   hands: "Hands",
   misc: "Misc",
   body_head: "Body / Head",
};

export function normalizeSlot(tag: string | null | undefined): string {
   const raw = (tag || "").toLowerCase().trim();
   if (!raw) return "misc";
   return SLOT_ALIASES[raw] ?? raw;
}

export function slotRank(slot: string): number {
   const i = SLOT_ORDER.indexOf(slot);
   return i === -1 ? SLOT_ORDER.length : i;
}

export function slotLabel(slot: string): string {
   return SLOT_LABELS[slot] ?? slot.charAt(0).toUpperCase() + slot.slice(1);
}

export function matchesCategory(heroAttr: string | undefined, category: FilterCategory): boolean {
   if (category === "all") return true;
   if (category === "favorites") return false;
   const a = (heroAttr || "").toLowerCase();
   if (category === "str") return a === "str" || a === "strength";
   if (category === "agi") return a === "agi" || a === "agility";
   if (category === "int") return a === "int" || a === "intelligence";
   if (category === "universal") return a === "universal" || a === "all" || a === "";
   return true;
}

function isDefaultSetEntry(s: SetEntry): boolean {
   const name = (s.name || "").toLowerCase();
   return name === "default set" || name === "default";
}

export function customSetCount(hero: Hero): number {
   if (!hero.sets || hero.sets.length === 0) return 0;
   return hero.sets.filter((s) => !isDefaultSetEntry(s)).length;
}

export function heroHasCustomSets(hero: Hero): boolean {
   return customSetCount(hero) > 0;
}

export function filterHeroes(
   heroes: Hero[],
   opts: { filter: FilterCategory; search: string; onlyWithSets: boolean; favorites: Set<string> },
): Hero[] {
   const q = opts.search.trim().toLowerCase();
   return heroes.filter((hero) => {
      if (opts.filter === "favorites") {
         if (!opts.favorites.has(hero.id)) return false;
      } else if (opts.filter !== "all") {
         if (!matchesCategory(hero.attribute, opts.filter)) return false;
      }
      if (q && !hero.name?.toLowerCase().includes(q) && !hero.displayName?.toLowerCase().includes(q)) {
         return false;
      }
      if (opts.onlyWithSets && !heroHasCustomSets(hero)) return false;
      return true;
   });
}

export function getCategoryTag(set: SetEntry): string | null {
   switch (getSetCategory(set)) {
      case "customset":
         return "Mix";
      case "persona":
         return "Persona";
      case "basehero":
         return "Arcana";
      case "prismatic":
         return "Prismatic";
      case "item":
      case "abilityeffect":
         return null;
      default:
         return "Set";
   }
}

export function getSelectedIndices(sel: HeroSelectionState | undefined): number[] {
   if (!sel) return [];
   const out: number[] = [];
   if (sel.set !== null) out.push(sel.set);
   out.push(...sel.items);
   if (sel.base !== null) out.push(sel.base);
   if (sel.prismatic != null) out.push(sel.prismatic);
   out.push(...(sel.effects ?? []));
   return out;
}

export function getBlockedIndices(hero: Hero, sel: HeroSelectionState | undefined): Set<number> {
   const blocked = new Set<number>();
   if (!sel) return blocked;

   const selected = getSelectedIndices(sel);
   if (selected.length === 0) return blocked;

   for (const idx of selected) {
      const conflicts = hero.sets[idx]?.conflicts;
      if (conflicts) for (const other of conflicts) blocked.add(other);
   }
   hero.sets.forEach((_, idx) => {
      if (isLockedByBase(hero, sel, idx) || isCarriedByPickedItem(hero, sel, idx)) blocked.add(idx);
   });
   for (const idx of selected) blocked.delete(idx);
   return blocked;
}

export function carrierOf(hero: Hero, sel: HeroSelectionState | undefined, idx: number): number | null {
   const set = hero.sets[idx];
   if (!sel || !set || getSetCategory(set) !== "abilityeffect") return null;
   return (set.carriedBy ?? []).find((i) => sel.items.includes(i)) ?? null;
}

export function isCarriedByPickedItem(hero: Hero, sel: HeroSelectionState | undefined, idx: number): boolean {
   return carrierOf(hero, sel, idx) != null;
}

export function getLockedSlots(hero: Hero, baseIdx: number | null | undefined): Set<string> {
   const set = baseIdx != null ? hero.sets[baseIdx] : undefined;
   if (!set || getSetCategory(set) !== "basehero") return new Set();
   return new Set((set.locks ?? []).map(normalizeSlot));
}

export function isLockedByBase(hero: Hero, sel: HeroSelectionState | undefined, idx: number): boolean {
   const set = hero.sets[idx];
   if (!sel || sel.base == null || !set || getSetCategory(set) !== "item" || !set.tag) return false;
   const tag = normalizeSlot(set.tag);
   for (const slot of getLockedSlots(hero, sel.base)) if (slotCovers(slot, tag)) return true;
   return false;
}

export function slotCovers(slot: string, tag: string): boolean {
   return slot === tag || slot.split("_").map(normalizeSlot).includes(tag);
}

export function sanitizeSelection(
   hero: Hero,
   sel: HeroSelectionState,
   catalog: ParticleEntry[] = [],
): { sel: HeroSelectionState; dropped: string[] } {
   const dropped: string[] = [];
   const kept: number[] = [];
   const next: HeroSelectionState = {
      ...sel,
      items: [...(sel.items ?? [])],
      effects: [...(sel.effects ?? [])],
      particles: [...(sel.particles ?? [])],
   };

   const isEffect = (idx: number | null | undefined): boolean =>
      idx != null && getSetCategory(hero.sets[idx]) === "abilityeffect";
   for (const slot of ["set", "base", "prismatic"] as const) {
      if (isEffect(next[slot])) {
         dropped.push(getSetDisplayName(hero.sets[next[slot]!], next[slot]!));
         next[slot] = null;
      }
   }
   next.items = next.items.filter((idx) => {
      if (!isEffect(idx)) return true;
      dropped.push(getSetDisplayName(hero.sets[idx], idx));
      return false;
   });

   if (isPersonaActive(hero, next)) {
      for (const idx of [...next.items, next.base, next.prismatic, ...(next.effects ?? [])]) {
         if (idx != null) dropped.push(getSetDisplayName(hero.sets[idx], idx));
      }
      next.items = [];
      next.base = null;
      next.prismatic = null;
      next.effects = [];
   }

   const conflictsWithKept = (idx: number): boolean =>
      kept.some((k) => hero.sets[k]?.conflicts?.includes(idx) || hero.sets[idx]?.conflicts?.includes(k));

   const consider = (idx: number | null, drop: () => void): void => {
      if (idx === null || idx === undefined) return;
      if (conflictsWithKept(idx)) {
         dropped.push(getSetDisplayName(hero.sets[idx], idx));
         drop();
      } else {
         kept.push(idx);
      }
   };

   consider(next.set, () => { next.set = null; });
   next.items = next.items.filter((idx) => {
      let keep = true;
      consider(idx, () => { keep = false; });
      return keep;
   });
   consider(next.base, () => { next.base = null; next.prismatic = null; });
   consider(next.prismatic, () => { next.prismatic = null; });
   if (next.base == null && next.prismatic != null) {
      dropped.push(getSetDisplayName(hero.sets[next.prismatic], next.prismatic));
      next.prismatic = null;
   }

   next.items = next.items.filter((idx) => {
      if (!isLockedByBase(hero, next, idx)) return true;
      dropped.push(getSetDisplayName(hero.sets[idx], idx));
      return false;
   });

   const effectTags = new Set<string>();
   next.effects = (next.effects ?? []).filter((idx) => {
      const set = hero.sets[idx];
      const tag = extractItemTag(set);
      if (!set || getSetCategory(set) !== "abilityeffect" || !tag) {
         dropped.push(set ? getSetDisplayName(set, idx) : `#${idx}`);
         return false;
      }
      if (effectTags.has(tag) || isCarriedByPickedItem(hero, next, idx)) {
         dropped.push(getSetDisplayName(set, idx));
         return false;
      }
      let keep = true;
      consider(idx, () => { keep = false; });
      if (keep) effectTags.add(tag);
      return keep;
   });

   const particles = sanitizeParticles(hero, next, catalog);
   next.particles = particles.particles;
   dropped.push(...particles.dropped);

   return { sel: next, dropped };
}

export function getActiveStyleIndex(groupIndices: number[], sel: HeroSelectionState, tileType: TileType): number | null {
   for (const idx of groupIndices) {
      if (tileType === "effect") {
         if (sel.effects?.includes(idx)) return idx;
      } else if (tileType === "item") {
         if (sel.items.includes(idx)) return idx;
      } else if (tileType === "base") {
         if (sel.base === idx) return idx;
      } else if (tileType === "prismatic") {
         if (sel.prismatic === idx) return idx;
      } else if (sel.set === idx) {
         return idx;
      }
   }
   return null;
}
