import { useState } from "react";
import { send } from "../../bridge/host";
import { T, useLocale } from "../../bridge/i18n";
import type { AlertType } from "./types";
import css from "./gallery.module.css";

const ICON_PATHS: Record<AlertType, string> = {
   success: "M5 13l4 4L19 7",
   warning: "M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z",
   info: "M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z",
};

export function AlertModal({
   title,
   message,
   type,
   hasLog,
   onClose,
   onShowLog,
}: {
   title: string;
   message: string;
   type: AlertType;
   hasLog: boolean;
   onClose: () => void;
   onShowLog: () => void;
}) {
   return (
      <div id="alertModal" className={css.ov}>
         <div className={css.ovScrim} onClick={onClose} />
         <div className={css.dialog}>
            <div className={css.dialogBody}>
               <div className={css.dialogIco}>
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <path d={ICON_PATHS[type]} />
                  </svg>
               </div>
               <div className={css.dialogMain}>
                  <h3 id="alertTitle" className={css.dialogTitle}>
                     {title}
                  </h3>
                  <p id="alertMessage" className={css.dialogMsg}>
                     {message}
                  </p>
               </div>
            </div>
            <div className={css.dialogActions}>
               {hasLog && (
                  <button type="button" data-no-drag id="alertLogButton" className={`${css.btn} ${css.ghost}`} onClick={onShowLog}>
                     <T k="hero.log.button">Show Log</T>
                  </button>
               )}
               <button type="button" data-no-drag id="alertOkButton" className={`${css.btn} ${css.primary}`} onClick={onClose}>
                  <T k="common.ok">OK</T>
               </button>
            </div>
         </div>
      </div>
   );
}

export function LogModal({ lines, onClose }: { lines: string[]; onClose: () => void }) {
   const { t } = useLocale();
   const [copied, setCopied] = useState(false);

   const copy = () => {
      const text = lines.join("\n");
      if (!text) return;
      send("copyConsole", { text });
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1200);
   };

   return (
      <div id="logModal" className={css.ov} style={{ zIndex: 250 }}>
         <div className={css.ovScrim} onClick={onClose} />
         <div className={`${css.dialog} ${css.logDialog}`}>
            <div className={css.dialogBody}>
               <div className={css.dialogMain}>
                  <h3 className={css.dialogTitle}>
                     <T k="hero.log.title">Generation Log</T>
                  </h3>
                  <div id="logModalConsole" className={`${css.retroTerminal} ${css.logModalConsole}`}>
                     <div id="logModalContent">
                        {lines.length === 0 ? (
                           <div className={css.logLine}>No log available.</div>
                        ) : (
                           lines.map((line, i) => (
                              <div className={css.logLine} key={i}>
                                 {line}
                              </div>
                           ))
                        )}
                     </div>
                  </div>
               </div>
            </div>
            <div className={css.dialogActions}>
               <button type="button" data-no-drag className={`${css.btn} ${css.ghost}`} onClick={copy}>
                  {copied ? t("common.copied", "Copied") : t("miscForm.copyLog", "Copy")}
               </button>
               <button type="button" data-no-drag className={`${css.btn} ${css.primary}`} onClick={onClose}>
                  <T k="common.close">Close</T>
               </button>
            </div>
         </div>
      </div>
   );
}
