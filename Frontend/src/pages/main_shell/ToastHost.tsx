import { useEffect, useRef, useState } from "react";
import type { ReactNode } from "react";
import { useLocale } from "../../bridge/i18n";
import { dismissToast, store, type Toast } from "./store";
import css from "./shell.module.css";

const TOAST_ICON_PATHS: Record<Toast["variant"], ReactNode> = {
   success: <path d="M20 6 9 17l-5-5" />,
   error: (
      <>
         <circle cx="12" cy="12" r="10" />
         <line x1="12" y1="8" x2="12" y2="13" />
         <line x1="12" y1="16" x2="12.01" y2="16" />
      </>
   ),
   info: (
      <>
         <circle cx="12" cy="12" r="10" />
         <line x1="12" y1="11" x2="12" y2="16" />
         <line x1="12" y1="8" x2="12.01" y2="8" />
      </>
   ),
};

const HIDE_ANIM_MS = 220;

function ToastCard({ toast }: { toast: Toast }) {
   const { t } = useLocale();
   const [hiding, setHiding] = useState(false);
   const timerRef = useRef<number | null>(null);
   const timeout = toast.timeout > 0 ? toast.timeout : 4000;

   const dismiss = () => {
      if (timerRef.current != null) window.clearTimeout(timerRef.current);
      setHiding(true);
      window.setTimeout(() => dismissToast(toast.id), HIDE_ANIM_MS);
   };

   useEffect(() => {
      timerRef.current = window.setTimeout(dismiss, timeout);
      return () => {
         if (timerRef.current != null) window.clearTimeout(timerRef.current);
      };
      // eslint-disable-next-line react-hooks/exhaustive-deps
   }, [toast.id]);

   return (
      <div
         className={`${css.toast} ${css[toast.variant] ?? ""} ${hiding ? css.hide : ""}`}
         onMouseEnter={() => {
            if (timerRef.current != null) {
               window.clearTimeout(timerRef.current);
               timerRef.current = null;
            }
         }}
         onMouseLeave={() => {
            if (!hiding) timerRef.current = window.setTimeout(dismiss, timeout);
         }}
      >
         <span className={css.toastIco}>
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
               {TOAST_ICON_PATHS[toast.variant]}
            </svg>
         </span>
         <div className={css.toastBody}>
            {toast.title && <div className={css.toastTitle}>{toast.title}</div>}
            {toast.message && <div className={css.toastMsg}>{toast.message}</div>}
         </div>
         <button type="button" data-no-drag className={css.toastX} title={t("shell.toast.dismiss", "Dismiss")} onClick={dismiss}>
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" aria-hidden="true">
               <line x1="6" y1="6" x2="18" y2="18" />
               <line x1="18" y1="6" x2="6" y2="18" />
            </svg>
         </button>
      </div>
   );
}

export function ToastHost() {
   const toasts = store.use((s) => s.toasts);
   if (toasts.length === 0) return null;
   return (
      <div id="toast-host" className={css.toastHost} aria-live="polite">
         {toasts.map((t) => (
            <ToastCard key={t.id} toast={t} />
         ))}
      </div>
   );
}
