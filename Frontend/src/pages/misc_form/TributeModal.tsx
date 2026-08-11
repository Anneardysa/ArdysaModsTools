import { useEffect, useState, type ReactNode } from "react";
import { T, translate, useLocale } from "../../bridge/i18n";
import css from "./misc.module.css";

const COUNTDOWN_SECONDS = 10;

function TributeExternalLink({ href, className, children }: { href: string; className?: string; children: ReactNode }) {
   return (
      <a
         href={href}
         target="_blank"
         rel="noreferrer"
         data-no-drag
         className={className}
         onClick={(e) => {
            e.preventDefault();
            window.open(href, "_blank");
         }}
      >
         {children}
      </a>
   );
}

export function TributeModal({ choiceName, onClose }: { choiceName: string | null; onClose: () => void }) {
   const { t } = useLocale();
   const [remaining, setRemaining] = useState(COUNTDOWN_SECONDS);

   useEffect(() => {
      if (remaining <= 0) return;
      const id = window.setTimeout(() => setRemaining((r) => r - 1), 1000);
      return () => window.clearTimeout(id);
   }, [remaining]);

   const canClose = remaining <= 0;

   return (
      <div id="tributeModal" className={`${css.ov} ${css.tributeModalZ}`}>
         <div className={css.ovScrim} />
         <div className={`${css.tributePanel} ${css.animateBounceIn}`}>
            <div className={css.tributeHead}>
               <div className={css.tributeIco}>
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <path d="M4.318 6.318a4.5 4.5 0 000 6.364L12 20.364l7.682-7.682a4.5 4.5 0 00-6.364-6.364L12 7.636l-1.318-1.318a4.5 4.5 0 00-6.364 0z" />
                  </svg>
               </div>
               <h3 className={css.tributeTitle}>
                  <T k="miscForm.tribute.title">Created by Darkness</T>
               </h3>
               <p className={css.tributeSubtitle}>
                  {choiceName
                     ? translate("miscForm.tribute.selected", `Selected: ${choiceName}`, { name: choiceName })
                     : t("miscForm.tribute.subtitle", "This mod was made with ❤ by the community")}
               </p>
            </div>

            <div className={css.tributeLinks}>
               <TributeExternalLink href="https://t.me/Darkness_Logovo" className={css.tributeLink}>
                  <div className={css.tributeLinkIco}>
                     <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
                        <path d="M11.944 0A12 12 0 0 0 0 12a12 12 0 0 0 12 12 12 12 0 0 0 12-12A12 12 0 0 0 12 0a12 12 0 0 0-.056 0zm4.962 7.224c.1-.002.321.023.465.14a.506.506 0 0 1 .171.325c.016.093.036.306.02.472-.18 1.898-.962 6.502-1.36 8.627-.168.9-.499 1.201-.82 1.23-.696.065-1.225-.46-1.9-.902-1.056-.693-1.653-1.124-2.678-1.8-1.185-.78-.417-1.21.258-1.91.177-.184 3.247-2.977 3.307-3.23.007-.032.014-.15-.056-.212s-.174-.041-.249-.024c-.106.024-1.793 1.14-5.061 3.345-.48.33-.913.49-1.302.48-.428-.008-1.252-.241-1.865-.44-.752-.245-1.349-.374-1.297-.789.027-.216.325-.437.893-.663 3.498-1.524 5.83-2.529 6.998-3.014 3.332-1.386 4.025-1.627 4.476-1.635z" />
                     </svg>
                  </div>
                  <div className={css.tributeLinkMain}>
                     <div className={css.tributeLinkTitle}>
                        <T k="miscForm.tribute.telegram">Telegram Channel</T>
                     </div>
                     <div className={css.tributeLinkSub}>@Darkness_Logovo</div>
                  </div>
                  <svg className={css.tributeLinkExt} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <path d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14" />
                  </svg>
               </TributeExternalLink>

               <TributeExternalLink href="https://www.donationalerts.com/r/darknessings" className={css.tributeLink}>
                  <div className={css.tributeLinkIco}>
                     <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <path d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                     </svg>
                  </div>
                  <div className={css.tributeLinkMain}>
                     <div className={css.tributeLinkTitle}>
                        <T k="miscForm.tribute.donation">Donation Alerts</T>
                     </div>
                     <div className={css.tributeLinkSub}>
                        <T k="miscForm.tribute.donationSub">Support the creator</T>
                     </div>
                  </div>
                  <svg className={css.tributeLinkExt} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <path d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14" />
                  </svg>
               </TributeExternalLink>

               <div className={css.tributeLink} style={{ cursor: "default" }}>
                  <div className={css.tributeLinkIco}>
                     <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <path d="M3 10h18M7 15h1m4 0h1m-7 4h12a3 3 0 003-3V8a3 3 0 00-3-3H6a3 3 0 00-3 3v8a3 3 0 003 3z" />
                     </svg>
                  </div>
                  <div className={css.tributeLinkMain}>
                     <div className={css.tributeLinkTitle}>ЮMoney</div>
                     <div className={`${css.tributeLinkSub} ${css.copy}`}>4100 1185 8130 2645</div>
                  </div>
               </div>
            </div>

            <div className={css.tributeFoot}>
               <span className={css.tributeFootNote}>Thank you for using community mods</span>
               <button type="button" data-no-drag disabled={!canClose} className={`${css.btn} ${css.primary}`} onClick={onClose}>
                  {canClose ? t("common.close", "Close") : translate("miscForm.tribute.wait", `Wait ${remaining}s`, { seconds: remaining })}
               </button>
            </div>
         </div>
      </div>
   );
}
