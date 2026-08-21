import { createRoot } from "react-dom/client";
import { expose } from "../../bridge/host";
import "../../design/base.css";
import { applyLoadedSelections, loadHighlightedHeroes, setCooldown, store } from "./store";
import type { AlertType, CooldownState, Hero, LatestUpdate, Selections } from "./types";
import { App } from "./App";

function parseJson<T>(value: T | string): T {
   return typeof value === "string" ? (JSON.parse(value) as T) : value;
}

const MAX_CAROUSEL_ITEMS = 30;
const ALERT_TYPES: readonly AlertType[] = ["success", "warning", "info"];

expose({
   setVersion: (version: string) => store.set({ version: `version ${version}` }),

   loadHeroes: (json: Hero[] | string) => {
      const heroes = parseJson(json);
      store.set({ heroes, status: `Loaded ${heroes.length} heroes` });
   },
   loadFavorites: (json: string[] | string) => store.set({ favorites: new Set(parseJson(json)) }),

   loadLatestUpdates: (json: LatestUpdate[] | string) => {
      const parsed = parseJson(json) ?? [];
      store.set({ latestUpdates: parsed.slice(0, MAX_CAROUSEL_ITEMS) });
   },

   updateStatus: (text: string) => store.set({ status: text }),

   updateCooldown: (json: CooldownState | string) => {
      const parsed = parseJson(json);
      setCooldown(parsed);
   },

   resetCooldown: () => {
      setCooldown({ active: false, remainingSeconds: 0, totalSeconds: 1800 });
   },

   showCachingOverlay: () => store.set({ cachingVisible: true, cachingStatus: { current: 0, total: 0 } }),
   updateCachingProgress: (current: number, total: number) => {
      store.set({ cachingStatus: { current, total } });
      if (current >= total && total > 0) {
         const el = document.getElementById("cachingStatus");
         if (el) el.textContent = "Complete!";
      }
   },
   hideCachingOverlay: () => store.set({ cachingVisible: false }),

   applyLoadedSelections: (json: Selections | string) => {
      const selections = parseJson(json);
      applyLoadedSelections(selections);
      store.set({ status: `Loaded ${Object.keys(selections).length} selection(s)` });
   },

   loadHighlightedHeroes: (json: string[] | string) => loadHighlightedHeroes(parseJson(json)),

   showConfirmBaseNoSet: (title: string, message: string) =>
      store.set({ confirm: { visible: true, kind: "baseNoSet", title, message } }),

   setGenerationLog: (text: string) => store.set({ generationLogLines: text ? text.split("\n") : [] }),

   showAlert: (title: string, message: string, type = "info", _closeFormAfter = false, showLogButton = false) => {
      const normalized: AlertType = (ALERT_TYPES as readonly string[]).includes(type) ? (type as AlertType) : "info";
      store.set({ alert: { visible: true, title, message, type: normalized, hasLog: !!showLogButton } });
   },
});

createRoot(document.getElementById("root")!).render(<App />);
