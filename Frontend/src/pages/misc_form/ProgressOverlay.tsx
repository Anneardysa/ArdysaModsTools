import { useEffect, useRef } from "react";
import { T } from "../../bridge/i18n";
import css from "./misc.module.css";

export function ProgressOverlay({
   title,
   percent,
   status,
   lines,
   flash,
   onCancel,
}: {
   title: string;
   percent: number;
   status: string;
   lines: string[];
   flash: "success" | "error" | null;
   onCancel: () => void;
}) {
   const consoleRef = useRef<HTMLDivElement>(null);

   useEffect(() => {
      const el = consoleRef.current;
      if (el) el.scrollTop = el.scrollHeight;
   }, [lines]);

   return (
      <div id="progressOverlay" className={`${css.ov} ${css.progressOverlayBg}`}>
         <div className={`${css.progressPanel} ${css.animateBounceIn}`}>
            <div className={css.progressHead}>
               <div className={css.progressHeadLeft}>
                  <div className={`${css.progressSpinner} ${css.animateSpin}`} aria-hidden="true" />
                  <span className={css.progressTitleText}>{title}</span>
               </div>
               <span className={css.progressPercentText}>{Math.round(percent)}%</span>
            </div>
            <div className={css.progressBarWrap}>
               <div className={css.progressBarTrack}>
                  <div className={css.progressBarFill} style={{ width: `${percent}%` }} />
               </div>
               <p className={css.progressStatusText}>{status}</p>
            </div>
            <div className={css.progressConsoleWrap}>
               <div
                  ref={consoleRef}
                  className={`${css.progressConsole} ${css.retroTerminal} ${flash === "success" ? css.flashSuccess : flash === "error" ? css.flashError : ""}`}
               >
                  <div>
                     {lines.map((line, i) => (
                        <div className={css.logLine} key={i}>
                           {line}
                        </div>
                     ))}
                  </div>
               </div>
            </div>
            <div className={css.progressFoot}>
               <button type="button" data-no-drag className={`${css.btn} ${css.ghost}`} onClick={onCancel}>
                  <T k="common.cancel">Cancel</T>
               </button>
            </div>
         </div>
      </div>
   );
}
