import { createRoot } from "react-dom/client";
import { expose } from "../../bridge/host";
import { getAllSettings, loadSettings, showCfgBanner, showToast } from "./store";
import type { CfgBannerState, ToastType } from "./types";
import { App } from "./App";

expose({
   loadSettings: (json: string) => loadSettings(json),
   showToast: (message: string, type: string) => showToast(message, (type as ToastType) || "info"),
   showCfgBanner: (message: string, state: string) => showCfgBanner(message, (state as CfgBannerState) || "hidden"),
   getAllSettings: getAllSettings,
});

createRoot(document.getElementById("root")!).render(<App />);
