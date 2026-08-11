import { T, useLocale } from "../../bridge/i18n";
import { resolveConfirm, store } from "./store";
import css from "./gallery.module.css";

const WARNING_PATH = "M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z";

export function ConfirmModal() {
   const { t } = useLocale();
   const confirm = store.use((s) => s.confirm);
   if (!confirm.visible) return null;

   return (
      <div id="confirmModal" className={css.ov}>
         <div className={css.ovScrim} onClick={() => resolveConfirm(false)} />
         <div className={css.dialog}>
            <div className={css.dialogBody}>
               <div className={css.dialogIco}>
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <path d={WARNING_PATH} />
                  </svg>
               </div>
               <div className={css.dialogMain}>
                  <h3 id="confirmTitle" className={css.dialogTitle}>
                     {confirm.kind === "clearAll" ? "Clear All Selections" : confirm.title}
                  </h3>
                  {confirm.kind === "clearAll" ? (
                     <div id="confirmMessage" className={css.dialogMsg}>
                        This will reset all <strong>{confirm.count}</strong> hero selection(s) to default.
                     </div>
                  ) : (
                     <div id="confirmMessage" className={css.dialogMsg} dangerouslySetInnerHTML={{ __html: confirm.message }} />
                  )}
                  {confirm.kind === "clearAll" && confirm.items.length > 0 && (
                     <div id="confirmSelectionList" className={css.csList}>
                        {confirm.items.map((item, i) => (
                           <div key={i} className={css.csRow}>
                              <img
                                 src={item.thumbnail}
                                 alt={item.heroName}
                                 onError={(e) => {
                                    e.currentTarget.src =
                                       "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 32 32'%3E%3Crect fill='%23222' width='32' height='32'/%3E%3C/svg%3E";
                                 }}
                              />
                              <div className={css.csInfo}>
                                 <div className={css.csName}>{item.heroName}</div>
                                 <div className={css.csSet}>{item.setName}</div>
                              </div>
                           </div>
                        ))}
                     </div>
                  )}
               </div>
            </div>
            <div className={css.dialogActions}>
               <button type="button" data-no-drag className={`${css.btn} ${css.ghost}`} onClick={() => resolveConfirm(false)}>
                  {t("common.cancel", "Cancel")}
               </button>
               <button type="button" data-no-drag className={`${css.btn} ${css.primary}`} onClick={() => resolveConfirm(true)}>
                  {confirm.kind === "clearAll" ? <T k="heroGallery.clearConfirm">Yes, Clear All</T> : "Continue"}
               </button>
            </div>
         </div>
      </div>
   );
}
