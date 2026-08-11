import { useEffect, useRef } from "react";
import { send } from "../../bridge/host";
import { closeConfirmModal, store, tickConfirmCountdown } from "./store";
import css from "./shell.module.css";

export function resolveShellConfirm(ok: boolean) {
   const s = store.get();
   if (!s.confirmModal) return;
   if (ok && s.confirmCountdown > 0) return;
   const id = s.confirmModal.id;
   closeConfirmModal();
   send("shellModalResult", { id, ok });
}

export function ConfirmModal() {
   const payload = store.use((s) => s.confirmModal);
   const remaining = store.use((s) => s.confirmCountdown);
   const okRef = useRef<HTMLButtonElement>(null);
   const cancelRef = useRef<HTMLButtonElement>(null);

   useEffect(() => {
      if (!payload) return;
      (payload.countdown > 0 ? cancelRef.current : okRef.current)?.focus();
   }, [payload?.id]); // eslint-disable-line react-hooks/exhaustive-deps

   useEffect(() => {
      if (remaining <= 0) return;
      const id = window.setTimeout(tickConfirmCountdown, 1000);
      return () => window.clearTimeout(id);
   }, [remaining]);

   if (!payload) return null;
   const locked = remaining > 0;

   const resolve = (ok: boolean) => resolveShellConfirm(ok);

   return (
      <div
         id="confirm-modal"
         className={`${css.modalOverlay} ${css.show}`}
         onClick={(e) => {
            if (e.target === e.currentTarget) resolve(false);
         }}
      >
         <div className={`${css.modal} ${css.confirm} ${payload.accent === "warn" ? css.confirmWarn : ""}`} role="alertdialog" aria-modal="true" aria-labelledby="confirm-heading">
            <div className={css.modalHead}>
               <div className={css.modalTitle}>
                  <span className={css.led} />
                  <span id="confirm-eyebrow">{payload.eyebrow || "Confirm"}</span>
               </div>
               <button type="button" data-no-drag className={css.modalX} onClick={() => resolve(false)}>
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
                     <line x1="6" y1="6" x2="18" y2="18" />
                     <line x1="18" y1="6" x2="6" y2="18" />
                  </svg>
               </button>
            </div>
            <div className={css.confirmBody}>
               <span className={css.confirmIco}>
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <path d="M10.3 3.3 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.3a2 2 0 0 0-3.4 0z" />
                     <line x1="12" y1="9" x2="12" y2="13" />
                     <line x1="12" y1="17" x2="12.01" y2="17" />
                  </svg>
               </span>
               <div id="confirm-heading" className={css.confirmHeading}>
                  {payload.heading}
               </div>
               <div id="confirm-text" className={css.confirmText}>
                  {payload.body}
               </div>
               {payload.note && (
                  <div id="confirm-note" className={css.confirmNote}>
                     {payload.note}
                  </div>
               )}
            </div>
            <div className={css.confirmActions}>
               <button ref={cancelRef} id="confirm-cancel" type="button" data-no-drag className={`${css.obBtn} ${css.ghost}`} onClick={() => resolve(false)}>
                  {payload.cancelText || "Cancel"}
               </button>
               <button ref={okRef} id="confirm-ok" type="button" data-no-drag disabled={locked} className={`${css.obBtn} ${css.primary}`} onClick={() => resolve(true)}>
                  {locked ? `${payload.confirmText || "Continue"} (${remaining})` : payload.confirmText || "Continue"}
               </button>
            </div>
         </div>
      </div>
   );
}
