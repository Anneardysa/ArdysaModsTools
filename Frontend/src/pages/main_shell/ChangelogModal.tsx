import { useEffect, useRef } from "react";
import { send } from "../../bridge/host";
import { T, useLocale } from "../../bridge/i18n";
import { Markdown } from "../../ui/Markdown";
import { StarOnGitHub } from "../../ui/StarOnGitHub";
import { closeChangelogModal, store } from "./store";
import css from "./shell.module.css";

const MD_STYLE = {
   h: css.clMdH,
   p: css.clMdP,
   ul: css.clMdUl,
   li: css.clMdLi,
   code: css.clMdCode,
   pre: css.clMdPre,
};

function formatDate(iso: string | null): string {
   if (!iso) return "";
   const date = new Date(iso);
   if (Number.isNaN(date.getTime())) return "";
   return date.toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
}

export function ChangelogModal() {
   const { t } = useLocale();
   const release = store.use((s) => s.changelogModal);
   const dialogRef = useRef<HTMLDivElement>(null);

   useEffect(() => {
      if (release) dialogRef.current?.focus();
   }, [release]);

   if (!release) return null;

   const date = formatDate(release.date);
   const heading = release.tag || release.name || "";
   const subtitle = release.name && release.name !== release.tag ? release.name : "";

   return (
      <div
         id="changelog-modal"
         className={`${css.modalOverlay} ${css.show}`}
         onClick={(e) => {
            if (e.target === e.currentTarget) closeChangelogModal();
         }}
      >
         <div
            ref={dialogRef}
            tabIndex={-1}
            className={`${css.modal} ${css.changelog}`}
            role="dialog"
            aria-modal="true"
            aria-labelledby="changelog-heading"
         >
            <div className={css.modalHead}>
               <div className={css.modalTitle}>
                  <span className={css.led} />
                  <span>
                     <T k="shell.changelog.title">Changelog</T>
                  </span>
               </div>
               <button
                  type="button"
                  data-no-drag
                  className={css.modalX}
                  title={t("shell.titlebar.close", "Close")}
                  aria-label={t("shell.titlebar.close", "Close")}
                  onClick={closeChangelogModal}
               >
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
                     <line x1="6" y1="6" x2="18" y2="18" />
                     <line x1="18" y1="6" x2="6" y2="18" />
                  </svg>
               </button>
            </div>

            <div className={css.clHead}>
               <h2 id="changelog-heading" className={css.clTag}>
                  {heading}
               </h2>
               {date && <span className={css.clDate}>{date}</span>}
            </div>
            {subtitle && <div className={css.clName}>{subtitle}</div>}

            <div className={css.clBody}>
               {release.body.trim() ? (
                  <Markdown source={release.body} style={MD_STYLE} />
               ) : (
                  <p className={css.clEmpty}>
                     <T k="shell.changelog.empty">This release shipped without notes — open the full changelog for the details.</T>
                  </p>
               )}
            </div>

            <div className={css.clActions}>
               <StarOnGitHub variant="cta" />
               <button
                  type="button"
                  data-no-drag
                  className={`${css.obBtn} ${css.ghost}`}
                  onClick={() => {
                     closeChangelogModal();
                     send("whatsnew");
                  }}
               >
                  <T k="shell.changelog.viewAll">Full Changelog</T>
               </button>
            </div>
         </div>
      </div>
   );
}
