import type { Hero, HeroSelectionState, SetEntry, Selections, FilterCategory } from "./types";

export const EMPTY_SELECTION: HeroSelectionState = { set: null, items: [], base: null, prismatic: null };

export function getHeroSelection(selections: Selections, heroId: string): HeroSelectionState {
   return selections[heroId] ?? EMPTY_SELECTION;
}

export function hasAnySelection(sel: HeroSelectionState | undefined): boolean {
   if (!sel) return false;
   return sel.set !== null || sel.items.length > 0 || sel.base !== null || sel.prismatic != null;
}

export function getSetCategory(set: SetEntry | undefined | null): string {
   return set?.category || "legacyset";
}

export function extractItemTag(set: SetEntry | undefined | null): string | null {
   return set?.tag ?? null;
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
      const setName = setObj.styleGroup ? `${setObj.styleGroup} · ${setObj.styleLabel}` : setObj.name;
      parts.push(isPersona ? `Persona: ${setName}` : setName);
   }
   if (sel.items.length > 0) {
      parts.push(`${sel.items.length} item${sel.items.length > 1 ? "s" : ""}`);
   }
   if (sel.base !== null) parts.push("Base");
   const prismObj = sel.prismatic != null ? hero.sets[sel.prismatic] : undefined;
   if (prismObj) {
      const prismName = prismObj.styleGroup ? `${prismObj.styleGroup} · ${prismObj.styleLabel}` : prismObj.name;
      parts.push(`Prismatic: ${prismName}`);
   }
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
         return null;
      default:
         return "Set";
   }
}

export function getActiveStyleIndex(groupIndices: number[], sel: HeroSelectionState, tileType: "set" | "persona" | "item" | "base" | "prismatic"): number | null {
   for (const idx of groupIndices) {
      if (tileType === "item") {
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
