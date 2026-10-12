import { useLocale } from "../../bridge/i18n";
import type { DetectionState } from "./types";
import css from "./gallery.module.css";

export function DetectionBadge({ detection }: { detection: DetectionState }) {
   const { t } = useLocale();
   const scanning = detection.phase === "scanning";
   const failed = detection.phase === "failed";
   const count = detection.phase === "done" ? detection.count : 0;
   const found = detection.phase === "done" && detection.found;

   const description = scanning
      ? t("hero.detection.scanning", "Checking installed skins…")
      : failed
      ? t("hero.detection.failed", "Could not read the installed ModsPack. Your picks still generate.")
      : found
      ? t("hero.detection.found", `Detected ${count} installed skin(s), reselected below.`, { count })
      : t("hero.detection.none", "No skins from the Skin Selector are installed.");

   const label = scanning
      ? t("hero.detection.badge.scanning", "Scanning ModsPack")
      : failed
      ? t("hero.detection.badge.failed", "Scan failed")
      : t("hero.detection.badge.label", "Detected in ModsPack");

   return (
      <div
         className={`${css.detectBadge} ${scanning ? css.detectScanning : failed ? css.detectFailed : found ? css.detectFound : ""}`}
         role="status"
         aria-busy={scanning}
         aria-label={description}
         title={description}
      >
         <span className={css.detectDot} aria-hidden="true" />
         {!scanning && !failed && <span className={css.detectValue}>{count}</span>}
         <span>{label}</span>
      </div>
   );
}
