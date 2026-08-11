import { send } from "../../bridge/host";
import { T, useLocale } from "../../bridge/i18n";
import { closeElevationModal, store } from "./store";
import css from "./shell.module.css";

export function ElevationModal() {
   const { t } = useLocale();
   const open = store.use((s) => s.elevationModalOpen);
   const detail = store.use((s) => s.elevationDetail);
   const found = store.use((s) => s.elevationFound);
   const canFix = store.use((s) => s.elevationCanFix);
   const fixLabel = store.use((s) => s.elevationFixLabel);
   const rechecking = store.use((s) => s.elevationRechecking);
   if (!open) return null;

   const fix = () => {
      closeElevationModal();
      send("fixSetup");
   };

   return (
      <div
         id="elevation-modal"
         className={`${css.modalOverlay} ${css.show}`}
         onClick={(e) => {
            if (e.target === e.currentTarget) closeElevationModal();
         }}
      >
         <div className={`${css.modal} ${css.confirm}`} role="alertdialog" aria-modal="true" aria-labelledby="elevation-heading">
            <div className={css.modalHead}>
               <div className={css.modalTitle}>
                  <span className={css.led} />
                  <span>
                     <T k="verify.chip.admin">Process Elevation</T>
                  </span>
               </div>
               <button type="button" data-no-drag className={css.modalX} title={t("shell.titlebar.close", "Close")} onClick={closeElevationModal}>
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
                     <line x1="6" y1="6" x2="18" y2="18" />
                     <line x1="18" y1="6" x2="6" y2="18" />
                  </svg>
               </button>
            </div>
            <div className={css.confirmBody}>
               <span className={`${css.confirmIco} ${css.elevIco} ${found ? css.found : ""}`}>
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <path d="M10.3 3.3 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.3a2 2 0 0 0-3.4 0z" />
                     <line x1="12" y1="9" x2="12" y2="13" />
                     <line x1="12" y1="17" x2="12.01" y2="17" />
                  </svg>
               </span>
               <div id="elevation-heading" className={css.confirmHeading}>
                  <T k="verify.elevation.heading">Do not run Steam or Dota 2 as administrator</T>
               </div>
               <div className={css.confirmText}>
                  <T k="verify.elevation.body">
                     With administrator rights the game cannot find matches, and it stops reading the files AMT patched — so your mods stop working. AMT cannot detect every way this happens, so check it yourself even when nothing is listed below.
                  </T>
               </div>
               {detail && (
                  <div id="elevation-detected" className={`${css.elevDetected} ${found ? css.found : ""} ${rechecking ? css.rechecking : ""}`}>
                     {detail}
                  </div>
               )}
               <div className={css.elevSteps}>
                  <div className={css.elevStepsTitle}>
                     <T k="verify.elevation.stepsTitle">How to fix it</T>
                  </div>
                  <ol>
                     <li>
                        <T k="verify.elevation.step1">Close Dota 2 and fully exit Steam from the system tray.</T>
                     </li>
                     <li>
                        <T k="verify.elevation.step2">Right-click steam.exe and dota2.exe, open Properties › Compatibility, and untick "Run this program as an administrator".</T>
                     </li>
                     <li>
                        <T k="verify.elevation.step3">Start Steam normally — do not use "Run as administrator".</T>
                     </li>
                     <li>
                        <T k="verify.elevation.step4">Back in AMT, refresh the status to confirm.</T>
                     </li>
                  </ol>
               </div>
            </div>
            <div className={css.confirmActions}>
               {canFix && (
                  <button id="elevation-fix" type="button" data-no-drag className={css.verifyFix} onClick={fix}>
                     {fixLabel}
                  </button>
               )}
               <button type="button" data-no-drag className={`${css.obBtn} ${css.primary}`} onClick={closeElevationModal}>
                  <T k="common.close">Close</T>
               </button>
            </div>
         </div>
      </div>
   );
}
