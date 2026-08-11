import { useLocale } from "../../bridge/i18n";
import { hideCfgBanner, store } from "./store";

export function CfgBanner() {
   const { t } = useLocale();
   const cfgBanner = store.use((s) => s.cfgBanner);

   if (cfgBanner.state !== "warning" && cfgBanner.state !== "success") return null;
   const isSuccess = cfgBanner.state === "success";

   return (
      <div className={`cfg-banner show ${isSuccess ? "success" : "warning"}`}>
         <svg className="cfg-banner-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <path d="M10.3 3.3 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.3a2 2 0 0 0-3.4 0z" />
            <line x1="12" y1="9" x2="12" y2="13" />
            <line x1="12" y1="17" x2="12.01" y2="17" />
         </svg>
         <div className="cfg-banner-text">
            <div className="cfg-banner-title">{isSuccess ? t("perf.banner.ready", "autoexec.cfg ready") : t("perf.cfgNotFound", "No autoexec.cfg found")}</div>
            <div className="cfg-banner-msg">{cfgBanner.message}</div>
         </div>
         <button type="button" data-no-drag className="cfg-banner-x" title={t("perf.dismiss", "Dismiss")} aria-label={t("perf.dismiss", "Dismiss")} onClick={hideCfgBanner}>
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
               <line x1="6" y1="6" x2="18" y2="18" />
               <line x1="18" y1="6" x2="6" y2="18" />
            </svg>
         </button>
      </div>
   );
}
