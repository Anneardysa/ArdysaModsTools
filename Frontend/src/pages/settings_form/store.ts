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

export type ServerConnectionResult = {
   serverKey: string;
   serverName: string;
   baseUrl: string;
   isReachable: boolean;
   latencyMs: number;
   jitterMs?: number;
   downloadSpeedKBps: number;
   downloadSpeedMBps: number;
   peakSpeedMBps?: number;
   stabilityPercent?: number;
   dataSampledMB?: number;
   status: "optimal" | "good" | "fair" | "slow" | "unreachable" | string;
   errorDetail?: string | null;
   isRecommended: boolean;
   qualityScore: number;
};

export type ConnectionTestReport = {
   servers: ServerConnectionResult[];
   recommendedServerKey: string;
   recommendedServerName: string;
   diagnosticMessage: string;
   diagnosticSeverity: "success" | "warning" | "error" | "info";
   testedAt: string;
};

export type ConnectionTestProgress = {
   stage: string;
   currentServerName: string;
   percent: number;
   message: string;
};

export const store = createStore<{
   settings: Settings;
   version: string;
   cacheSize: string;
   toast: { message: string; type: "success" | "error" } | null;
   checkUpdatesBusy: boolean;
   clearCacheBusy: boolean;
   connectionTestBusy: boolean;
   connectionTestProgress: ConnectionTestProgress | null;
   connectionTestReport: ConnectionTestReport | null;
   isConnectionModalOpen: boolean;
}>({
   settings: DEFAULT_SETTINGS,
   version: "",
   cacheSize: "",
   toast: null,
   checkUpdatesBusy: false,
   clearCacheBusy: false,
   connectionTestBusy: false,
   connectionTestProgress: null,
   connectionTestReport: null,
   isConnectionModalOpen: false,
});
