import { T } from "../../bridge/i18n";
import { store } from "./store";
import css from "./shell.module.css";

export function Warning() {
   const dotaRunning = store.use((s) => s.dotaRunning);
   if (!dotaRunning) return null;
   return (
      <div id="warning" className={css.dotaWarning}>
         <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <path d="M10.3 3.3 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.3a2 2 0 0 0-3.4 0z" />
            <line x1="12" y1="9" x2="12" y2="13" />
            <line x1="12" y1="17" x2="12.01" y2="17" />
         </svg>
         <span>
            <T k="shell.warning.closeDota">Dota 2 is still running — tools are unavailable until you close the game.</T>
         </span>
      </div>
   );
}
