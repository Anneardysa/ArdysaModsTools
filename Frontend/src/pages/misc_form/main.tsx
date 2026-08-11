import { createRoot } from "react-dom/client";
import { expose } from "../../bridge/host";
import { translate } from "../../bridge/i18n";
import "../../design/base.css";
import { applyResetOption, store, type AlertType, type MiscOption, type Selections } from "./store";
import { App } from "./App";

function parseJson<T>(value: T | string): T {
   return typeof value === "string" ? (JSON.parse(value) as T) : value;
}

expose({
   setDefaultThumb: (url: string) => store.set({ defaultThumb: url }),

   loadOptions: (json: MiscOption[] | string) =>
      store.set({
         options: parseJson(json),
         cachingVisible: false,
         status: translate("hero.status.ready", "Ready"),
      }),
   loadSelections: (json: Selections | string) => store.set({ selections: parseJson(json) }),

   setStatus: (text: string) => store.set({ status: text }),
   setVersion: (version: string) => store.set({ version: `version ${version}` }),

   showCachingOverlay: () => store.set({ cachingVisible: true }),
   updateCachingProgress: (current: number, total: number) => {
      store.set({ cachingStatus: { current, total } });
      if (current >= total && total > 0) {
         const el = document.getElementById("cachingStatus");
         if (el) el.textContent = "Complete!";
      }
   },
   hideCachingOverlay: () => store.set({ cachingVisible: false }),

   resetOption: applyResetOption,

   showModeModal: () => store.set({ modeModalOpen: true }),
   closeModeModal: () => store.set({ modeModalOpen: false }),

   showProgress: (title?: string) =>
      store.set((s) => ({
         progress: {
            ...s.progress,
            visible: true,
            title: title || translate("miscForm.generating", "Generating..."),
            percent: 0,
            status: translate("miscForm.preparing", "Preparing..."),
            lines: [],
            flash: null,
         },
      })),
   hideProgress: () => store.set((s) => ({ progress: { ...s.progress, visible: false } })),
   updateProgress: (percent: number, status?: string) =>
      store.set((s) => ({ progress: { ...s.progress, percent, status: status || s.progress.status } })),
   appendConsole: (msg: string) => {
      if (!msg) return;
      store.set((s) => ({ progress: { ...s.progress, lines: [...s.progress.lines, msg] } }));
   },
   clearConsole: () => store.set((s) => ({ progress: { ...s.progress, lines: [] } })),
   setGenerating: (gen: boolean) => store.set({ generating: gen }),

   flashConsole: (type: string) =>
      store.set((s) => ({ progress: { ...s.progress, flash: type === "error" ? "error" : "success" } })),

   showAlert: (title: string, message: string, type: string = "info") => {
      const normalized: AlertType = (["success", "warning", "error", "info"] as const).includes(type as AlertType)
         ? (type as AlertType)
         : "info";
      const hasLog =
         normalized !== "info" && store.get().progress.lines.some((line) => line.trim().length > 0);
      store.set((s) => ({
         progress: { ...s.progress, visible: false },
         alert: { visible: true, title, message, type: normalized, hasLog },
      }));
   },
});

createRoot(document.getElementById("root")!).render(<App />);
