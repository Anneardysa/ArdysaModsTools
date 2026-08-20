import { useEffect, useRef, useState } from "react";
import { send } from "../../bridge/host";
import { T, useLocale } from "../../bridge/i18n";
import { closeInstallLogModal, store } from "./store";
import type { ConsoleLine, InstallLogLine, LogCategory } from "./types";
import css from "./shell.module.css";

function lineText(l: ConsoleLine): string {
   if (l.segments) return window.renderLogSegments ? window.renderLogSegments(l.segments) : "";
   return l.text ?? "";
}

function fallbackLines(consoleLines: ConsoleLine[], variant: "error" | "success"): { text: string; category: LogCategory }[] {
   if (variant === "success") return consoleLines.slice(-12).map((l) => ({ text: lineText(l), category: l.category }));
   const errorsAndWarnings = consoleLines.filter((l) => l.category === "error" || l.category === "warning").slice(-12);
   const source = errorsAndWarnings.length > 0 ? errorsAndWarnings : consoleLines.slice(-8);
   return source.map((l) => ({ text: lineText(l), category: l.category }));
}

export function FailureModal() {
   const { t } = useLocale();
   const payload = store.use((s) => s.installLogModal);
   const consoleLines = store.use((s) => s.consoleLines);
   const [revealed, setRevealed] = useState(false);
   const closeRef = useRef<HTMLButtonElement>(null);

   useEffect(() => {
      if (!payload) return;
      setRevealed(payload.variant === "error");
      closeRef.current?.focus();
   }, [payload]);

   if (!payload) return null;
   const ok = payload.variant === "success";

   const lines: { text: string; category: LogCategory }[] =
      payload.lines && payload.lines.length > 0
         ? payload.lines.map((l: InstallLogLine) => ({ text: l.t, category: (l.c as LogCategory) || "default" }))
         : fallbackLines(consoleLines, payload.variant);

   const copyLog = () => send("copyConsole", { text: lines.map((l) => l.text).join("\n") });

   return (
      <div
         id="failure-modal"
         className={`${css.modalOverlay} ${css.show}`}
         onClick={(e) => {
            if (e.target === e.currentTarget) closeInstallLogModal();
         }}
      >
         <div className={`${css.modal} ${css.confirm} ${ok ? css.logok : css.failure}`} role="alertdialog" aria-modal="true" aria-labelledby="failure-heading">
            <div className={css.modalHead}>
               <div className={css.modalTitle}>
                  <span className={css.led} />
                  <span id="failure-eyebrow">{t(ok ? "shell.failure.eyebrowOk" : "shell.failure.eyebrow", ok ? "Complete" : "Error")}</span>
               </div>
               <button type="button" data-no-drag className={css.modalX} title={t("shell.titlebar.close", "Close")} onClick={closeInstallLogModal}>
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
                     <line x1="6" y1="6" x2="18" y2="18" />
                     <line x1="18" y1="6" x2="6" y2="18" />
                  </svg>
               </button>
            </div>
            <div className={css.confirmBody}>
               <span className={css.confirmIco}>
                  {ok ? (
                     <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <circle cx="12" cy="12" r="10" />
                        <path d="M8.5 12.2l2.4 2.4 4.6-5" />
                     </svg>
                  ) : (
                     <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <circle cx="12" cy="12" r="10" />
                        <line x1="12" y1="8" x2="12" y2="13" />
                        <line x1="12" y1="16" x2="12.01" y2="16" />
                     </svg>
                  )}
               </span>
               <div id="failure-heading" className={css.confirmHeading}>
                  {payload.title}
               </div>
               <div id="failure-text" className={css.confirmText}>
                  {payload.body}
               </div>
               {ok && (
                  <div className={css.confirmText}>
                     <T k="play.postGenerate.note">Launch Dota 2 through this app for your mods to work correctly.</T>
                  </div>
               )}
               {!revealed ? (
                  <button id="failure-showlog" type="button" data-no-drag className={`${css.obBtn} ${css.ghost}`} onClick={() => setRevealed(true)}>
                     <T k="shell.toast.showLog">Show Log</T>
                  </button>
               ) : (
                  <div className={css.failureLog}>
                     <div className={css.failureLogHead}>
                        <T k="shell.failure.logTitle">Recent log</T>
                     </div>
                     <div id="failure-log-body" className={css.failureLogBody}>
                        {lines.length === 0 ? (
                           <div className={css.failureLogEmpty}>
                              <T k="shell.failure.empty">No log entries captured.</T>
                           </div>
                        ) : (
                           lines.map((l, i) => (
                              <div key={i} className={`${css.logLine} ${css[l.category] ?? css.default}`}>
                                 <div className={css.bar} />
                                 <div className={css.msg}>{l.text}</div>
                              </div>
                           ))
                        )}
                     </div>
                  </div>
               )}
            </div>
            <div className={css.confirmActions}>
               {ok && (
                  <button
                     id="failure-play"
                     type="button"
                     data-no-drag
                     className={`${css.obBtn} ${css.primary}`}
                     onClick={() => {
                        send("playDota");
                        closeInstallLogModal();
                     }}
                  >
                     <T k="play.postGenerate.button">Play Dota 2</T>
                  </button>
               )}
               {revealed && (
                  <button id="failure-copy" type="button" data-no-drag className={`${css.obBtn} ${css.ghost}`} onClick={copyLog}>
                     {t("shell.failure.copy", "Copy Log")}
                  </button>
               )}
               <button ref={closeRef} id="failure-close" type="button" data-no-drag className={`${css.obBtn} ${ok ? css.ghost : css.primary}`} onClick={closeInstallLogModal}>
                  {t(ok ? "common.done" : "common.close", ok ? "Done" : "Close")}
               </button>
            </div>
         </div>
      </div>
   );
}
