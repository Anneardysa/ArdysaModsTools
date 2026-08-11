import { send, startDragUnlessInteractive } from "../../bridge/host";
import { T, useLocale } from "../../bridge/i18n";
import { BrandGlyph, BrandSprite } from "../../ui/BrandMark";
import css from "./gallery.module.css";

const MARQUEE_ICON = "M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z";

function MarqueeItem() {
   return (
      <span className={css.tbMarqueeItem}>
         <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <path d={MARQUEE_ICON} />
         </svg>
         <T k="heroGallery.marquee">Not all heroes have a set — sets will be updated gradually</T>
         <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <path d={MARQUEE_ICON} />
         </svg>
      </span>
   );
}

export function TitleBar() {
   const { t } = useLocale();

   return (
      <div id="titleBar" className={css.titleBar} onMouseDown={startDragUnlessInteractive}>
         <BrandSprite />
         <div className={css.tbBrand}>
            <BrandGlyph height={16} />
            <span className={css.bName}>
               <T k="shell.nav.skinSelector">Skin Selector</T>
            </span>
            <span className={css.bTag}>
               <T k="heroGallery.tag">Beta</T>
            </span>
         </div>

         <div className={css.tbMarquee} aria-hidden="true">
            <div className={css.tbMarqueeInner}>
               <div className={css.tbMarqueeTrack}>
                  <MarqueeItem />
                  <MarqueeItem />
               </div>
            </div>
         </div>

         <button type="button" data-no-drag className={css.tbClose} onClick={() => send("close")} title={t("common.close", "Close")} aria-label={t("common.close", "Close")}>
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
               <path d="M6 18L18 6M6 6l12 12" />
            </svg>
         </button>
      </div>
   );
}
