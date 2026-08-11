import { send, startDragUnlessInteractive } from "../../bridge/host";
import { T, useLocale } from "../../bridge/i18n";
import { BrandGlyph, BrandSprite } from "../../ui/BrandMark";
import css from "./misc.module.css";

export function TitleBar() {
   const { t } = useLocale();

   return (
      <div id="titleBar" className={css.titleBar} onMouseDown={startDragUnlessInteractive}>
         <BrandSprite />
         <div className={css.tbBrand}>
            <BrandGlyph height={16} />
            <span className={css.bName}>
               <T k="shell.nav.miscellaneous">Miscellaneous</T>
            </span>
            <span className={css.bTag}>
               <T k="miscForm.tag">Custom Options</T>
            </span>
         </div>
         <button
            type="button"
            data-no-drag
            className={css.tbClose}
            onClick={() => send("close")}
            title={t("common.close", "Close")}
            aria-label={t("common.close", "Close")}
         >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
               <path d="M6 18L18 6M6 6l12 12" />
            </svg>
         </button>
      </div>
   );
}
