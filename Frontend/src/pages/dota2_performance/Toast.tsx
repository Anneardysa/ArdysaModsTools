import { useLocale } from "../../bridge/i18n";
import { Icon, type IconName } from "./icons";
import { store } from "./store";
import type { ToastType } from "./types";

const TOAST_ICONS: Record<ToastType, IconName> = {
   success: "check_circle",
   error: "disabled",
   warning: "warning",
   info: "info",
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
      <div id="toast" className={`toast ${toast.type} ${toast.visible ? "show" : ""}`} role="status" aria-live="polite">
         <Icon name={TOAST_ICONS[toast.type]} size={20} className="toast-ico" />
         <div className="toast-body">
            <div className="toast-title">{titles[toast.type]}</div>
            <div className="toast-msg">{message}</div>
         </div>
      </div>
   );
}
