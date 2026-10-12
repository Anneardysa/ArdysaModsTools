import { send } from "../../bridge/host";
import { T, translatePlural, useLocale } from "../../bridge/i18n";
import { Icon } from "./icons";
import { armOrConfirmDelete, changedKeys, hasWrittenCfg, resetAll, setBusy, showToast, store } from "./store";

function ApplyStatus() {
   const { t } = useLocale();
   const busy = store.use((s) => s.busy);
   const currentValues = store.use((s) => s.currentValues);
   const originalValues = store.use((s) => s.originalValues);
   const changed = changedKeys(currentValues, originalValues).length;

   let icon: "info" | "warning" | "check_circle" = "check_circle";
   let text: string;
   let tone = "text-amt-text-dim";
   if (busy) {
      icon = "info";
      text = t("perf.status.applying", "Applying. A Map Cleanup change can take a minute.");
      tone = "text-amt-text";
   } else if (!hasWrittenCfg(originalValues)) {
      icon = "warning";
      text = t("perf.status.notWritten", "Nothing written yet. APPLY creates autoexec.cfg.");
      tone = "text-amt-amber";
   } else if (changed > 0) {
      icon = "warning";
      text = translatePlural("perf.status.pending", changed, `${changed} changes not applied`);
      tone = "text-amt-amber";
   } else {
      text = t("perf.status.applied", "Everything shown is applied.");
   }

   return (
      <div className={`apply-status flex-1 min-w-0 flex items-center gap-2 text-xs ${tone}`} role="status" aria-live="polite">
         <Icon name={icon} size={14} />
         <span className="leading-snug">{text}</span>
      </div>
   );
}

export function ActionBar() {
   const { t } = useLocale();
   const deleteArmed = store.use((s) => s.deleteArmed);
   const busy = store.use((s) => s.busy);
   const ghost = "action-btn px-4 py-3 text-xs font-bold border border-amt-border hover:border-amt-border-strong text-amt-text-dim hover:text-amt-text";

   return (
      <div className="flex-shrink-0 border-t border-amt-border px-5 py-3 flex flex-wrap items-center gap-x-3 gap-y-2">
         <ApplyStatus />
         <button
            type="button"
            data-no-drag
            disabled={busy}
            aria-busy={busy}
            onClick={() => {
               setBusy();
               send("applySettings");
            }}
            className="action-btn whitespace-nowrap bg-amt-accent hover:bg-amt-accent-hover text-amt-on-accent font-bold px-6 py-3 text-sm tracking-wider"
         >
            <Icon name="check" size={15} />
            <T k="perf.action.apply">[ APPLY TO AUTOEXEC.CFG ]</T>
         </button>
         <button type="button" data-no-drag onClick={() => send("exportCfg")} className={ghost} title={t("perf.action.export", "EXPORT")} aria-label={t("perf.action.export", "EXPORT")}>
            <Icon name="arrow_top_right" size={13} />
            <span className="action-label">
               <T k="perf.action.export">EXPORT</T>
            </span>
         </button>
         <button
            type="button"
            data-no-drag
            onClick={() => {
               resetAll();
               showToast(t("perf.banner.resetNotice", "// reset to defaults"), "info");
            }}
            className={ghost}
            title={t("perf.action.reset", "RESET")}
            aria-label={t("perf.action.reset", "RESET")}
         >
            <Icon name="reset" size={13} />
            <span className="action-label">
               <T k="perf.action.reset">RESET</T>
            </span>
         </button>
         <button
            id="deleteCfgBtn"
            type="button"
            data-no-drag
            disabled={busy}
            onClick={() =>
               armOrConfirmDelete(() => {
                  setBusy();
                  send("deleteCfg");
               })
            }
            className={`action-btn danger-btn px-4 py-3 text-xs font-bold ${deleteArmed ? "armed" : ""}`}
            aria-label={t("perf.action.delete", "DELETE")}
            title={t("perf.action.deleteTooltip", "Remove the autoexec.cfg this tool wrote and turn Map Cleanup off")}
         >
            <Icon name="trash" size={13} />
            {deleteArmed ? t("perf.action.deleteConfirm", "CONFIRM DELETE?") : <span className="action-label">{t("perf.action.delete", "DELETE")}</span>}
         </button>
      </div>
   );
}
