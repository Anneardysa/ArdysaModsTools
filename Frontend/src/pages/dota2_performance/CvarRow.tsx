import type { ReactNode } from "react";
import { T, translate, useLocale } from "../../bridge/i18n";
import { setCvar, toggleCvar } from "./store";
import type { CvarConfig } from "./types";

export function CvarRow({ cvar, cfg, value, isModified }: { cvar: string; cfg: CvarConfig; value: string; isModified: boolean }) {
   useLocale();
   const label = translate("perf.cvar." + cvar + ".label", cfg.label);
   const tip = translate("perf.cvar." + cvar + ".tip", cfg.tip);

   let control: ReactNode;
   if (cfg.type === "toggle") {
      const isOn = value !== "0";
      control = (
         <button type="button" className={`toggle-track ${isOn ? "on" : ""}`} data-no-drag role="switch" aria-checked={isOn} aria-label={label} onClick={() => toggleCvar(cvar)}>
            <div className="toggle-thumb" />
         </button>
      );
   } else if (cfg.type === "select") {
      control = (
         <select className="cvar-select" data-no-drag value={value} onChange={(e) => setCvar(cvar, e.target.value)} aria-label={label}>
            {Object.entries(cfg.options ?? {}).map(([v, optLabel]) => (
               <option key={v} value={v}>
                  {optLabel}
               </option>
            ))}
         </select>
      );
   } else {
      control = <input className="cvar-input" data-no-drag type="text" value={value} onChange={(e) => setCvar(cvar, e.target.value)} aria-label={label} />;
   }

   return (
      <div id={`row-${cvar}`} className="setting-row flex items-center justify-between px-5 py-3">
         <div className="flex-1 min-w-0 mr-4">
            <div className="flex items-center">
               <span className="text-sm font-semibold">{label}</span>
               {isModified && (
                  <span className="diff-badge">
                     <T k="perf.cvar.modified">mod</T>
                  </span>
               )}
            </div>
            <div className="text-xs text-amt-text-dim mt-1 truncate" title={tip}>
               // {tip}
            </div>
         </div>
         <div className="flex items-center gap-3">
            <span className="text-xs text-amt-text-dim">{cvar}</span>
            {control}
         </div>
      </div>
   );
}
