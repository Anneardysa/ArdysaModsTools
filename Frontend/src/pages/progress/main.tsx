import { createRoot } from "react-dom/client";
import { expose } from "../../bridge/host";
import "../../design/base.css";
import { store, type PreviewHero, type ServerEntry } from "./store";
import { App } from "./App";

expose({
   updateProgress: (percent: number) => {
      store.set({ percent });
      if (percent >= 100) store.set({ dlSpeed: "-- MB/S", downloadProgress: null, metricsHidden: true });
   },
   updateStatus: (text: string) => store.set({ status: text }),
   updateSubstatus: (text: string) => store.set({ substatus: text }),
   updateDownloadSpeed: (speed: string) => store.set({ dlSpeed: speed }),
   updateWriteSpeed: () => {},
   updateDownloadProgress: (downloadedMB: number, totalMB: number) =>
      store.set({ downloadProgress: totalMB > 0 ? { kind: "bytes", downloaded: downloadedMB, total: totalMB } : null }),
   hideDownloadProgress: () => store.set({ downloadProgress: null, metricsHidden: true }),
   updateFileProgress: (current: number, total: number) =>
      store.set({ downloadProgress: total > 0 ? { kind: "files", current, total } : null }),
   updateServerLog: (servers: ServerEntry[]) => store.set({ serverLog: servers ?? [] }),
   hideCancel: () => store.set({ cancelHidden: true }),
   showPreviewLoading: () => store.set({ preview: { status: "loading" } }),
   showPreviewError: (message: string) =>
      store.set({ preview: { status: "error", message: message || "Could not load preview data" } }),
   initPreview: (heroes: PreviewHero[]) => store.set({ preview: { status: "ready", heroes } }),
});

createRoot(document.getElementById("root")!).render(<App />);
