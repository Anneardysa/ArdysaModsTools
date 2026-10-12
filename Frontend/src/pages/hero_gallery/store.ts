import { send } from "../../bridge/host";
import { createStore } from "../../bridge/store";
import {
   EMPTY_SELECTION,
   MAX_PARTICLES,
   canCarryParticles,
   filterHeroes,
   findParticle,
   getBlockedIndices,
   getEffectsWithSameTag,
   getHeroSelection,
   getItemsWithSameTag,
   getSetDisplayName,
   getSelectionSummary,
   getSetCategory,
   hasAnySelection,
   heroHasCustomSets,
   isBaseActive,
   carrierOf,
   isCarriedByPickedItem,
   isLockedByBase,
   isPersonaActive,
   normalizeSlot,
   sanitizeSelection,
} from "./helpers";
import type { AlertType, ConfirmItem, CooldownState, DetectionState, FilterCategory, Hero, HeroSelectionState, LatestUpdate, ParticleEntry, ParticleSlots, Selections, TileType, VisualTile } from "./types";

export type ConfirmState =
   | { visible: false }
   | { visible: true; kind: "clearAll"; count: number; items: ConfirmItem[] }
   | { visible: true; kind: "baseNoSet"; title: string; message: string };

export type StylePreviewState = {
   heroId: string;
   tileType: TileType;
   groupIndices: number[];
   group: string;
   groupCover: string | null;
} | null;

export const store = createStore<{
   heroes: Hero[];
   heroesLoad: "loading" | "ready" | "failed";
   particles: ParticleEntry[];
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

   particlePicker: { heroId: string; slot: number } | null;
   particleSlots: Record<string, ParticleSlots | "loading" | "failed">;

   visualOrder: VisualTile[];

   confirm: ConfirmState;

   alert: { visible: boolean; title: string; message: string; type: AlertType; hasLog: boolean };
   generationLogLines: string[];
   logModalOpen: boolean;

   cooldown: CooldownState;

   detection: DetectionState;
}>({
   heroes: [],
   heroesLoad: "loading",
   particles: [],
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
   visualOrder: [],

   stylePreview: null,
   particlePicker: null,
   particleSlots: {},

   confirm: { visible: false },

   alert: { visible: false, title: "", message: "", type: "info", hasLog: false },
   generationLogLines: [],
   logModalOpen: false,

   cooldown: { active: false, remainingSeconds: 0, totalSeconds: 600, dailyUsed: 0, dailyMax: 0, isDailyLimit: false },

   detection: { phase: "scanning" },
});

export function setDetection(found: boolean, count: number) {
   store.set({ detection: { phase: "done", found, count } });
}

export function setDetectionFailed() {
   store.set({ detection: { phase: "failed" } });
}

export function applyDetectedSelections(selections: Selections) {
   if (Object.keys(store.get().selections).length === 0) {
      applyLoadedSelections(selections);
      return;
   }
   notifySelectionChanged();
}

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
   let next = mutate({ ...current, items: [...current.items], effects: [...(current.effects ?? [])], particles: [...(current.particles ?? [])] });
   const hero = s.heroes.find((h) => h.id === heroId);
   if (hero && (next.particles?.length ?? 0) > 0 && !canCarryParticles(hero, next)) {
      next = { ...next, particles: [] };
      store.set({ status: "Particles removed: they need a set, item or arcana to ride on" });
   }
   const selections = withCleanup({ ...s.selections, [heroId]: next }, heroId);
   store.set({ selections });
   notifySelectionChanged();
}

function blockedBySelection(hero: Hero, setIndex: number): boolean {
   const sel = getHeroSelection(store.get().selections, hero.id);
   if (!getBlockedIndices(hero, sel).has(setIndex)) return false;

   const name = getSetDisplayName(hero.sets[setIndex], setIndex);
   if (isLockedByBase(hero, sel, setIndex)) {
      const arcana = getSetDisplayName(hero.sets[sel.base!], sel.base!);
      store.set({ status: `${name} is locked: ${arcana} owns the ${normalizeSlot(hero.sets[setIndex]?.tag)} slot` });
      return true;
   }
   const carrier = carrierOf(hero, sel, setIndex);
   if (carrier != null) {
      store.set({ status: `${name} is already part of ${getSetDisplayName(hero.sets[carrier], carrier)}` });
      return true;
   }
   const active = (hero.sets[setIndex]?.conflicts ?? []).find((i) => isSelected(sel, i));
   const blocker = active != null ? getSetDisplayName(hero.sets[active], active) : null;
   store.set({
      status: blocker
         ? `${name} cannot be combined with ${blocker}; remove it first`
         : `${name} cannot be combined with your current selection`,
   });
   return true;
}

function isSelected(sel: HeroSelectionState, idx: number): boolean {
   return sel.set === idx || sel.base === idx || sel.prismatic === idx || sel.items.includes(idx)
      || (sel.effects?.includes(idx) ?? false);
}

export function selectSet(hero: Hero, setIndex: number): boolean {
   if (blockedBySelection(hero, setIndex)) return false;
   updateSelection(hero.id, (sel) => {
      if (sel.set === setIndex) return { ...sel, set: null };
      const isPersona = getSetCategory(hero.sets[setIndex]) === "persona";
      return isPersona ? { set: setIndex, items: [], base: null, prismatic: null, effects: [], particles: [] } : { ...sel, set: setIndex };
   });
   store.set({ focusedSetIndex: setIndex });
   return true;
}

export function toggleItem(hero: Hero, setIndex: number) {
   const sel = getHeroSelection(store.get().selections, hero.id);
   if (isPersonaActive(hero, sel)) return;
   if (!sel.items.includes(setIndex) && blockedBySelection(hero, setIndex)) return;

   let droppedEffects = 0;
   updateSelection(hero.id, (s) => {
      if (s.items.includes(setIndex)) {
         return { ...s, items: s.items.filter((i) => i !== setIndex) };
      }
      const conflicts = new Set(getItemsWithSameTag(hero, setIndex));
      const next = { ...s, items: [...s.items.filter((i) => !conflicts.has(i)), setIndex] };
      const effects = (s.effects ?? []).filter((i) => !isCarriedByPickedItem(hero, next, i));
      droppedEffects = (s.effects ?? []).length - effects.length;
      return { ...next, effects };
   });
   const status = droppedEffects > 0
      ? `Removed ${droppedEffects} ability effect${droppedEffects > 1 ? "s" : ""}: ${getSetDisplayName(hero.sets[setIndex], setIndex)} already includes ${droppedEffects > 1 ? "them" : "it"}`
      : null;
   store.set({ focusedSetIndex: setIndex, ...(status ? { status } : {}) });
}

export function toggleBase(hero: Hero, setIndex: number) {
   const sel = getHeroSelection(store.get().selections, hero.id);
   if (isPersonaActive(hero, sel)) return;
   if (sel.base !== setIndex && blockedBySelection(hero, setIndex)) return;

   let droppedPrismatic = false;
   let droppedLocked = 0;
   updateSelection(hero.id, (s) => {
      if (s.base === setIndex) {
         droppedPrismatic = s.prismatic != null;
         return { ...s, base: null, prismatic: null };
      }
      const next = { ...s, base: setIndex };
      const items = next.items.filter((i) => !isLockedByBase(hero, next, i));
      droppedLocked = next.items.length - items.length;
      return { ...next, items };
   });
   const status = droppedPrismatic
      ? "Prismatic cleared: it needs an active Base Hero"
      : droppedLocked > 0
         ? `Removed ${droppedLocked} item${droppedLocked > 1 ? "s" : ""}: ${getSetDisplayName(hero.sets[setIndex], setIndex)} owns that slot`
         : null;
   store.set({ focusedSetIndex: setIndex, ...(status ? { status } : {}) });
}

export function togglePrismatic(hero: Hero, setIndex: number) {
   const sel = getHeroSelection(store.get().selections, hero.id);
   if (sel.prismatic !== setIndex && !isBaseActive(sel)) {
      store.set({ status: "Select a Base Hero first to enable Prismatic" });
      return;
   }
   if (sel.prismatic !== setIndex && blockedBySelection(hero, setIndex)) return;
   updateSelection(hero.id, (s) => ({ ...s, prismatic: s.prismatic === setIndex ? null : setIndex }));
   store.set({ focusedSetIndex: setIndex });
}

export function toggleEffect(hero: Hero, setIndex: number) {
   const sel = getHeroSelection(store.get().selections, hero.id);
   if (isPersonaActive(hero, sel)) {
      store.set({ status: "A Persona replaces ability effects" });
      return;
   }
   const effects = sel.effects ?? [];
   if (!effects.includes(setIndex) && blockedBySelection(hero, setIndex)) return;

   updateSelection(hero.id, (s) => {
      const current = s.effects ?? [];
      if (current.includes(setIndex)) return { ...s, effects: current.filter((i) => i !== setIndex) };
      const sameAbility = new Set(getEffectsWithSameTag(hero, setIndex));
      return { ...s, effects: [...current.filter((i) => !sameAbility.has(i)), setIndex] };
   });
   store.set({ focusedSetIndex: setIndex });
}

export function setParticle(hero: Hero, slot: number, id: string, host: string): boolean {
   const s = store.get();
   const sel = getHeroSelection(s.selections, hero.id);
   const picks = sel.particles ?? [];
   const entry = findParticle(s.particles, id);
   const hostEntry = (hero.particleHosts ?? []).find((h) => h.id === host);

   if (isPersonaActive(hero, sel)) {
      store.set({ status: "A Persona replaces particles" });
      return false;
   }
   if (!canCarryParticles(hero, sel)) {
      store.set({ status: "Pick a set, item or arcana first. Particles ride on a worn item." });
      return false;
   }
   if (!entry || !hostEntry) {
      store.set({ status: "That particle cannot go on this hero" });
      return false;
   }
   if (!Number.isInteger(slot) || slot < 0 || slot > picks.length || slot >= MAX_PARTICLES) {
      store.set({ status: `A hero carries at most ${MAX_PARTICLES} particles` });
      return false;
   }
   if (picks.some((p, i) => i !== slot && p.id === entry.id && p.host === host)) {
      store.set({ status: `${entry.name} is already on the ${hostEntry.label}` });
      return false;
   }

   updateSelection(hero.id, (cur) => {
      const particles = [...(cur.particles ?? [])];
      particles[slot] = { id: entry.id, host };
      return { ...cur, particles };
   });
   store.set({ status: `${entry.name} added to the ${hostEntry.label}` });
   return true;
}

export function openParticlePicker(hero: Hero, slot: number) {
   const sel = getHeroSelection(store.get().selections, hero.id);
   const count = sel.particles?.length ?? 0;
   if (isPersonaActive(hero, sel)) {
      store.set({ status: "A Persona replaces particles" });
      return;
   }
   if (!canCarryParticles(hero, sel)) {
      store.set({ status: "Pick a set, item or arcana first. Particles ride on a worn item." });
      return;
   }
   if (slot < 0 || slot > count || slot >= MAX_PARTICLES) {
      store.set({ status: `A hero carries at most ${MAX_PARTICLES} particles` });
      return;
   }
   store.set({ particlePicker: { heroId: hero.id, slot } });
}

export function closeParticlePicker() {
   store.set({ particlePicker: null });
}

export const particleSlotsKey = (heroId: string, setIndex: number) => `${heroId}:${setIndex}`;

export function requestParticleSlots(hero: Hero, setIndex: number) {
   const key = particleSlotsKey(hero.id, setIndex);
   const have = store.get().particleSlots[key];
   if (have && have !== "failed") return;
   store.set({ particleSlots: { ...store.get().particleSlots, [key]: "loading" } });
   send("particleSlots", { heroId: hero.id, setIndex });
}

export function particleSlotsLoaded(heroId: string, setIndex: number, slots: ParticleSlots | null) {
   const valid = slots && Array.isArray(slots.slots) ? slots : null;
   store.set({ particleSlots: { ...store.get().particleSlots, [particleSlotsKey(heroId, setIndex)]: valid ?? "failed" } });
}

export function removeParticle(hero: Hero, slot: number) {
   const pick = getHeroSelection(store.get().selections, hero.id).particles?.[slot];
   if (!pick) return;
   updateSelection(hero.id, (cur) => ({ ...cur, particles: (cur.particles ?? []).filter((_, i) => i !== slot) }));
   store.set({ status: `${findParticle(store.get().particles, pick.id)?.name ?? "Particle"} removed` });
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
   const { heroes, particles } = store.get();
   const cleaned: Selections = {};
   const dropped: string[] = [];

   for (const [heroId, sel] of Object.entries(selections ?? {})) {
      const hero = heroes.find((h) => h.id === heroId);
      if (!hero || !sel) {
         if (sel) cleaned[heroId] = sel;
         continue;
      }
      const result = sanitizeSelection(hero, sel, particles);
      cleaned[heroId] = result.sel;
      for (const name of result.dropped) dropped.push(`${hero.displayName || hero.name}: ${name}`);
   }

   store.set({ selections: cleaned });
   if (dropped.length > 0) {
      store.set({ status: `Dropped ${dropped.length} incompatible pick${dropped.length > 1 ? "s" : ""}: ${dropped.join(", ")}` });
      notifySelectionChanged();
   }
}

export function applyPresetSelections(selections: Selections) {
   store.set({ status: `Loaded ${Object.keys(selections ?? {}).length} selection(s)` });
   applyLoadedSelections(selections);
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
   store.set({ modalHeroId: null, stylePreview: null, particlePicker: null, visualOrder: [] });
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

   const order = s.visualOrder;
   if (order.length === 0) {
      let idx = s.focusedSetIndex + direction;
      if (idx < 0) idx = hero.sets.length - 1;
      if (idx >= hero.sets.length) idx = 0;
      store.set({ focusedSetIndex: idx });
      return;
   }

   const at = order.findIndex((t) => t.idx === s.focusedSetIndex);
   const next = at === -1 ? (direction === 1 ? 0 : order.length - 1) : (at + direction + order.length) % order.length;
   store.set({ focusedSetIndex: order[next]!.idx });
}

export function setVisualOrder(order: VisualTile[]) {
   store.set({ visualOrder: order });
}

export function activateFocusedSet(hero: Hero): void {
   const s = store.get();
   const tile = s.visualOrder.find((t) => t.idx === s.focusedSetIndex);
   const idx = s.focusedSetIndex;
   const group = hero.sets[idx]?.styleGroup;
   if (group && hero.sets.filter((x) => x.styleGroup === group).length > 1) {
      openStylePreview(hero, group, tile?.tileType ?? "set");
      return;
   }
   switch (tile?.tileType) {
      case "item":
         toggleItem(hero, idx);
         return;
      case "base":
         toggleBase(hero, idx);
         return;
      case "prismatic":
         togglePrismatic(hero, idx);
         return;
      case "effect":
         toggleEffect(hero, idx);
         return;
      default:
         if (selectSet(hero, idx)) closeModal();
   }
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
   } else if (tileType === "effect") {
      toggleEffect(hero, idx);
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
      if (tileType === "effect") return { ...s, effects: (s.effects ?? []).filter((i) => !groupIndices.includes(i)) };
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
