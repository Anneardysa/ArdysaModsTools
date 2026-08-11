import { send } from "../../bridge/host";
import { T, useLocale } from "../../bridge/i18n";
import { armOrConfirmDelete, resetAll, showToast, store } from "./store";

export function ActionBar() {
   const { t } = useLocale();
   const deleteArmed = store.use((s) => s.deleteArmed);

   return (
      <div className="flex-shrink-0 border-t border-amt-border p-4 flex items-center gap-3">
         <button
            type="button"
            data-no-drag
            onClick={() => send("applySettings")}
            className="action-btn flex-1 bg-amt-accent hover:bg-amt-accent-hover text-amt-on-accent font-bold py-3 rounded-lg text-sm tracking-wider transition-colors"
         >
            <T k="perf.action.apply">[ APPLY TO AUTOEXEC.CFG ]</T>
         </button>
         <button
            type="button"
            data-no-drag
            onClick={() => send("exportCfg")}
            className="action-btn px-5 py-3 rounded-lg text-xs font-bold border border-amt-border hover:border-amt-border-strong text-amt-text-dim hover:text-amt-accent transition-colors"
         >
            <T k="perf.action.export">EXPORT</T>
         </button>
         <button
            type="button"
            data-no-drag
            onClick={() => {
               resetAll();
               showToast(t("perf.banner.resetNotice", "// reset to defaults"), "info");
            }}
            className="action-btn px-5 py-3 rounded-lg text-xs font-bold border border-amt-border hover:border-amt-border-strong text-amt-text-dim hover:text-amt-accent transition-colors"
         >
            <T k="perf.action.reset">RESET</T>
         </button>
         <button
            id="deleteCfgBtn"
            type="button"
            data-no-drag
            onClick={() => armOrConfirmDelete(() => send("deleteCfg"))}
            className={`action-btn px-5 py-3 rounded-lg text-xs font-bold border border-[rgba(255,137,131,0.5)] text-amt-error hover:bg-[rgba(255,137,131,0.12)] transition-colors ${deleteArmed ? "armed" : ""}`}
            title={t("perf.action.deleteTooltip", "Remove the autoexec.cfg this tool wrote to your Dota 2 config folder")}
         >
            {deleteArmed ? t("perf.action.deleteConfirm", "CONFIRM DELETE?") : t("perf.action.delete", "DELETE")}
         </button>
      </div>
   );
}
