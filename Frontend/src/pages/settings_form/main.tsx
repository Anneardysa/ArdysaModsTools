import { createRoot } from "react-dom/client";
import { expose } from "../../bridge/host";
import "../../design/base.css";
import { store, type Settings } from "./store";
import { App } from "./App";

expose({
   initSettings: (data: Partial<Settings>) => store.set((s) => ({ settings: { ...s.settings, ...data } })),
   setVersion: (version: string) => store.set({ version: `v${version}` }),
   setCacheSize: (size: string) => store.set({ cacheSize: size }),
   setDotaPath: (path: string) => {
      if (path) store.set((s) => ({ settings: { ...s.settings, dotaPath: path } }));
   },
   revertSetting: (key: keyof Settings, value: boolean) => store.set((s) => ({ settings: { ...s.settings, [key]: value } })),
   showToast: (message: string, type: string) => store.set({ toast: { message, type: type === "error" ? "error" : "success" } }),
   resetCheckUpdatesButton: () => store.set({ checkUpdatesBusy: false }),
   resetClearCacheButton: () => store.set({ clearCacheBusy: false }),
   setConnectionTestProgress: (progress: any) => store.set({ connectionTestProgress: progress, connectionTestBusy: true, isConnectionModalOpen: true }),
   setConnectionTestResults: (report: any) => store.set({ connectionTestReport: report, connectionTestBusy: false, isConnectionModalOpen: true }),
   resetConnectionTestButton: () => store.set({ connectionTestBusy: false }),
});

createRoot(document.getElementById("root")!).render(<App />);
