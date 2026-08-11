import { send } from "../../bridge/host";
import { store } from "./store";
import css from "./shell.module.css";

export function LaunchModal() {
   const panel = store.use((s) => s.launchPanel);
   if (!panel) return null;

   const asking = !!panel.confirmLabel;
   const hasPercent = typeof panel.percent === "number" && panel.percent >= 0;
   const showBar = hasPercent && !panel.isError;

   return (
      <div id="launch-modal" className={`${css.modalOverlay} ${css.show}`}>
         <div className={css.modal} role="alertdialog" aria-modal="true" aria-labelledby="launch-heading">
            <div className={css.modalHead}>
               <div className={css.modalTitle}>
                  <span className={css.led} />
                  <span id="launch-heading">{panel.heading}</span>
               </div>
            </div>
            <div className={css.confirmBody}>
               {!asking && <span id="launch-spinner" className={`${css.launchSpinner} ${panel.isError ? css.isError : ""}`} aria-hidden="true" />}
               <div id="launch-detail" className={css.confirmText}>
                  {panel.detail}
               </div>
               {showBar && (
                  <div id="launch-bar" className={`${css.launchBar} ${css.show}`}>
                     <span style={{ width: `${Math.min(100, Math.max(0, panel.percent as number))}%` }} />
                  </div>
               )}
            </div>
            <div className={css.confirmActions}>
               {asking && (
                  <button id="launch-confirm" type="button" data-no-drag className={`${css.obBtn} ${css.primary}`} onClick={() => send("confirmLaunch")}>
                     {panel.confirmLabel}
                  </button>
               )}
               {panel.canCancel && (
                  <button id="launch-cancel" type="button" data-no-drag className={`${css.obBtn} ${css.ghost}`} onClick={() => send("cancelLaunch")}>
                     {panel.cancelLabel}
                  </button>
               )}
            </div>
         </div>
      </div>
   );
}
