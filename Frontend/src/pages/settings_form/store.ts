import { createStore } from "../../bridge/store";

export type Settings = {
   startup: boolean;
   tray: boolean;
   notifications: boolean;
   preloadAssets: boolean;
   autoDetectPath: boolean;
   dotaPath: string;
   language: string;
   uiScale: number;
   theme: string;
   cdnServer: string;
};

const DEFAULT_SETTINGS: Settings = {
   startup: false,
   tray: false,
   notifications: true,
   preloadAssets: true,
   autoDetectPath: true,
   dotaPath: "",
   language: "en",
   uiScale: 1,
   theme: "dark",
   cdnServer: "auto",
};

export const store = createStore<{
   settings: Settings;
   version: string;
   cacheSize: string;
   toast: { message: string; type: "success" | "error" } | null;
   checkUpdatesBusy: boolean;
   clearCacheBusy: boolean;
}>({
   settings: DEFAULT_SETTINGS,
   version: "",
   cacheSize: "",
   toast: null,
   checkUpdatesBusy: false,
   clearCacheBusy: false,
});
