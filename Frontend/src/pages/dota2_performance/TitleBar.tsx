import { send, startDragUnlessInteractive } from "../../bridge/host";
import { T, translate, useLocale } from "../../bridge/i18n";
import { store } from "./store";

export function TitleBar() {
   const { t } = useLocale();
   const activePreset = store.use((s) => s.activePreset);

   const badgeLabel = activePreset ? translate("perf.preset." + activePreset, activePreset.toUpperCase()) : t("perf.customBadge", "CUSTOM");

   return (
      <div id="titlebar" className="h-11 w-full flex items-center justify-between px-4 border-b border-amt-border relative z-10">
         <div className="flex-1 flex items-center gap-2 h-full" onMouseDown={startDragUnlessInteractive}>
            <span className="text-amt-accent flex">
               <svg viewBox="0 0 1000 1000" fill="currentColor" className="w-[15px] h-[15px]" aria-hidden="true">
                  <g>
                     <g>
                        <g transform="translate(1.007913, 440.428851)">
                           <g>
                              <path d="M190.79,158.66c0,12.07,2.23,23.49,6.69,34.25c4.47,10.77,10.65,20.08,18.52,27.96s17.06,14.05,27.56,18.52c10.5,4.46,21.79,6.69,33.87,6.69H84.48c11.55-1.57,22.71-5.24,33.48-11.02c10.76-5.78,20.86-13,30.31-21.67c9.46-8.67,17.86-18.25,25.21-28.75c7.34-10.5,13.11-21.26,17.31-32.29V158.66z M342.02,246.08c11.03,0,20.48-4.59,28.35-13.77c7.88-9.19,11.81-19.57,11.81-31.13c0-2.09,0-4.19,0-6.29c0-2.09-0.53-4.2-1.58-6.31l-22.83-66.15l-1.58-4.73L300.27-43.75c-3.68-8.92-10.24-13.38-19.69-13.38c-8.93,0-15.23,4.46-18.9,13.38l94.5-261.46L443.6-53.98c3.68,10.5,7.88,22.97,12.6,37.42c4.72,14.43,8.92,26.9,12.6,37.4l64.58,184.29c3.67,10.5,10.09,19.95,19.29,28.35c9.19,8.41,19.82,12.6,31.9,12.6H342.02z" />
                           </g>
                        </g>
                     </g>
                     <g>
                        <g>
                           <g transform="translate(407.31193, 150.568186)">
                              <g>
                                 <path d="M400.89,182.21c0-12.07-2.23-23.49-6.69-34.25c-4.47-10.77-10.65-20.08-18.52-27.96s-17.06-14.05-27.56-18.52c-10.5-4.46-21.79-6.69-33.88-6.69H507.2c-11.55,1.57-22.71,5.24-33.48,11.02c-10.76,5.78-20.86,13-30.31,21.67c-9.46,8.67-17.86,18.25-25.21,28.75c-7.34,10.5-13.11,21.26-17.31,32.29V182.21z M249.66,94.79c-11.03,0-20.48,4.59-28.35,13.77c-7.88,9.19-11.81,19.57-11.81,31.12c0,2.09,0,4.19,0,6.29c0,2.09,0.53,4.2,1.58,6.31l22.83,66.15l1.58,4.73l55.92,161.46c3.68,8.92,10.24,13.38,19.69,13.38c8.93,0,15.23-4.46,18.9-13.38l-94.5,261.46l-87.42-251.23c-3.68-10.5-7.88-22.97-12.6-37.42c-4.72-14.43-8.92-26.9-12.6-37.4L58.29,135.75c-3.67-10.5-10.09-19.95-19.29-28.35c-9.19-8.41-19.82-12.6-31.9-12.6H249.66z" />
                              </g>
                           </g>
                        </g>
                     </g>
                  </g>
               </svg>
            </span>
            <span className="font-bold text-xs tracking-[2px] uppercase">
               <span className="text-amt-text-muted">// </span>
               <span className="text-amt-accent">AMT</span>
            </span>
            <span className="text-amt-text-dim font-medium text-[11px] tracking-[1px] uppercase">
               <T k="perf.header">Performance Tweak</T>
            </span>
            <span
               id="presetBadge"
               className="text-[11px] px-2 py-0.5 border tracking-wider"
               style={{ color: activePreset ? "var(--ink)" : "var(--mute)", borderColor: activePreset ? "var(--ink)" : "var(--hairline)" }}
            >
               {badgeLabel}
            </span>
         </div>
         <button
            id="closeBtn"
            type="button"
            data-no-drag
            className="h-9 w-10 flex items-center justify-center text-amt-text-muted hover:text-white hover:bg-[rgba(248,81,73,0.85)] border border-transparent transition-colors flex-shrink-0"
            onClick={(e) => {
               e.stopPropagation();
               send("close");
            }}
            title={t("common.close", "Close")}
            aria-label={t("common.close", "Close")}
         >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" className="w-[14px] h-[14px]" aria-hidden="true">
               <line x1="6" y1="6" x2="18" y2="18" />
               <line x1="18" y1="6" x2="6" y2="18" />
            </svg>
         </button>
      </div>
   );
}
