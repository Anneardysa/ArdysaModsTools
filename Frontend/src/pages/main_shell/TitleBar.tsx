import { send } from "../../bridge/host";
import { useLocale } from "../../bridge/i18n";
import { BrandGlyph, BrandSprite } from "../../ui/BrandMark";
import css from "./shell.module.css";

export function TitleBar() {
   const { t } = useLocale();

   return (
      <header
         id="titlebar"
         className={css.titlebar}
         onMouseDown={(e) => {
            if ((e.target as HTMLElement).closest(`.${css.winBtn}`)) return;
            send("startDrag");
         }}
      >
         <BrandSprite />
         <div className={css.brand}>
            <span className={css.glyph}>
               <BrandGlyph height={17} />
            </span>
            <span className={css.bName}>ArdysaModsTools</span>
            <span className={css.bTag}>Dota 2</span>
         </div>
         <div className={css.winControls}>
            <button type="button" id="btn-about" className={`${css.winBtn} ${css.about}`} title={t("shell.titlebar.about", "About")} onClick={() => send("about")}>
               <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <circle cx="12" cy="12" r="10" />
                  <line x1="12" y1="11" x2="12" y2="16" />
                  <line x1="12" y1="8" x2="12.01" y2="8" />
               </svg>
            </button>
            <button type="button" id="btn-settings" className={`${css.winBtn} ${css.settings}`} title={t("shell.titlebar.settings", "Settings")} onClick={() => send("settings")}>
               <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <circle cx="12" cy="12" r="3" />
                  <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z" />
               </svg>
            </button>
            <button type="button" className={`${css.winBtn} ${css.min}`} title={t("shell.titlebar.minimize", "Minimize")} onClick={() => send("minimize")}>
               <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
                  <line x1="5" y1="12" x2="19" y2="12" />
               </svg>
            </button>
            <button type="button" className={`${css.winBtn} ${css.close}`} title={t("shell.titlebar.close", "Close")} onClick={() => send("close")}>
               <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
                  <line x1="6" y1="6" x2="18" y2="18" />
                  <line x1="18" y1="6" x2="6" y2="18" />
               </svg>
            </button>
         </div>
      </header>
   );
}
