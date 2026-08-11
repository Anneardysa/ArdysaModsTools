import type { ReactNode } from "react";
import { useLocale } from "../../bridge/i18n";
import { store } from "./store";
import type { ToastType } from "./types";

const TOAST_ICON_PATHS: Record<ToastType, ReactNode> = {
   success: <path d="M20 6 9 17l-5-5" />,
   error: (
      <>
         <circle cx="12" cy="12" r="10" />
         <line x1="15" y1="9" x2="9" y2="15" />
         <line x1="9" y1="9" x2="15" y2="15" />
      </>
   ),
   warning: (
      <>
         <path d="M10.3 3.3 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.3a2 2 0 0 0-3.4 0z" />
         <line x1="12" y1="9" x2="12" y2="13" />
         <line x1="12" y1="17" x2="12.01" y2="17" />
      </>
   ),
   info: (
      <>
         <circle cx="12" cy="12" r="10" />
         <line x1="12" y1="16" x2="12" y2="12" />
         <line x1="12" y1="8" x2="12.01" y2="8" />
      </>
   ),
};

export function Toast() {
   const { t } = useLocale();
   const toast = store.use((s) => s.toast);

   const titles: Record<ToastType, string> = {
      success: t("common.success", "Success"),
      error: t("common.error", "Failed"),
      warning: t("common.warning", "Warning"),
      info: t("common.information", "Notice"),
   };

   const message = toast.message.replace(/^\/\/\s*/, "");

   return (
      <div id="toast" className={`toast ${toast.type} ${toast.visible ? "show" : ""}`}>
         <span className="toast-ico">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
               {TOAST_ICON_PATHS[toast.type]}
            </svg>
         </span>
         <div className="toast-body">
            <div className="toast-title">{titles[toast.type]}</div>
            <div className="toast-msg">{message}</div>
         </div>
      </div>
   );
}
