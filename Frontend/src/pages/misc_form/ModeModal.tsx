import { T } from "../../bridge/i18n";
import css from "./misc.module.css";

export function ModeModal({ onSelect, onDismiss }: { onSelect: (mode: "clean" | "add") => void; onDismiss: () => void }) {
   return (
      <div id="modeModal" className={`${css.ov} ${css.modeModalZ}`}>
         <div className={css.ovScrim} onClick={onDismiss} />
         <div className={`${css.modePanel} ${css.animateBounceIn}`}>
            <h3 className={css.modeTitle}>
               <T k="miscForm.modeTitle">Generation Mode</T>
            </h3>
            <div className={css.modeList}>
               <button type="button" data-no-drag className={css.modeOption} onClick={() => onSelect("clean")}>
                  <div className={css.modeIco}>
                     <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <path d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                     </svg>
                  </div>
                  <div>
                     <div className={css.modeName}>
                        <T k="miscForm.genOnly.name">Generate Only Misc Mods</T>
                     </div>
                     <div className={css.modeDesc}>
                        <T k="miscForm.genOnly.desc">Create a clean VPK with only Misc mods</T>
                     </div>
                  </div>
               </button>
               <button type="button" data-no-drag className={css.modeOption} onClick={() => onSelect("add")}>
                  <div className={css.modeIco}>
                     <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <path d="M12 6v6m0 0v6m0-6h6m-6 0H6" />
                     </svg>
                  </div>
                  <div>
                     <div className={css.modeName}>
                        <T k="miscForm.addCurrent.name">Add to Current Mods</T>
                     </div>
                     <div className={css.modeDesc}>
                        <T k="miscForm.addCurrent.desc">Apply modifications on top of existing game mods</T>
                     </div>
                  </div>
               </button>
            </div>
            <button type="button" data-no-drag className={css.modeCancel} onClick={onDismiss}>
               <T k="common.cancel">Cancel</T>
            </button>
         </div>
      </div>
   );
}
