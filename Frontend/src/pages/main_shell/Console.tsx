import { useEffect, useRef } from "react";
import { send } from "../../bridge/host";
import { T, useLocale } from "../../bridge/i18n";
import { store } from "./store";
import type { ConsoleLine } from "./types";
import css from "./shell.module.css";

function LogLine({ line }: { line: ConsoleLine }) {
   useLocale();
   const text = line.segments
      ? window.renderLogSegments
         ? window.renderLogSegments(line.segments)
         : line.segments.map((s) => (typeof s === "string" ? s : s.k || "")).join("")
      : (line.text ?? "");

   return (
      <div className={`${css.logLine} ${css[line.category] ?? css.default}`}>
         <div className={css.bar} />
         <div className={css.msg}>{text}</div>
      </div>
   );
}

export function Console() {
   const { t } = useLocale();
   const lines = store.use((s) => s.consoleLines);
   const bodyRef = useRef<HTMLDivElement>(null);
   const atBottomRef = useRef(true);

   const trackScroll = () => {
      const body = bodyRef.current;
      if (body) atBottomRef.current = body.scrollHeight - body.scrollTop - body.clientHeight < 40;
   };

   useEffect(() => {
      const body = bodyRef.current;
      if (body && atBottomRef.current) body.scrollTop = body.scrollHeight;
   }, [lines]);

   const copyConsole = () => {
      const text = lines
         .map((l) => (l.segments ? (window.renderLogSegments ? window.renderLogSegments(l.segments) : "") : (l.text ?? "")))
         .join("\n");
      send("copyConsole", { text });
   };

   return (
      <section id="console-panel" className={`${css.console} notch`}>
         <div className={css.consoleHead}>
            <div className={css.cTitle}>
               <span className={css.led} />{" "}
               <span>
                  <T k="shell.console.title">Console</T>
               </span>
            </div>
            <button type="button" data-no-drag className={css.copyBtn} onClick={copyConsole}>
               <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <rect x="9" y="9" width="13" height="13" rx="2" />
                  <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1" />
               </svg>
               <span>{t("shell.console.copy", "COPY")}</span>
            </button>
         </div>
         <div id="console-body" ref={bodyRef} className={css.consoleBody} onScroll={trackScroll}>
            {lines.map((l) => (
               <LogLine key={l.id} line={l} />
            ))}
         </div>
      </section>
   );
}
