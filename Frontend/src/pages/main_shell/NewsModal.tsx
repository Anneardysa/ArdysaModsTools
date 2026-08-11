import { send } from "../../bridge/host";
import { T, useLocale } from "../../bridge/i18n";
import { closeNewsModal, store } from "./store";
import css from "./shell.module.css";

export function NewsModal() {
   const { t } = useLocale();
   const open = store.use((s) => s.newsModalOpen);
   const newsAppVersion = store.use((s) => s.newsAppVersion);
   const newsModspackVersion = store.use((s) => s.newsModspackVersion);
   if (!open) return null;

   const choose = (type: "whatsnew" | "modspackUpdates") => {
      closeNewsModal();
      send(type);
   };

   return (
      <div
         id="news-modal"
         className={`${css.modalOverlay} ${css.show}`}
         onClick={(e) => {
            if (e.target === e.currentTarget) closeNewsModal();
         }}
      >
         <div className={css.modal} role="dialog" aria-modal="true" aria-label="What's New">
            <div className={css.modalHead}>
               <div className={css.modalTitle}>
                  <span className={css.led} />
                  <span>
                     <T k="shell.news.modalTitle">What's New</T>
                  </span>
               </div>
               <button type="button" data-no-drag className={css.modalX} title={t("shell.titlebar.close", "Close")} onClick={closeNewsModal}>
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
                     <line x1="6" y1="6" x2="18" y2="18" />
                     <line x1="18" y1="6" x2="6" y2="18" />
                  </svg>
               </button>
            </div>
            <div className={css.modalSub}>
               <T k="shell.news.modalSub">Choose what you'd like to view</T>
            </div>
            <div className={css.choiceGrid}>
               <button type="button" data-no-drag className={css.choice} onClick={() => choose("whatsnew")}>
                  <span className={css.choiceIco}>
                     <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <path d="M4 5h13a2 2 0 0 1 2 2v11a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2z" />
                        <line x1="7" y1="9" x2="14" y2="9" />
                        <line x1="7" y1="13" x2="14" y2="13" />
                        <line x1="7" y1="17" x2="11" y2="17" />
                     </svg>
                  </span>
                  <span className={css.choiceTitle}>
                     <T k="shell.news.changelog.title">Changelog</T>
                  </span>
                  <span className={css.choiceSub}>
                     <T k="shell.news.changelog.sub">App release notes, fixes &amp; new features</T>
                  </span>
                  <span id="modal-app-ver" className={css.choiceVer}>
                     {newsAppVersion}
                  </span>
               </button>
               <button type="button" data-no-drag className={css.choice} onClick={() => choose("modspackUpdates")}>
                  <span className={css.choiceIco}>
                     <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <path d="M12 2l2.4 5 5.6.8-4 4 1 5.5L12 19.8 6.9 22.3l1-5.5-4-4 5.6-.8z" />
                     </svg>
                  </span>
                  <span className={css.choiceTitle}>
                     <T k="shell.news.modspack.title">ModsPack</T>
                  </span>
                  <span className={css.choiceSub}>
                     <T k="shell.news.modspack.sub">Latest hero skins &amp; cosmetic sets</T>
                  </span>
                  <span id="modal-mods-ver" className={`${css.choiceVer} ${css.mods}`}>
                     {newsModspackVersion}
                  </span>
               </button>
            </div>
         </div>
      </div>
   );
}
