import { useLocale } from "../../bridge/i18n";
import { Icon } from "./icons";
import { hideCfgBanner, store } from "./store";

export function CfgBanner() {
   const { t } = useLocale();
   const cfgBanner = store.use((s) => s.cfgBanner);

   if (cfgBanner.state !== "warning" && cfgBanner.state !== "success") return null;
   const isSuccess = cfgBanner.state === "success";

   return (
      <div className={`cfg-banner show ${isSuccess ? "success" : "warning"}`} role="status">
         <Icon name={isSuccess ? "check_circle" : "warning"} size={20} className="cfg-banner-ico" />
         <div className="cfg-banner-text">
            <div className="cfg-banner-title">{isSuccess ? t("perf.banner.ready", "autoexec.cfg ready") : t("perf.cfgNotFound", "No autoexec.cfg found")}</div>
            <div className="cfg-banner-msg">{cfgBanner.message}</div>
         </div>
         <button type="button" data-no-drag className="cfg-banner-x" title={t("perf.dismiss", "Dismiss")} aria-label={t("perf.dismiss", "Dismiss")} onClick={hideCfgBanner}>
            <Icon name="x_close" size={13} />
         </button>
      </div>
   );
}
