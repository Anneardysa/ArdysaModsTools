import { T, useLocale } from "../../bridge/i18n";
import { isCustomized } from "./helpers";
import type { MiscOption, Selections } from "./store";
import css from "./misc.module.css";

export function Header({
   options,
   selections,
   generating,
   onClear,
   onLoad,
   onSave,
   onGenerate,
}: {
   options: MiscOption[] | null;
   selections: Selections;
   generating: boolean;
   onClear: () => void;
   onLoad: () => void;
   onSave: () => void;
   onGenerate: () => void;
}) {
   const { t } = useLocale();
   const changed = (options ?? []).filter((o) => isCustomized(o, selections)).length;

   return (
      <header className={css.mfHeader}>
         <div>
            <h1 className={css.mfTitle}>
               <T k="miscForm.title">Miscellaneous Options</T>
            </h1>
            <p className={css.mfSub}>
               {options === null
                  ? t("miscForm.loading", "Loading...")
                  : `${t("miscForm.optionsAvailable", `${options.length} options available`, { count: options.length })} / ${t("miscForm.changedCount", `${changed} changed`, { count: changed })}`}
            </p>
         </div>
         <div className={css.mfActions}>
            <button type="button" data-no-drag className={`${css.btn} ${css.ghost}`} disabled={generating} onClick={onClear}>
               <T k="miscForm.clear">Clear</T>
            </button>
            <button type="button" data-no-drag className={`${css.btn} ${css.ghost}`} disabled={generating} onClick={onLoad}>
               <T k="common.load">Load</T>
            </button>
            <button type="button" data-no-drag className={`${css.btn} ${css.ghost}`} disabled={generating} onClick={onSave}>
               <T k="common.save">Save</T>
            </button>
            <button
               type="button"
               data-no-drag
               className={`${css.btn} ${css.primary}`}
               disabled={generating || !options?.length}
               onClick={onGenerate}
            >
               {generating ? t("miscForm.generating", "Generating...") : t("miscForm.generate", "Generate")}
            </button>
         </div>
      </header>
   );
}
