import { send, useEscape } from "../../bridge/host";
import { T, THtml, useLocale } from "../../bridge/i18n";
import { Button } from "../../ui/Button";
import css from "./update.module.css";

export function App() {
   const { t } = useLocale();
   const dismiss = () => send("notNow");

   useEscape(dismiss);

   return (
      <>
         <div className={css.dragArea} onMouseDown={() => send("startDrag")} />

         <button
            type="button"
            data-no-drag
            className={css.closeBtn}
            title={t("common.close", "Close")}
            aria-label={t("common.close", "Close")}
            onClick={dismiss}
         >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
               <path d="M6 18L18 6M6 6l12 12" />
            </svg>
         </button>

         <div className={css.container}>
            <div className={css.icon}>
               <svg
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  aria-hidden="true"
               >
                  <path d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
               </svg>
            </div>

            <div className={css.badge}>
               <T k="modspackUpd.badge">[ MODSPACK UPDATE ]</T>
            </div>

            <h1 className={css.title}>
               <T k="modspackUpd.title">A NEWER MODSPACK IS AVAILABLE</T>
            </h1>

            <p className={css.description}>
               <THtml k="modspackUpd.description">
                  A new version of the ModsPack has been released.
                  <br />
                  Update now to get the latest cosmetic mods.
               </THtml>
            </p>

            <div className={css.buttons}>
               <Button variant="primary" onClick={() => send("updateNow")}>
                  <T k="modspackUpd.updateNow">UPDATE NOW</T>
               </Button>
               <Button variant="ghost" onClick={dismiss}>
                  <T k="modspackUpd.notNow">NOT NOW</T>
               </Button>
            </div>
         </div>
      </>
   );
}
