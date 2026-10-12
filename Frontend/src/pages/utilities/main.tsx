import { createRoot } from "react-dom/client";
import { expose } from "../../bridge/host";
import "../../design/base.css";
import { App } from "./App";
import { showToast, STAGES, store, type BadgeRequest, type RankRequest, type Stage } from "./store";

type HostState = {
   rank?: { applied?: boolean; request?: RankRequest | null };
   badge?: { applied?: boolean; request?: BadgeRequest | null };
   isModInstalled?: boolean;
};

function loadState(json: string): void {
   try {
      const state = JSON.parse(json) as HostState;
      store.set({
         hydrated: true,
         applied: state.rank?.applied === true,
         saved: state.rank?.request ?? null,
         badgeApplied: state.badge?.applied === true,
         badgeSaved: state.badge?.request ?? null,
         isModInstalled: state.isModInstalled ?? true,
      });
   } catch {
      store.set({ hydrated: true, applied: false, saved: null, badgeApplied: false, badgeSaved: null, isModInstalled: true });
   }
}

expose({
   loadState: (json: string) => loadState(json),
   showToast: (message: string, kind: string) => showToast(message, kind),
   setBusy: (busy: boolean) => setBusy(busy),
   setProgress: (stage: string, percent: number) => {
      if (!STAGES.includes(stage as Stage)) return;
      store.set({ stage: stage as Stage, percent: Math.min(Math.max(Math.round(percent), 0), 100) });
   },
});

let lingerTimer = 0;

function setBusy(busy: boolean): void {
   window.clearTimeout(lingerTimer);
   if (busy) {
      store.set({ busy, lingering: false, stage: "start", percent: 0 });
      return;
   }
   const finished = store.get().stage === "finish";
   store.set({ busy, lingering: finished });
   if (finished) lingerTimer = window.setTimeout(() => store.set({ lingering: false }), 800);
}

createRoot(document.getElementById("root")!).render(<App />);
