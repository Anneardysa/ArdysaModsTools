import { useState } from "react";
import { send } from "../../bridge/host";
import { T, useLocale } from "../../bridge/i18n";
import { closeSyncModal, openSyncDetailsModal, pushToast, store } from "./store";
import css from "./shell.module.css";

export function SyncModal() {
   const { t } = useLocale();
   const open = store.use((s) => s.syncModalOpen);
   const syncDetail = store.use((s) => s.syncDetail);
   const syncNeedsFix = store.use((s) => s.syncNeedsFix);
   const [copiedDetail, setCopiedDetail] = useState(false);
   if (!open) return null;

   const fix = () => {
      closeSyncModal();
      send("fixPackageSync");
   };

   const copySyncDetail = (e: React.MouseEvent) => {
      e.stopPropagation();
      if (!syncDetail) return;
      send("copyConsole", { text: syncDetail });
      try {
         if (navigator?.clipboard?.writeText) {
            navigator.clipboard.writeText(syncDetail).catch(() => {});
         }
      } catch {}
      setCopiedDetail(true);
      pushToast({
         title: t("shell.toast.copied", "Copied"),
         message: t("verify.sync.modal.copiedNotice", "Sync status copied to clipboard."),
         variant: "info",
         timeout: 2000,
      });
      setTimeout(() => setCopiedDetail(false), 2000);
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
               <span className={`${css.syncHeroIco} ${syncNeedsFix ? css.outOfSync : css.inSync}`}>
                  {syncNeedsFix ? (
                     <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <path d="M21 12a9 9 0 1 1-3-6.7L21 8" />
                        <path d="M21 3v5h-5" />
                     </svg>
                  ) : (
                     <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14" />
                        <polyline points="22 4 12 14.01 9 11.01" />
                     </svg>
                  )}
               </span>
               <div id="sync-heading" className={css.confirmHeading}>
                  {syncNeedsFix ? (
                     <T k="verify.sync.modal.heading">Your mod package is older than the game</T>
                  ) : (
                     <T k="verify.sync.modal.inSyncHeading">Mod package is in sync with Dota 2</T>
                  )}
               </div>
               <div className={css.confirmText}>
                  {syncNeedsFix ? (
                     <T k="verify.sync.modal.body">
                        Your mod package carries its own copy of Dota 2's package. When an update changes that data, the package keeps serving the old version and the game crashes on launch.
                     </T>
                  ) : (
                     <T k="verify.sync.modal.inSyncBody">
                        Your installed mod package matches the current Dota 2 game version and item definitions.
                     </T>
                  )}
               </div>

               <div className={`${css.syncStatusCard} ${syncNeedsFix ? css.outOfSync : css.inSync}`}>
                  <div className={css.syncStatusCardHead}>
                     <span className={`${css.syncStatusPill} ${syncNeedsFix ? css.pillWarn : css.pillPass}`}>
                        <span className={css.led} />
                        {syncNeedsFix ? "Out of Sync" : "Package Up to Date"}
                     </span>
                     {syncDetail && (
                        <button
                           type="button"
                           data-no-drag
                           className={`${css.syncCardCopyBtn} ${copiedDetail ? css.copied : ""}`}
                           title={t("verify.sync.copyDetail", "Copy sync details")}
                           onClick={copySyncDetail}
                        >
                           {copiedDetail ? (
                              <>
                                 <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ width: 11, height: 11 }}>
                                    <polyline points="20 6 9 17 4 12" />
                                 </svg>
                                 <span>{t("shell.toast.copied", "Copied")}</span>
                              </>
                           ) : (
                              <>
                                 <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ width: 11, height: 11 }}>
                                    <rect x="9" y="9" width="13" height="13" rx="2" ry="2" />
                                    <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1" />
                                 </svg>
                                 <span>{t("common.copy", "Copy")}</span>
                              </>
                           )}
                        </button>
                     )}
                  </div>
                  <div id="sync-detail" className={css.syncStatusCardText}>
                     {syncDetail}
                  </div>
               </div>

               {syncNeedsFix && (
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
               )}
            </div>
            <div className={css.confirmActions}>
               <button
                  id="btn-sync-view-details"
                  type="button"
                  data-no-drag
                  className={`${css.obBtn} ${css.ghost}`}
                  onClick={() => openSyncDetailsModal()}
                  style={{ display: "inline-flex", alignItems: "center", justifyContent: "center", gap: 6 }}
               >
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" style={{ width: 14, height: 14 }}>
                     <path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z" />
                     <circle cx="12" cy="12" r="3" />
                  </svg>
                  <span>
                     <T k="verify.sync.modal.viewDetails">View Item Diff & Details</T>
                  </span>
               </button>
               {syncNeedsFix ? (
                  <button id="sync-fix" type="button" data-no-drag className={css.verifyFix} onClick={fix}>
                     <T k="verify.sync.modal.fix">Rebuild Package</T>
                  </button>
               ) : null}
               <button
                  type="button"
                  data-no-drag
                  className={`${css.obBtn} ${syncNeedsFix ? css.ghost : css.primary}`}
                  onClick={closeSyncModal}
               >
                  <T k="common.close">Close</T>
               </button>
            </div>
         </div>
      </div>
   );
}
