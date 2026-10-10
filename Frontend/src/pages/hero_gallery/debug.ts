import { send } from "../../bridge/host";
import { createStore } from "../../bridge/store";
import { hasAnySelection } from "./helpers";
import { store } from "./store";
import type { Selections } from "./types";


export type DebugRow = {
   path: string;
   file: string;
   heroId: string;
   heroName: string;
   layer: string;
   order: number;
   top: boolean;
   baseWins: boolean;
   mode: string;
};

export const debugStore = createStore<{
   enabled: boolean;
   open: boolean;
   zips: string[];
   heroesJson: string;
   rows: DebugRow[];
   errors: string[];
   errorsByPath: Record<string, string>;
   warnings: string[];
   planning: boolean;
   seq: number;
}>({
   enabled: false,
   open: false,
   zips: [],
   heroesJson: "",
   rows: [],
   errors: [],
   errorsByPath: {},
   warnings: [],
   planning: false,
   seq: 0,
});

function replan() {
   const s = debugStore.get();
   const seq = s.seq + 1;
   if (s.zips.length === 0) {
      debugStore.set({ seq, rows: [], errors: [], errorsByPath: {}, warnings: [], planning: false });
      return;
   }
   debugStore.set({ seq, planning: true });
   send("debugPlan", { zips: s.zips, heroesJson: s.heroesJson, seq });
}

export const debugHost = {
   setDebugMode: (enabled: boolean, heroesJson: string) => debugStore.set({ enabled, heroesJson }),
   debugZipsPicked: (paths: string[]) => {
      const known = new Set(debugStore.get().zips.map((p) => p.toLowerCase()));
      const added = paths.filter((p) => !known.has(p.toLowerCase()));
      if (added.length === 0) return;
      debugStore.set((s) => ({ zips: [...s.zips, ...added] }));
      replan();
   },
   setDebugHeroesJson: (path: string) => {
      debugStore.set({ heroesJson: path });
      replan();
   },
   setDebugPlan: (plan: {
      seq: number;
      rows: DebugRow[];
      errors: string[];
      errorsByPath?: Record<string, string>;
      warnings?: string[];
   }) => {
      if (plan.seq !== debugStore.get().seq) return;
      debugStore.set({
         rows: plan.rows ?? [],
         errors: plan.errors ?? [],
         errorsByPath: plan.errorsByPath ?? {},
         warnings: plan.warnings ?? [],
         planning: false,
      });
   },
};

export const openDebug = () => debugStore.set({ open: true });
export const closeDebug = () => debugStore.set({ open: false });
export const pickZips = () => send("debugPickZips");
export const pickHeroesJson = () => send("debugPickHeroesJson");

export function removeZip(path: string) {
   const key = path.toLowerCase();
   debugStore.set((s) => ({ zips: s.zips.filter((p) => p.toLowerCase() !== key) }));
   replan();
}

export function clearZips() {
   debugStore.set({ zips: [] });
   replan();
}

export function buildDebug() {
   const s = debugStore.get();
   if (s.zips.length === 0 || s.errors.length > 0 || s.planning) return;
   send("debugBuild", { zips: s.zips, heroesJson: s.heroesJson });
   closeDebug();
}

export function generateModsPack() {
   const active: Selections = {};
   for (const [id, sel] of Object.entries(store.get().selections)) {
      if (hasAnySelection(sel)) active[id] = sel;
   }
   send("debugGenerateModsPack", { selections: active });
   closeDebug();
}
