import { useEffect } from "react";
import { send } from "../../bridge/host";
import { T, translate, useLocale } from "../../bridge/i18n";
import { store, type AppCheck } from "./store";
import css from "./settings.module.css";


const WrenchIcon = () => (
   <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z" />
   </svg>
);

const RetestIcon = () => (
   <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M21.5 2v6h-6M21.34 15.57a10 10 0 1 1-.57-8.38l5.67-5.67" />
   </svg>
);

function stateBadgeKind(state: string) {
   switch (state) {
      case "pass":
         return "success";
      case "advisory":
         return "warning";
      case "fail":
         return "danger";
      default:
         return "info";
   }
}

function CheckRow({ check }: { check: AppCheck }) {
   const { t } = useLocale();
   const kind = stateBadgeKind(check.state);

   return (
      <div className={css.toggleRow}>
         <div className={css.checkRowLeft}>
            <span className={`${css.checkLed} ${css[`led_${kind}`]}`} />
            <div className={css.toggleInfo}>
               <span className={css.toggleLabel}>{translate(check.titleKey, check.id)}</span>
               <span className={css.toggleDesc}>
                  {translate(check.detailKey, "", check.detailVars ?? undefined)}
               </span>
               {check.appliesOnRestart && (
                  <span className={css.toggleDesc}>
                     <T k="settings.repair.restartNote">Applies the next time AMT opens.</T>
                  </span>
               )}
               {check.diagnostic && <code className={css.checkDiagnostic}>{check.diagnostic}</code>}
            </div>
         </div>
         <span className={css.statusBadge} data-kind={kind}>
            {t(`settings.repair.state.${check.state}`, check.state.toUpperCase())}
         </span>
      </div>
   );
}

export function RepairModal() {
   const isOpen = store.use((s) => s.isRepairModalOpen);
   const busy = store.use((s) => s.repairBusy);
   const progress = store.use((s) => s.repairProgress);
   const report = store.use((s) => s.repairReport);
   const confirming = store.use((s) => s.repairConfirming);

   const close = () => {
      if (busy) send("cancelRepair");
      store.set({ isRepairModalOpen: false, repairConfirming: false });
   };

   const startScan = () => {
      store.set({ repairBusy: true, repairProgress: null, repairConfirming: false });
      send("runRepairScan");
   };

   const runFix = (ids: string[]) => {
      store.set({ repairBusy: true, repairProgress: null, repairConfirming: false });
      send("runRepairFix", { ids });
   };

   const confirmItems = report ? report.checks.filter((c) => report.fixableIds.includes(c.id) && c.needsConfirm) : [];

   const repairAll = () => {
      if (!report) return;
      if (confirmItems.length > 0) {
         store.set({ repairConfirming: true });
      } else {
         runFix(report.fixableIds);
      }
   };

   useEffect(() => {
      const onKeyDown = (e: KeyboardEvent) => {
         if (e.key === "Escape" && isOpen) close();
      };
      window.addEventListener("keydown", onKeyDown);
      return () => window.removeEventListener("keydown", onKeyDown);
      // eslint-disable-next-line react-hooks/exhaustive-deps
   }, [isOpen, busy]);

   if (!isOpen) return null;

   const percent = progress?.percent ?? 0;
   const allHealthy = !!report && !report.checks.some((c) => c.state !== "pass");

   return (
      <div
         className={`${css.modalOverlay} ${css.show}`}
         onClick={(e) => {
            if (e.target === e.currentTarget) close();
         }}
      >
         <div className={css.connectionModal} role="dialog" aria-modal="true" aria-labelledby="repair-title">
            <div className={css.modalHead}>
               <div className={css.modalTitle}>
                  <span className={`${css.led} ${busy ? css.ledBusy : ""}`} />
                  <span id="repair-title">
                     [ REPAIR // <T k="settings.repair.title">Repair App</T> ]
                  </span>
               </div>
               <button type="button" data-no-drag className={css.modalX} onClick={close} aria-label="Close">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
                     <line x1="6" y1="6" x2="18" y2="18" />
                     <line x1="18" y1="6" x2="6" y2="18" />
                  </svg>
               </button>
            </div>

            <div className={css.modalBody}>
               {busy ? (
                  <div className={css.testingView}>
                     <div className={css.launchSpinner} />
                     <div className={css.testingStatus}>
                        <span className={css.testingStageTitle}>
                           {translate(progress?.messageKey ?? "", "Working...", progress?.messageVars ?? undefined)}
                        </span>
                        <div className={css.progressBarTrack}>
                           <div className={css.progressBarFill} style={{ width: `${percent}%` }} />
                        </div>
                        <span className={css.progressPercentText}>{percent}% COMPLETE</span>
                     </div>
                  </div>
               ) : confirming && report ? (
                  <>
                     <div className={`${css.confirmNote} ${css.diag_warning}`}>
                        <div className={css.diagHeader}>
                           <div className={css.diagTitleGroup}>
                              <span className={css.diagLed} />
                              <span className={css.diagEyebrow}>
                                 ! CONFIRM // <T k="settings.repair.confirmTitle">These fixes need your OK</T>
                              </span>
                           </div>
                        </div>
                        <div className={css.diagMessage}>
                           <T k="settings.repair.confirmBody">
                              Some fixes download a file from Microsoft or only take effect the next time AMT opens.
                           </T>
                        </div>
                     </div>
                     <div className={`${css.checkList} panel`}>
                        {confirmItems.map((c) => (
                           <CheckRow key={c.id} check={c} />
                        ))}
                     </div>
                  </>
               ) : report ? (
                  <>
                     <div className={`${css.confirmNote} ${allHealthy ? "" : css.diag_warning}`}>
                        <div className={css.diagHeader}>
                           <div className={css.diagTitleGroup}>
                              <span className={css.diagLed} />
                              <span className={css.diagEyebrow}>! DIAGNOSTIC // RUNTIME &amp; ENVIRONMENT</span>
                           </div>
                        </div>
                        <div className={css.diagMessage}>
                           {allHealthy ? (
                              <T k="settings.repair.allHealthy">Everything checks out.</T>
                           ) : (
                              <T k="settings.repair.issuesFound">A few things could use a fix.</T>
                           )}
                        </div>
                     </div>
                     <div className={`${css.checkList} panel`}>
                        {report.checks.map((c) => (
                           <CheckRow key={c.id} check={c} />
                        ))}
                     </div>
                  </>
               ) : (
                  <div className={css.emptyStateView}>
                     <div className={css.emptyIcon}>
                        <WrenchIcon />
                     </div>
                     <span className={css.emptyTitle}>
                        <T k="settings.repair.subtitle">
                           Scans the WebView2 runtime and app folders for common problems and fixes what it can.
                        </T>
                     </span>
                     <button type="button" data-no-drag className={`${css.obBtn} ${css.primary}`} onClick={startScan}>
                        <T k="settings.repair.scan">Scan Now</T>
                     </button>
                  </div>
               )}
            </div>

            <div className={css.confirmActions}>
               <div className={css.footerMeta}>
                  {report?.scannedAt && !busy && !confirming && (
                     <span className={css.lastTestedText}>
                        TIMESTAMP: {new Date(report.scannedAt).toLocaleTimeString()}
                     </span>
                  )}
               </div>
               <div className={css.footerButtons}>
                  {confirming ? (
                     <>
                        <button
                           type="button"
                           data-no-drag
                           className={`${css.obBtn} ${css.ghost}`}
                           onClick={() => store.set({ repairConfirming: false })}
                        >
                           <T k="common.cancel">Cancel</T>
                        </button>
                        <button
                           type="button"
                           data-no-drag
                           className={`${css.obBtn} ${css.primary}`}
                           onClick={() => runFix(report?.fixableIds ?? [])}
                        >
                           <T k="settings.repair.repairAll">Repair All</T>
                        </button>
                     </>
                  ) : report && !busy ? (
                     <>
                        <button type="button" data-no-drag className={`${css.obBtn} ${css.ghost}`} onClick={startScan}>
                           <RetestIcon />
                           <span>
                              <T k="settings.repair.rescan">Rescan</T>
                           </span>
                        </button>
                        {report.fixableIds.length > 0 && (
                           <button type="button" data-no-drag className={`${css.obBtn} ${css.primary}`} onClick={repairAll}>
                              <T k="settings.repair.repairAll">Repair All</T>
                           </button>
                        )}
                        <button type="button" data-no-drag className={`${css.obBtn} ${css.ghost}`} onClick={close}>
                           <T k="settings.close">Close</T>
                        </button>
                     </>
                  ) : (
                     <button type="button" data-no-drag className={`${css.obBtn} ${css.ghost}`} onClick={close}>
                        <T k="settings.close">Close</T>
                     </button>
                  )}
               </div>
            </div>
         </div>
      </div>
   );
}
