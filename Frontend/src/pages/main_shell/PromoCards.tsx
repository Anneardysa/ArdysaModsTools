import { send } from "../../bridge/host";
import { T } from "../../bridge/i18n";
import { BrandGlyph } from "../../ui/BrandMark";
import { openNewsModal, store } from "./store";
import css from "./shell.module.css";

export function PromoCards() {
   const installCardUrl = store.use((s) => s.installCardUrl);
   const newsAppVersion = store.use((s) => s.newsAppVersion);
   const newsModspackVersion = store.use((s) => s.newsModspackVersion);
   const installEnabled = store.use((s) => s.buttons.install !== false);

   return (
      <div className={css.cards}>
         <button
            id="btn-install"
            type="button"
            data-no-drag
            className={`${css.promoCard} ${css.install} ${!installEnabled ? css.isDisabled : ""}`}
            disabled={!installEnabled}
            onClick={() => installEnabled && send("install")}
         >
            <img
               id="install-card-bg"
               className={css.bg}
               alt=""
               src={installCardUrl ?? undefined}
               onError={(e) => {
                  e.currentTarget.style.display = "none";
               }}
            />
            <span className={css.veil} />
            <span className={css.pcBody}>
               <span className={css.pcTop}>
                  <span className={css.pcIco}>
                     <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <path d="M12 3v12" />
                        <polyline points="7 11 12 16 17 11" />
                        <path d="M5 20h14" />
                     </svg>
                  </span>
                  <svg className={css.pcArrow} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <polyline points="9 6 15 12 9 18" />
                  </svg>
               </span>
               <span className={css.pcText}>
                  <span className={css.pcTitle}>
                     <T k="shell.card.install.title">Install ModsPack</T>
                  </span>
                  <span className={css.pcSub}>
                     <T k="shell.card.install.sub">Apply the latest skins &amp; mods to Dota&nbsp;2</T>
                  </span>
               </span>
            </span>
         </button>

         <button id="btn-news" type="button" data-no-drag className={`${css.promoCard} ${css.news}`} onClick={openNewsModal}>
            <span className={css.veil} />
            <span className={css.pcBody}>
               <span className={css.pcTop}>
                  <span className={css.pcIco}>
                     <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <path d="M4 5h13a2 2 0 0 1 2 2v11a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2z" />
                        <line x1="7" y1="9" x2="14" y2="9" />
                        <line x1="7" y1="13" x2="14" y2="13" />
                        <line x1="7" y1="17" x2="11" y2="17" />
                     </svg>
                  </span>
                  <svg className={`${css.pcArrow} ${css.chev}`} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <polyline points="6 9 12 15 18 9" />
                  </svg>
               </span>
               <BrandGlyph height={66} className={css.pcLogo} />
               <span className={css.pcText}>
                  <span className={css.pcTitle}>
                     <T k="shell.card.news.title">What's New</T>
                  </span>
                  <span className={css.pcSub}>
                     <T k="shell.card.news.sub">Changelog &amp; ModsPack — latest releases</T>
                  </span>
                  <span className={css.pcVers}>
                     <span id="news-app-ver" className={css.pcVer}>
                        {newsAppVersion}
                     </span>
                     <span id="news-mods-ver" className={`${css.pcVer} ${css.mods}`}>
                        {newsModspackVersion}
                     </span>
                  </span>
               </span>
            </span>
         </button>
      </div>
   );
}
