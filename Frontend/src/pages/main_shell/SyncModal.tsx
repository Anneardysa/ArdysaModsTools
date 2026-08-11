import { send } from "../../bridge/host";
import { T, useLocale } from "../../bridge/i18n";
import { closeSyncModal, store } from "./store";
import css from "./shell.module.css";

export function SyncModal() {
   const { t } = useLocale();
   const open = store.use((s) => s.syncModalOpen);
   const syncDetail = store.use((s) => s.syncDetail);
   const syncNeedsFix = store.use((s) => s.syncNeedsFix);
   if (!open) return null;

   const fix = () => {
      closeSyncModal();
      send("fixPackageSync");
   };

   return (
      <div
         id="sync-modal"
         className={`${css.modalOverlay} ${css.show}`}
         onClick={(e) => {
            if (e.target === e.currentTarget) closeSyncModal();
         }}
      >
         <div className={`${css.modal} ${css.confirm}`} role="alertdialog" aria-modal="true" aria-labelledby="sync-heading">
            <div className={css.modalHead}>
               <div className={css.modalTitle}>
                  <span className={css.led} />
                  <span>
                     <T k="verify.chip.sync">Package Sync</T>
                  </span>
               </div>
               <button type="button" data-no-drag className={css.modalX} title={t("shell.titlebar.close", "Close")} onClick={closeSyncModal}>
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
                     <line x1="6" y1="6" x2="18" y2="18" />
                     <line x1="18" y1="6" x2="6" y2="18" />
                  </svg>
               </button>
            </div>
            <div className={css.confirmBody}>
               <span className={`${css.confirmIco} ${css.elevIco}`}>
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <path d="M21 12a9 9 0 1 1-3-6.7L21 8" />
                     <path d="M21 3v5h-5" />
                  </svg>
               </span>
               <div id="sync-heading" className={css.confirmHeading}>
                  <T k="verify.sync.modal.heading">Your mod package is older than the game</T>
               </div>
               <div className={css.confirmText}>
                  <T k="verify.sync.modal.body">
                     Your mod package carries its own copy of Dota 2's package. When an update changes that data, the package keeps serving the old version and the game crashes on launch.
                  </T>
               </div>
               <div id="sync-detail" className={`${css.elevDetected} ${css.found}`}>
                  {syncDetail}
               </div>
               <div className={css.elevSteps}>
                  <div className={css.elevStepsTitle}>
                     <T k="verify.sync.modal.stepsTitle">What the repair does</T>
                  </div>
                  <ol>
                     <li>
                        <T k="verify.sync.modal.step1">Reads the package from Dota 2 folder.</T>
                     </li>
                     <li>
                        <T k="verify.sync.modal.step2">Rebuilds your package on top of it, keeping your mods and everything the update added.</T>
                     </li>
                     <li>
                        <T k="verify.sync.modal.step3">Reinstalls the package. This takes a few minutes and nothing changes until it succeeds.</T>
                     </li>
                  </ol>
               </div>
            </div>
            <div className={css.confirmActions}>
               {syncNeedsFix && (
                  <button id="sync-fix" type="button" data-no-drag className={css.verifyFix} onClick={fix}>
                     <T k="verify.sync.modal.fix">Rebuild Package</T>
                  </button>
               )}
               <button type="button" data-no-drag className={`${css.obBtn} ${css.ghost}`} onClick={closeSyncModal}>
                  <T k="common.close">Close</T>
               </button>
            </div>
         </div>
      </div>
   );
}
