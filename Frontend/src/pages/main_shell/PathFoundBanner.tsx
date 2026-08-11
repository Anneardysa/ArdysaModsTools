import { T } from "../../bridge/i18n";
import { store } from "./store";
import css from "./shell.module.css";

export function PathFoundBanner() {
   const pathFound = store.use((s) => s.pathFound);
   if (!pathFound.visible) return null;
   return (
      <div id="pathFoundBanner" className={css.pathFound}>
         <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <polyline points="20 6 9 17 4 12" />
         </svg>
         <span>
            <T k="shell.pathFound.label">Dota 2 path found</T>
         </span>
         <span id="pathFoundText" className={css.pathFoundValue}>
            {pathFound.path}
         </span>
      </div>
   );
}
