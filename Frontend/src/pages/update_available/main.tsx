import { createRoot } from "react-dom/client";
import { expose } from "../../bridge/host";
import "../../design/base.css";
import { store, type UpdateInfoPayload } from "./store";
import { App } from "./App";

expose({
   setUpdateInfo: (data: UpdateInfoPayload) =>
      store.set({ info: data, deltaChecking: Boolean(data.deltaPending), controlsVisible: !data.deltaPending }),
   setAutoUpdateNote: (title: string, desc: string) => store.set({ deltaChecking: false, autoNote: { title, desc } }),
   setDeltaUnavailable: () => store.set({ deltaChecking: false, controlsVisible: true }),
   setUpdateBusy: (title: string) => store.set({ busy: title, deltaChecking: false, controlsVisible: false, error: null }),
   setUpdateProgress: (percent: number) => store.set({ progressPercent: Math.max(0, Math.min(100, percent)) }),
   setUpdateStatus: (text: string) => store.set({ progressText: text }),
   setUpdateFailed: (message: string) =>
      store.set({ busy: null, autoNote: null, deltaChecking: false, controlsVisible: true, error: message }),
});

createRoot(document.getElementById("root")!).render(<App />);
