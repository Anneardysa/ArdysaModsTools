import { send } from "../../bridge/host";
import { createStore } from "../../bridge/store";
import {
   EMPTY_SELECTION,
   filterHeroes,
   getHeroSelection,
   getItemsWithSameTag,
   getSelectionSummary,
   getSetCategory,
   hasAnySelection,
   heroHasCustomSets,
   isBaseActive,
   isPersonaActive,
} from "./helpers";
import type { AlertType, ConfirmItem, CooldownState, FilterCategory, Hero, HeroSelectionState, LatestUpdate, Selections, TileType } from "./types";

export type ConfirmState =
   | { visible: false }
   | { visible: true; kind: "clearAll"; count: number; items: ConfirmItem[] }
   | { visible: true; kind: "baseNoSet"; title: string; message: string };

export type StylePreviewState = {
   heroId: string;
   tileType: "set" | "persona" | "item" | "base" | "prismatic";
   groupIndices: number[];
   group: string;
   groupCover: string | null;
} | null;

export const store = createStore<{
   heroes: Hero[];
   selections: Selections;
   favorites: Set<string>;
   version: string;
   status: string;

   currentFilter: FilterCategory;
   searchQuery: string;
   showOnlyWithSets: boolean;

   latestUpdates: LatestUpdate[];
   updatesCollapsed: boolean;

   cachingVisible: boolean;
   cachingStatus: { current: number; total: number };

   modalHeroId: string | null;
   focusedSetIndex: number;
   highlightSetIndex: number | null;

   stylePreview: StylePreviewState;

   confirm: ConfirmState;

   alert: { visible: boolean; title: string; message: string; type: AlertType; hasLog: boolean };
   generationLogLines: string[];
   logModalOpen: boolean;

   cooldown: CooldownState;
}>({
   heroes: [],
   selections: {},
   favorites: new Set(),
   version: "",
   status: "",

   currentFilter: "all",
   searchQuery: "",
   showOnlyWithSets: false,

   latestUpdates: [],
   updatesCollapsed: false,

   cachingVisible: false,
   cachingStatus: { current: 0, total: 0 },

   modalHeroId: null,
   focusedSetIndex: 0,
   highlightSetIndex: null,

   stylePreview: null,

   confirm: { visible: false },

   alert: { visible: false, title: "", message: "", type: "info", hasLog: false },
   generationLogLines: [],
   logModalOpen: false,

   cooldown: { active: false, remainingSeconds: 0, totalSeconds: 600, dailyUsed: 0, dailyMax: 5, isDailyLimit: false },
});

function notifySelectionChanged() {
   send("selectionChanged", { selections: store.get().selections });
}

function withCleanup(selections: Selections, heroId: string): Selections {
   if (hasAnySelection(selections[heroId])) return selections;
   const { [heroId]: _drop, ...rest } = selections;
   return rest;
}

function updateSelection(heroId: string, mutate: (sel: HeroSelectionState) => HeroSelectionState) {
   const s = store.get();
   const current = getHeroSelection(s.selections, heroId);
   const next = mutate({ ...current, items: [...current.items] });
   const selections = withCleanup({ ...s.selections, [heroId]: next }, heroId);
   store.set({ selections });
   notifySelectionChanged();
}

export function selectSet(hero: Hero, setIndex: number) {
   updateSelection(hero.id, (sel) => {
      if (sel.set === setIndex) return { ...sel, set: null };
      const isPersona = getSetCategory(hero.sets[setIndex]) === "persona";
      return isPersona ? { set: setIndex, items: [], base: null, prismatic: null } : { ...sel, set: setIndex };
   });
   store.set({ focusedSetIndex: setIndex });
}

export function toggleItem(hero: Hero, setIndex: number) {
   const sel = getHeroSelection(store.get().selections, hero.id);
   if (isPersonaActive(hero, sel)) return;

   updateSelection(hero.id, (s) => {
      if (s.items.includes(setIndex)) {
         return { ...s, items: s.items.filter((i) => i !== setIndex) };
      }
      const conflicts = new Set(getItemsWithSameTag(hero, setIndex));
      return { ...s, items: [...s.items.filter((i) => !conflicts.has(i)), setIndex] };
   });
   store.set({ focusedSetIndex: setIndex });
}

export function toggleBase(hero: Hero, setIndex: number) {
   const sel = getHeroSelection(store.get().selections, hero.id);
   if (isPersonaActive(hero, sel)) return;

   let droppedPrismatic = false;
   updateSelection(hero.id, (s) => {
      if (s.base === setIndex) {
         droppedPrismatic = s.prismatic != null;
         return { ...s, base: null, prismatic: null };
      }
      return { ...s, base: setIndex };
   });
   store.set({ focusedSetIndex: setIndex, ...(droppedPrismatic ? { status: "Prismatic cleared — it requires an active Base Hero" } : {}) });
}

export function togglePrismatic(hero: Hero, setIndex: number) {
   const sel = getHeroSelection(store.get().selections, hero.id);
   if (sel.prismatic !== setIndex && !isBaseActive(sel)) {
      store.set({ status: "Select a Base Hero first to enable Prismatic" });
      return;
   }
   updateSelection(hero.id, (s) => ({ ...s, prismatic: s.prismatic === setIndex ? null : setIndex }));
   store.set({ focusedSetIndex: setIndex });
}

export function deselectHero(heroId: string) {
   const { [heroId]: _drop, ...rest } = store.get().selections;
   store.set({ selections: rest });
   notifySelectionChanged();
}

export function toggleFavorite(heroId: string) {
   const favorites = new Set(store.get().favorites);
   if (favorites.has(heroId)) favorites.delete(heroId);
   else favorites.add(heroId);
   store.set({ favorites });
   send("favoritesChanged", { favorites: Array.from(favorites) });
}

function doClearAllSelections() {
   store.set({ selections: {} });
   notifySelectionChanged();
   store.set({ status: "All selections cleared" });
}

export function applyLoadedSelections(selections: Selections) {
   store.set({ selections });
}

export function loadHighlightedHeroes(heroIds: string[]) {
   if (!Array.isArray(heroIds)) return;
   store.set((s) => {
      const selections = { ...s.selections };
      for (const id of heroIds) {
         if (!selections[id]) selections[id] = { ...EMPTY_SELECTION };
      }
      return { selections };
   });
}

function getFilteredHeroes(): Hero[] {
   const s = store.get();
   return filterHeroes(s.heroes, { filter: s.currentFilter, search: s.searchQuery, onlyWithSets: s.showOnlyWithSets, favorites: s.favorites });
}


export function openHeroModal(heroId: string) {
   const hero = store.get().heroes.find((h) => h.id === heroId);
   if (!hero || !heroHasCustomSets(hero)) {
      store.set({ status: `${hero?.displayName || heroId} has no sets available` });
      return;
   }
   store.set({ modalHeroId: heroId, focusedSetIndex: 0 });
}

export function closeModal() {
   store.set({ modalHeroId: null, stylePreview: null });
}

export function navigateHero(direction: 1 | -1) {
   const filtered = getFilteredHeroes();
   const currentIndex = filtered.findIndex((h) => h.id === store.get().modalHeroId);
   if (currentIndex === -1) return;
   let i = currentIndex + direction;
   while (i >= 0 && i < filtered.length) {
      const next = filtered[i]!;
      if (heroHasCustomSets(next)) {
         store.set({ modalHeroId: next.id, focusedSetIndex: 0 });
         return;
      }
      i += direction;
   }
}

export function navigateSet(direction: 1 | -1) {
   const s = store.get();
   const hero = s.heroes.find((h) => h.id === s.modalHeroId);
   if (!hero || hero.sets.length === 0) return;
   let idx = s.focusedSetIndex + direction;
   if (idx < 0) idx = hero.sets.length - 1;
   if (idx >= hero.sets.length) idx = 0;
   store.set({ focusedSetIndex: idx });
}

export function navigateToUpdate(heroId: string, setIndex: number) {
   const card = document.querySelector(`[data-hero-id="${CSS.escape(heroId)}"]`);
   card?.scrollIntoView({ behavior: "smooth", block: "center" });

   window.setTimeout(() => {
      openHeroModal(heroId);
      if (setIndex < 0) return;
      window.setTimeout(() => {
         store.set({ focusedSetIndex: setIndex, highlightSetIndex: setIndex });
         window.setTimeout(() => {
            if (store.get().highlightSetIndex === setIndex) store.set({ highlightSetIndex: null });
         }, 2000);
      }, 150);
   }, 350);
}


export function openStylePreview(hero: Hero, groupName: string, tileType: TileType) {
   const groupIndices: number[] = [];
   let groupCover: string | null = null;
   hero.sets.forEach((s, i) => {
      if (s.styleGroup === groupName) {
         groupIndices.push(i);
         if (!groupCover && s.styleGroupThumbnail) groupCover = s.styleGroupThumbnail;
      }
   });
   if (groupIndices.length === 0) return;
   store.set({ stylePreview: { heroId: hero.id, tileType, groupIndices, group: groupName, groupCover } });
}

export function closeStylePreview() {
   store.set({ stylePreview: null });
}

export function selectStyleFromPreview(hero: Hero, idx: number) {
   const sp = store.get().stylePreview;
   if (!sp) return;
   const { tileType, groupIndices } = sp;

   if (tileType === "item") {
      const sel = getHeroSelection(store.get().selections, hero.id);
      if (isPersonaActive(hero, sel)) return;
      if (!sel.items.includes(idx)) {
         updateSelection(hero.id, (s) => ({ ...s, items: s.items.filter((i) => !groupIndices.includes(i)) }));
      }
      toggleItem(hero, idx);
   } else if (tileType === "base") {
      toggleBase(hero, idx);
   } else if (tileType === "prismatic") {
      togglePrismatic(hero, idx);
   } else {
      selectSet(hero, idx);
   }
}

export function deselectStylePreview() {
   const sp = store.get().stylePreview;
   if (!sp) return;
   const { heroId, tileType, groupIndices } = sp;
   updateSelection(heroId, (s) => {
      if (tileType === "item") return { ...s, items: s.items.filter((i) => !groupIndices.includes(i)) };
      if (tileType === "base") return s.base != null && groupIndices.includes(s.base) ? { ...s, base: null, prismatic: null } : s;
      if (tileType === "prismatic") return s.prismatic != null && groupIndices.includes(s.prismatic) ? { ...s, prismatic: null } : s;
      return s.set != null && groupIndices.includes(s.set) ? { ...s, set: null } : s;
   });
   store.set({ stylePreview: null });
}


export function requestClearAllSelections() {
   const s = store.get();
   const count = Object.keys(s.selections).length;
   if (count === 0) {
      store.set({ status: "No selections to clear" });
      return;
   }
   const items: ConfirmItem[] = [];
   for (const [heroId, sel] of Object.entries(s.selections)) {
      const hero = s.heroes.find((h) => h.id === heroId);
      if (hero) {
         items.push({ heroName: hero.displayName || hero.name, setName: getSelectionSummary(hero, sel) || "Selected", thumbnail: hero.thumbnail });
      }
   }
   store.set({ confirm: { visible: true, kind: "clearAll", count, items } });
}

let cooldownInterval: ReturnType<typeof setInterval> | null = null;

export function setCooldown(cooldown: CooldownState) {
   if (cooldownInterval) {
      clearInterval(cooldownInterval);
      cooldownInterval = null;
   }

   store.set({ cooldown });

   if (cooldown.active && cooldown.remainingSeconds > 0) {
      cooldownInterval = setInterval(() => {
         const current = store.get().cooldown;
         if (!current.active || current.remainingSeconds <= 1) {
            if (cooldownInterval) {
               clearInterval(cooldownInterval);
               cooldownInterval = null;
            }
            store.set({
               cooldown: { ...current, active: false, remainingSeconds: 0 },
               status: "Ready",
            });
         } else {
            store.set({
               cooldown: { ...current, remainingSeconds: current.remainingSeconds - 1 },
            });
         }
      }, 1000);
   }
}

export function requestGenerate() {
   const s = store.get();

   if (s.cooldown.active && s.cooldown.remainingSeconds > 0) {
      if (s.cooldown.isDailyLimit) {
         const h = Math.floor(s.cooldown.remainingSeconds / 3600);
         const m = Math.floor((s.cooldown.remainingSeconds % 3600) / 60);
         store.set({ status: `Daily limit reached (${h}h ${m}m remaining until reset)` });
      } else {
         const m = Math.floor(s.cooldown.remainingSeconds / 60);
         const sec = s.cooldown.remainingSeconds % 60;
         store.set({ status: `Generation on cooldown (${m}m ${sec}s remaining)` });
      }
      return;
   }

   const active: Selections = {};
   for (const [id, sel] of Object.entries(s.selections)) {
      if (hasAnySelection(sel)) active[id] = sel;
   }
   if (Object.keys(active).length === 0) {
      store.set({ status: "Please select at least one hero" });
      return;
   }
   send("generate", { selections: active });
}

export function resolveConfirm(confirmed: boolean) {
   const c = store.get().confirm;
   if (!c.visible) return;
   store.set({ confirm: { visible: false } });
   if (c.kind === "clearAll") {
      if (confirmed) doClearAllSelections();
   } else {
      send("baseNoSetConfirmed", { confirmed });
   }
}
