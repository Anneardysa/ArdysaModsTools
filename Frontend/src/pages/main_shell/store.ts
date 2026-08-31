import { createStore } from "../../bridge/store";
import type {
   ButtonStates,
   CarouselSlide,
   ChangelogPayload,
   ConfirmPayload,
   ConsoleLine,
   InstallLogPayload,
   LaunchPanelPayload,
   LogCategory,
   LogSegment,
   OnboardStep,
   PlayStatePayload,
   SetupChecksPayload,
   StatusPayload,
   SyncDetailsPayload,
   ToastPayload,
   UpdateCard,
} from "./types";

const MAX_LINES = 500;

const DEFAULT_INSTALL_CARD = "https://cdn.ardysamods.my.id/Assets/image/banner/install_card.png";
const defaultInstallCardUrl = `${DEFAULT_INSTALL_CARD}?t=${Date.now()}`;

export type PatchMenuState = { visible: boolean; left: number; top: number; minWidth: number };
export type Toast = ToastPayload & { id: number };

export const store = createStore<{
   version: string;
   installCardUrl: string | null;
   newsAppVersion: string;
   newsModspackVersion: string;

   status: StatusPayload;
   buttons: ButtonStates;
   patchButton: { status: "ready" | "needUpdate" | null; isError: boolean };

   verifyChecks: SetupChecksPayload | null;
   elevationDetail: string;
   elevationFound: boolean;
   elevationCanFix: boolean;
   elevationFixLabel: string;
   syncDetail: string;
   syncNeedsFix: boolean;
   syncDetails: SyncDetailsPayload | null;
   elevationModalOpen: boolean;
   syncModalOpen: boolean;
   syncDetailsModalOpen: boolean;
   elevationRechecking: boolean;

   playState: PlayStatePayload;
   launchPanel: LaunchPanelPayload;

   dotaRunning: boolean;
   pathFound: { visible: boolean; path: string };

   consoleLines: ConsoleLine[];

   carouselSlides: CarouselSlide[];
   carouselIndex: number;

   updatesCards: UpdateCard[];
   updatesTotal: number;

   patchMenu: PatchMenuState;
   newsModalOpen: boolean;
   changelogModal: ChangelogPayload | null;
   confirmModal: ConfirmPayload | null;
   confirmCountdown: number;
   installLogModal: InstallLogPayload | null;

   toasts: Toast[];

   onboarding: { steps: OnboardStep[]; index: number } | null;
}>({
   version: "",
   installCardUrl: defaultInstallCardUrl,
   newsAppVersion: "App v—",
   newsModspackVersion: "ModsPack v—",

   status: { kind: "neutral", text: "", tooltip: "" },
   buttons: {},
   patchButton: { status: null, isError: false },

   verifyChecks: null,
   elevationDetail: "",
   elevationFound: false,
   elevationCanFix: false,
   elevationFixLabel: "",
   syncDetail: "",
   syncNeedsFix: false,
   syncDetails: null,
   elevationModalOpen: false,
   syncModalOpen: false,
   syncDetailsModalOpen: false,
   elevationRechecking: false,

   playState: { enabled: false, label: "", reason: "" },
   launchPanel: null,

   dotaRunning: false,
   pathFound: { visible: false, path: "" },

   consoleLines: [],

   carouselSlides: [],
   carouselIndex: 0,

   updatesCards: [],
   updatesTotal: 0,

   patchMenu: { visible: false, left: 0, top: 0, minWidth: 0 },
   newsModalOpen: false,
   changelogModal: null,
   confirmModal: null,
   confirmCountdown: 0,
   installLogModal: null,

   toasts: [],

   onboarding: null,
});


export function setStatus(p: StatusPayload) {
   store.set({ status: p });
}

export function showChecking(label: string) {
   store.set({ status: { kind: "loading", text: label, tooltip: "" } });
}

export function setButtonStates(patch: ButtonStates) {
   store.set((s) => ({ buttons: { ...s.buttons, ...patch } }));
}

export function setPatchButtonStatus(status: "ready" | "needUpdate" | null, isError: boolean) {
   store.set({ patchButton: { status, isError } });
}


export function setSetupChecks(p: SetupChecksPayload) {
   if (!p.checks || p.checks.length === 0) {
      store.set({
         verifyChecks: null,
         elevationDetail: "",
         elevationFound: false,
         elevationCanFix: false,
      });
      return;
   }

   const elevation = p.checks.find((c) => c.id === "NotForcedToRunAsAdmin");
   const sync = p.checks.find((c) => c.id === "ItemsGameInSync");

   store.set({
      verifyChecks: p,
      elevationDetail: elevation?.detail ?? "",
      elevationFound: !!elevation?.detected,
      elevationCanFix: p.canFix === true,
      elevationFixLabel: p.fixLabel || "Fix Setup",
      syncDetail: sync?.detail ?? "",
      syncNeedsFix: sync?.state === "fail",
      elevationRechecking: false,
   });
}

export function setSyncDetails(p: SyncDetailsPayload) {
   store.set({ syncDetails: p });
}

export function openElevationModal() {
   store.set((s) => ({ elevationModalOpen: true, elevationRechecking: !!s.elevationDetail }));
}
export function closeElevationModal() {
   store.set({ elevationModalOpen: false });
}
export function openSyncModal() {
   store.set({ syncModalOpen: true });
}
export function closeSyncModal() {
   store.set({ syncModalOpen: false });
}
export function openSyncDetailsModal() {
   store.set({ syncModalOpen: false, syncDetailsModalOpen: true });
}
export function closeSyncDetailsModal() {
   store.set({ syncDetailsModalOpen: false });
}


export function setPlayState(p: PlayStatePayload) {
   store.set({ playState: p });
}

export function setLaunchPanel(p: LaunchPanelPayload) {
   store.set({ launchPanel: p });
}


export function setDotaRunningState(running: boolean) {
   store.set({ dotaRunning: running });
}

let pathFoundTimer: number | null = null;

export function showPathFound(path: string) {
   store.set({ pathFound: { visible: true, path } });
   if (pathFoundTimer != null) window.clearTimeout(pathFoundTimer);
   pathFoundTimer = window.setTimeout(() => store.set((s) => ({ pathFound: { ...s.pathFound, visible: false } })), 4000);
}


let lineSeq = 0;

function pushLine(line: ConsoleLine) {
   store.set((s) => {
      const lines = [...s.consoleLines, line];
      while (lines.length > MAX_LINES) lines.shift();
      return { consoleLines: lines };
   });
}

export function appendLog(text: string, category: LogCategory) {
   pushLine({ id: ++lineSeq, category, text });
}

export function appendLogI18n(segments: LogSegment[], category: LogCategory) {
   pushLine({ id: ++lineSeq, category, segments });
}

export function showStickyLog(key: string, text: string, category: LogCategory) {
   store.set((s) => {
      const idx = s.consoleLines.findIndex((l) => l.key === key);
      if (idx >= 0) {
         const lines = s.consoleLines.slice();
         lines[idx] = { ...lines[idx]!, category, text, segments: undefined };
         return { consoleLines: lines };
      }
      const lines = [...s.consoleLines, { id: ++lineSeq, key, category, text }];
      while (lines.length > MAX_LINES) lines.shift();
      return { consoleLines: lines };
   });
}

export function clearStickyLog(key: string) {
   store.set((s) => ({ consoleLines: s.consoleLines.filter((l) => l.key !== key) }));
}

export function clearLog() {
   store.set({ consoleLines: [] });
}


export function setBanner(dataUri: string) {
   if (!dataUri || store.get().carouselSlides.length > 0) return;
   store.set({ carouselSlides: [{ image: dataUri, link: "", title: "" }], carouselIndex: 0 });
}

export function loadBanners(slides: CarouselSlide[]) {
   if (!Array.isArray(slides) || slides.length === 0) return;
   store.set({ carouselSlides: slides, carouselIndex: 0 });
}

export function carouselGo(i: number) {
   const n = store.get().carouselSlides.length;
   if (n === 0) return;
   store.set({ carouselIndex: ((i % n) + n) % n });
}

export function loadLatestUpdates(cards: UpdateCard[]) {
   if (!Array.isArray(cards) || cards.length === 0) return;
   store.set({ updatesCards: cards.slice(0, 12), updatesTotal: cards.length });
}

export function dropUpdateCard(index: number) {
   store.set((s) => ({ updatesCards: s.updatesCards.filter((_, i) => i !== index) }));
}

export function setInstallCard(url: string) {
   if (!url) return;
   store.set({ installCardUrl: url + (url.includes("?") ? "&" : "?") + "t=" + Date.now() });
}

export function setNewsVersions(app: string | undefined, modspack: string | undefined) {
   store.set({
      ...(app ? { newsAppVersion: "App " + app } : {}),
      ...(modspack ? { newsModspackVersion: "ModsPack " + modspack } : {}),
   });
}


export function openPatchMenu(rect: { left: number; top: number; minWidth: number }) {
   store.set({ patchMenu: { visible: true, ...rect } });
}
export function closePatchMenu() {
   store.set((s) => (s.patchMenu.visible ? { patchMenu: { ...s.patchMenu, visible: false } } : {}));
}

export function openNewsModal() {
   closePatchMenu();
   store.set({ newsModalOpen: true });
}
export function closeNewsModal() {
   store.set({ newsModalOpen: false });
}

export function showChangelogModal(p: ChangelogPayload) {
   closePatchMenu();
   store.set({
      newsModalOpen: false,
      changelogModal: { tag: p.tag ?? "", name: p.name ?? "", date: p.date ?? null, body: p.body ?? "", url: p.url ?? "" },
   });
}
export function closeChangelogModal() {
   store.set({ changelogModal: null });
}


export function showShellConfirm(p: ConfirmPayload) {
   closePatchMenu();
   const countdown = Number.parseInt(String(p.countdown), 10) || 0;
   store.set({ confirmModal: p, confirmCountdown: countdown > 0 ? countdown : 0 });
}
export function tickConfirmCountdown() {
   store.set((s) => ({ confirmCountdown: Math.max(0, s.confirmCountdown - 1) }));
}
export function closeConfirmModal() {
   store.set({ confirmModal: null, confirmCountdown: 0 });
}

export function showInstallLog(p: InstallLogPayload) {
   closePatchMenu();
   store.set({ installLogModal: p });
}
export function closeInstallLogModal() {
   store.set({ installLogModal: null });
}


let toastSeq = 0;

export function pushToast(p: ToastPayload) {
   const id = ++toastSeq;
   store.set((s) => ({ toasts: [...s.toasts, { ...p, id }] }));
   return id;
}
export function dismissToast(id: number) {
   store.set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) }));
}


export function startOnboarding(steps: OnboardStep[]) {
   if (!Array.isArray(steps) || steps.length === 0) return;
   closePatchMenu();
   store.set({ onboarding: { steps, index: 0 } });
}
export function onboardNext() {
   const ob = store.get().onboarding;
   if (!ob) return;
   if (ob.index >= ob.steps.length - 1) {
      finishOnboarding();
      return;
   }
   store.set({ onboarding: { ...ob, index: ob.index + 1 } });
}
export function onboardPrev() {
   const ob = store.get().onboarding;
   if (!ob || ob.index <= 0) return;
   store.set({ onboarding: { ...ob, index: ob.index - 1 } });
}
let onFinishOnboarding: (() => void) | null = null;
export function setOnboardingFinishedHandler(fn: () => void) {
   onFinishOnboarding = fn;
}
export function finishOnboarding() {
   if (!store.get().onboarding) return;
   store.set({ onboarding: null });
   onFinishOnboarding?.();
}
