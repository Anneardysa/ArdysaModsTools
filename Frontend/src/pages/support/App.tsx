import { useEffect } from "react";
import { send, useEscape } from "../../bridge/host";
import { T, THtml, useLocale } from "../../bridge/i18n";
import { store } from "./store";
import css from "./support.module.css";


const PLATFORMS = [
   {
     id: "paypal",
     label: "PAYPAL",
     url: "https://paypal.me/ardysa",
     icon: "https://cdn.simpleicons.org/paypal/ffffff",
     subKey: "support.oneTime",
     sub: "One-time donation",
   },
   {
     id: "kofi",
     label: "KO-FI",
     url: "https://ko-fi.com/ardysa",
     icon: "https://cdn.simpleicons.org/kofi/ffffff",
     subKey: "support.oneTimeMonthly",
     sub: "One-time / monthly",
   },
   {
     id: "sociabuzz",
     label: "SOCIABUZZ",
     url: "https://sociabuzz.com/ardysa/support",
     icon: "https://cdn.simpleicons.org/simpleicons/ffffff",
     subKey: "support.oneTime",
     sub: "One-time donation",
   },
] as const;

export function App() {
   const { t } = useLocale();
   const promptMode = store.use((s) => s.promptMode);
   const countdown = store.use((s) => s.countdown);
   const snooze = store.use((s) => s.snooze);

   const locked = countdown > 0;

   useEffect(() => {
      if (countdown <= 0) return;
      const id = window.setInterval(() => {
         store.set((s) => ({ countdown: Math.max(0, s.countdown - 1) }));
      }, 1000);
      return () => window.clearInterval(id);
   }, [countdown > 0]);

   const requestClose = () => {
      if (locked) return;
      send("close", { snoozeToday: snooze });
   };

   useEscape(requestClose);

   return (
      <>
         <div className={css.dragArea} onMouseDown={() => send("startDrag")} />

         <button
            type="button"
            data-no-drag
            className={css.closeX}
            title={t("common.close", "Close")}
            aria-label={t("common.close", "Close")}
            disabled={locked}
            onClick={requestClose}
         >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
               <line x1="6" y1="6" x2="18" y2="18" />
               <line x1="18" y1="6" x2="6" y2="18" />
            </svg>
         </button>

         <div className={css.container}>
            <div className={css.badge}>
               <T k="support.badge">[ SUPPORT ]</T>
            </div>

            <h1 className={css.title}>
               <T k="support.title">SUPPORT THE DEVELOPMENT</T>
            </h1>
            <p className={css.subtitle}>
               <THtml k="support.subtitle">
                  ArdysaModsTools is free and always will be.
                  <br />
                  Your support helps keep this project alive!
               </THtml>
            </p>

            <div className={css.cards}>
               {PLATFORMS.map((platform) => (
                  <button
                     type="button"
                     data-no-drag
                     key={platform.id}
                     /* Was a <div onclick>: not focusable, not operable by keyboard, and announced as
                        nothing. A real button costs the same and works for everyone. */
                     className={`${css.card} ${css[platform.id] ?? ""}`}
                     onClick={() => send("openUrl", { url: platform.url })}
                  >
                     <img className={css.cardIcon} src={platform.icon} alt="" aria-hidden="true" />
                     <div className={css.cardTitle}>{platform.label}</div>
                     <div className={css.cardSub}>
                        <T k={platform.subKey}>{platform.sub}</T>
                     </div>
                  </button>
               ))}
            </div>

            <div className={css.footerMsg}>
               <T k="support.footerMsg">
                  ❤ Every contribution helps improve the tool for everyone ❤
               </T>
            </div>

            <div className={css.separator} />

            {promptMode && (
               <label className={css.snooze}>
                  <input
                     type="checkbox"
                     checked={snooze}
                     onChange={(e) => store.set({ snooze: e.currentTarget.checked })}
                  />
                  <span>
                     <T k="support.dontShowToday">Don't show this again today</T>
                  </span>
               </label>
            )}

            <button
               type="button"
               data-no-drag
               className={css.closeBtn}
               disabled={locked}
               onClick={requestClose}
            >
               {locked
                  ? `[ ${t("support.pleaseWait", `PLEASE WAIT ${countdown}s`, { seconds: countdown })} ]`
                  : `[ ${t("common.close", "Close").toUpperCase()} ]`}
            </button>
         </div>
      </>
   );
}
