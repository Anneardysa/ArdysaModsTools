import { createStore } from "../../bridge/store";

export type RankRequest = {
   replaceEveryRank: boolean;
   currentTier: number;
   currentStars: number;
   targetTier: number;
   targetStars: number;
   applyStars: boolean;
   applyProfile: boolean;
   applyScoreboard: boolean;
   targetLeaderboard: number;
   profileMmr: number;
   rankConfidence: number;
   leaderboardRank: number;
};

export type BadgeRequest = {
   show: "keep" | "tier" | "hide";
   tier: number;
   level: "keep" | "hide" | "custom";
   customLevel: number;
   progress: "keep" | "max" | "custom";
   customProgress: number;
   onlyTier: number | null;
};

export type Tool = "rankChanger" | "heroBadge";

export type Toast = { id: number; message: string; kind: string };

export const STAGES = ["start", "processing", "compiling", "finish"] as const;
export type Stage = (typeof STAGES)[number];

export const store = createStore({
   hydrated: false,
   applied: false,
   saved: null as RankRequest | null,
   badgeApplied: false,
   badgeSaved: null as BadgeRequest | null,
   tool: "rankChanger" as Tool,
   busy: false,
   runKind: "apply" as "apply" | "reset",
   runTool: "rankChanger" as Tool,
   stage: "start" as Stage,
   percent: 0,
   lingering: false,
   toast: null as Toast | null,
   faqOpen: false,
   isModInstalled: true,
});

let toastId = 0;

export function showToast(message: string, kind = "info"): void {
   store.set({ toast: { id: ++toastId, message, kind } });
}

export function dismissToast(id: number): void {
   if (store.get().toast?.id === id) store.set({ toast: null });
}

export function setFaqOpen(open: boolean): void {
   store.set({ faqOpen: open });
}
