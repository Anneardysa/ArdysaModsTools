import { T, translate } from "../../bridge/i18n";
import css from "./gallery.module.css";

export function CachingOverlay({ current, total }: { current: number; total: number }) {
   const pct = total > 0 ? Math.round((current / total) * 100) : 0;

   return (
      <div id="cachingOverlay" className={css.cachingOverlay}>
         <div className={css.coBox}>
            <div className={css.coIco}>
               <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <path d="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z" />
               </svg>
            </div>
            <h2 className={css.coTitle}>
               <T k="common.loadingAssets">Loading Assets</T>
            </h2>
            <p id="cachingStatus" className={css.cachingStatus}>{translate("common.preparingThumbs", "Preparing thumbnails...")}</p>
            <div className={css.coBar}>
               <div id="cachingBar" className={css.cachingBar} style={{ width: `${pct}%` }} />
            </div>
            <div className={css.coMeta}>
               <span id="cachingCount">
                  {current} / {total}
               </span>
               <span id="cachingPercent">{pct}%</span>
            </div>
         </div>
      </div>
   );
}
