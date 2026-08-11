import { T, useLocale } from "../../bridge/i18n";
import type { MiscOption } from "./store";
import css from "./misc.module.css";

export function Header({
   options,
   generating,
   onClear,
   onLoad,
   onSave,
   onGenerate,
}: {
   options: MiscOption[] | null;
   generating: boolean;
   onClear: () => void;
   onLoad: () => void;
   onSave: () => void;
   onGenerate: () => void;
}) {
   const { t } = useLocale();

   return (
      <header className={css.mfHeader}>
         <div>
            <h1 className={css.mfTitle}>
               <T k="miscForm.title">Miscellaneous Options</T>
            </h1>
            <p className={css.mfSub}>
               {options === null
                  ? t("miscForm.loading", "Loading...")
                  : t("miscForm.optionsAvailable", `${options.length} options available`, { count: options.length })}
            </p>
         </div>
         <div className={css.mfActions}>
            <button type="button" data-no-drag className={`${css.btn} ${css.ghost}`} disabled={generating} onClick={onClear}>
               <T k="miscForm.clear">Clear</T>
            </button>
            <button type="button" data-no-drag className={`${css.btn} ${css.ghost}`} onClick={onLoad}>
               <T k="common.load">Load</T>
            </button>
            <button type="button" data-no-drag className={`${css.btn} ${css.ghost}`} onClick={onSave}>
               <T k="common.save">Save</T>
            </button>
            <button
               type="button"
               data-no-drag
               className={`${css.btn} ${css.primary}`}
               disabled={generating}
               onClick={onGenerate}
            >
               {generating ? t("miscForm.generating", "Generating...") : t("miscForm.generate", "Generate")}
            </button>
         </div>
      </header>
   );
}
