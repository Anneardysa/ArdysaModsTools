import { createRoot } from "react-dom/client";
import { expose, send } from "../../bridge/host";
import { translate } from "../../bridge/i18n";
import "../../design/base.css";
import {
   appendLog,
   appendLogI18n,
   clearLog,
   clearStickyLog,
   loadBanners,
   loadLatestUpdates,
   openPatchMenu,
   pushToast,
   setBanner,
   setButtonStates,
   setDotaRunningState,
   setInstallCard,
   setLaunchPanel,
   setNewsVersions,
   setOnboardingFinishedHandler,
   setPatchButtonStatus,
   setPlayState,
   setSetupChecks,
   setStatus,
   showChecking,
   showInstallLog,
   showPathFound,
   showShellConfirm,
   showStickyLog,
   startOnboarding,
   store,
} from "./store";
import type {
   BadgeKind,
   ButtonStates,
   CarouselSlide,
   ConfirmPayload,
   InstallLogPayload,
   LaunchPanelPayload,
   LogCategory,
   LogSegment,
   OnboardStep,
   PlayStatePayload,
   SetupChecksPayload,
   StatusPayload,
   ToastPayload,
   UpdateCard,
} from "./types";
import { App } from "./App";


function parseJson<T>(value: T | string): T {
   return typeof value === "string" ? (JSON.parse(value) as T) : value;
}

setOnboardingFinishedHandler(() => send("onboardingDone"));

expose({
   setVersion: (v: string) => store.set({ version: v || "" }),
   setInstallCard: (url: string) => setInstallCard(url),
   setNewsVersions: (payload: { app?: string; modspack?: string } | string) => {
      const p = parseJson(payload);
      setNewsVersions(p.app, p.modspack);
   },

   setStatus: (payload: StatusPayload | string) => {
      const p = parseJson(payload);
      setStatus({ kind: (p.kind as BadgeKind) || "neutral", text: p.text || "", tooltip: p.tooltip });
   },
   showChecking: () => showChecking(translate("status.checking.text", "Checking…")),

   setPlayState: (payload: PlayStatePayload | string) => setPlayState(parseJson(payload)),
   setLaunchPanel: (payload: LaunchPanelPayload | string | null) => setLaunchPanel(payload == null ? null : parseJson(payload)),

   setSetupChecks: (payload: SetupChecksPayload | string) => setSetupChecks(parseJson(payload)),
   setPatchButton: (status: string | null, isError: boolean) => setPatchButtonStatus((status as "ready" | "needUpdate" | null) ?? null, !!isError),
   setButtonStates: (payload: ButtonStates | string) => setButtonStates(parseJson(payload)),
   setDotaWarning: (on: boolean | string) => setDotaRunningState(on === true || on === "true"),

   showPathFound: (path: string) => showPathFound(path || ""),

   appendLog: (msg: string, cat: string) => appendLog(msg, (cat as LogCategory) || "default"),
   appendLogI18n: (segJson: string, cat: string) => {
      let segs: LogSegment[] = [];
      try {
         segs = JSON.parse(segJson);
      } catch {
         segs = [];
      }
      appendLogI18n(segs, (cat as LogCategory) || "default");
   },
   clearLog: () => clearLog(),
   showStickyLog: (key: string, message: string, category: string) => showStickyLog(key, message, (category as LogCategory) || "default"),
   clearStickyLog: (key: string) => clearStickyLog(key),

   setBanner: (dataUri: string) => setBanner(dataUri),
   loadBanners: (payload: CarouselSlide[] | string) => loadBanners(parseJson(payload)),
   loadLatestUpdates: (payload: UpdateCard[] | string) => loadLatestUpdates(parseJson(payload)),

   showPatchMenu: () => {
      const btn = document.getElementById("btn-patch");
      if (!btn) return;
      const r = btn.getBoundingClientRect();
      openPatchMenu({ left: r.left, top: r.bottom + 6, minWidth: r.width });
   },

   showShellConfirm: (payload: ConfirmPayload | string) => showShellConfirm(parseJson(payload)),
   showToast: (payload: ToastPayload | string) => pushToast(parseJson(payload)),
   showInstallFailure: (payload: Omit<InstallLogPayload, "variant"> | string) => {
      const p = parseJson(payload);
      showInstallLog({ ...p, variant: "error" });
   },
   showInstallComplete: (payload: Omit<InstallLogPayload, "variant"> | string) => {
      const p = parseJson(payload);
      showInstallLog({ ...p, variant: "success" });
   },

   startOnboarding: (payload: OnboardStep[] | string) => startOnboarding(parseJson(payload)),
});

createRoot(document.getElementById("root")!).render(<App />);
