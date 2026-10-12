import { useEffect, useState } from "react";
import { send, useEscape } from "../../bridge/host";
import { T, useLocale } from "../../bridge/i18n";
import { Titlebar, TitlebarButton } from "../../ui/Titlebar";
import { HeroBadge } from "./HeroBadge";
import { RankChanger } from "./RankChanger";
import { RankFaq } from "./RankFaq";
import { dismissToast, setFaqOpen, STAGES, store, type Tool } from "./store";
import css from "./utilities.module.css";

const TOOLS = [
   {
      id: "rankChanger",
      labelKey: "utilities.tool.rankChanger",
      label: "Rank Changer",
      descKey: "utilities.tool.rankChanger.desc",
      desc: "Swap the medal your client draws in profiles and scoreboards.",
      ready: true,
      icon: (
         <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <circle cx="12" cy="9" r="5" />
            <path d="M8.5 13.5 7 22l5-2.5L17 22l-1.5-8.5" />
         </svg>
      ),
   },
   {
      id: "heroBadge",
      labelKey: "utilities.tool.heroBadge",
      label: "Hero Badge",
      descKey: "utilities.tool.heroBadge.desc",
      desc: "Swap the hero level badge, its number and the level bar.",
      ready: true,
      icon: (
         <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <path d="M5 4h14l-1.5 11L12 21l-5.5-6z" />
         </svg>
      ),
   },
   {
      id: "dashboardBackground",
      labelKey: "utilities.tool.dashboardBackground",
      label: "Dashboard Background",
      descKey: "utilities.tool.dashboardBackground.desc",
      desc: "Not built yet.",
      ready: false,
      icon: (
         <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <rect x="3" y="4" width="18" height="16" />
            <path d="M3 16l5-5 4 4 3-3 6 6" />
            <circle cx="9" cy="9" r="1.4" fill="currentColor" stroke="none" />
         </svg>
      ),
   },
] as const;

export function App() {
   const busy = store.use((s) => s.busy);
   const faqOpen = store.use((s) => s.faqOpen);
   const lingering = store.use((s) => s.lingering);
   const activeTool = store.use((s) => s.tool);

   useEscape(() => {
      if (faqOpen) {
         setFaqOpen(false);
         return;
      }
      if (!busy) send("close");
   });

   return (
      <>
         <Titlebar titleKey="utilities.title" title="Utilities">
            {activeTool === "rankChanger" && (
               <TitlebarButton
                  labelKey="rank.faq.open"
                  label="Information"
                  onClick={() => setFaqOpen(true)}
               >
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                     <circle cx="12" cy="12" r="10" />
                     <line x1="12" y1="16" x2="12" y2="12" />
                     <line x1="12" y1="8" x2="12.01" y2="8" />
                  </svg>
               </TitlebarButton>
            )}
         </Titlebar>

         <main className={css.body}>
            <nav className={`${css.rail} notch`} aria-label="Tools">
               <h2 className={css.railLabel}>
                  <T k="utilities.group.tools">Tools</T>
               </h2>
               {TOOLS.map((tool) => (
                  <ToolButton key={tool.id} tool={tool} />
               ))}
            </nav>

            {activeTool === "heroBadge" ? <HeroBadge /> : <RankChanger />}
         </main>

         <ToastHost />
         {faqOpen && <RankFaq onClose={() => setFaqOpen(false)} />}
         {(busy || lingering) && <ProgressPanel />}
      </>
   );
}

function ToolButton({ tool }: { tool: (typeof TOOLS)[number] }) {
   const { t } = useLocale();
   const active = store.use((s) => s.tool) === tool.id;

   return (
      <button
         type="button"
         data-no-drag
         className={`${css.toolBtn} ${active ? css.toolOn : ""}`}
         disabled={!tool.ready}
         aria-current={active ? "page" : undefined}
         onClick={() => store.set({ tool: tool.id as Tool })}
         title={tool.ready ? undefined : t("utilities.tool.soonTip", "This tool has not been built yet.")}
      >
         <span className={css.toolIcon}>{tool.icon}</span>
         <span className={css.toolName}>{t(tool.labelKey, tool.label)}</span>
         {!tool.ready && (
            <span className={css.soon}>
               <T k="shell.tag.soon">SOON</T>
            </span>
         )}
      </button>
   );
}

function ProgressPanel() {
   const runKind = store.use((s) => s.runKind);
   const runTool = store.use((s) => s.runTool);
   const stage = store.use((s) => s.stage);
   const percent = store.use((s) => s.percent);
   const at = STAGES.indexOf(stage);

   return (
      <div className={css.modalScrim}>
         <div
            className={`${css.modal} ${css.progress} notch`}
            role="dialog"
            aria-modal="true"
            aria-busy={stage !== "finish"}
         >
            <header className={css.modalHead}>
               <h2 className={css.modalTitle}>
                  {runTool === "heroBadge" ? (
                     runKind === "reset" ? (
                        <T k="badge.progress.reset">Restoring original badges</T>
                     ) : (
                        <T k="badge.progress.apply">Applying hero badge</T>
                     )
                  ) : runKind === "reset" ? (
                     <T k="rank.progress.reset">Restoring original medals</T>
                  ) : (
                     <T k="rank.progress.apply">Applying rank</T>
                  )}
               </h2>
               <span className={css.progressPct}>{percent}%</span>
            </header>

            <div className={css.progressBody}>
               <div
                  className={css.progressBar}
                  role="progressbar"
                  aria-valuemin={0}
                  aria-valuemax={100}
                  aria-valuenow={percent}
               >
                  <span style={{ transform: `scaleX(${percent / 100})` }} />
               </div>

               <ol className={css.progressStages}>
                  {STAGES.map((s, i) => (
                     <li
                        key={s}
                        className={i < at || stage === "finish" ? css.stageDone : i === at ? css.stageNow : css.stageNext}
                        aria-current={i === at ? "step" : undefined}
                     >
                        <T k={`rank.stage.${s}`}>{s}</T>
                     </li>
                  ))}
               </ol>

               <p className={css.progressHint}>
                  <T k="rank.progress.hint">Keep Dota 2 closed until this finishes.</T>
               </p>
            </div>
         </div>
      </div>
   );
}

function ToastHost() {
   const toast = store.use((s) => s.toast);
   const [visible, setVisible] = useState(false);

   useEffect(() => {
      if (!toast) return;
      setVisible(true);
      const ms = toast.kind === "error" ? 9000 : 4500;
      const timer = window.setTimeout(() => dismissToast(toast.id), ms);
      return () => window.clearTimeout(timer);
   }, [toast]);

   if (!toast) return null;

   return (
      <div
         key={toast.id}
         className={`${css.toast} ${css[toast.kind] ?? ""} ${visible ? css.toastIn : ""} notch`}
         role={toast.kind === "error" ? "alert" : "status"}
         onClick={() => dismissToast(toast.id)}
      >
         {toast.message}
      </div>
   );
}
