import { T, translate, useLocale } from "../../bridge/i18n";
import { applyPreset, store } from "./store";
import type { PresetName } from "./types";

const PRESET_ORDER: PresetName[] = ["potato", "low", "medium", "high", "ultra", "competitive"];

export function Sidebar() {
   useLocale();
   const activePreset = store.use((s) => s.activePreset);

   return (
      <div className="w-[220px] flex-shrink-0 flex flex-col border-r border-amt-border fade-in" style={{ minHeight: 0 }}>
         <div className="px-4 pt-4 pb-2 text-[11px] text-amt-text-muted tracking-[3px] flex-shrink-0">
            <T k="perf.presets">[ PRESETS ]</T>
         </div>

         <div className="flex-1 min-h-0 overflow-y-auto px-4 pb-2 flex flex-col gap-2">
            {PRESET_ORDER.map((name) => (
               <button
                  key={name}
                  type="button"
                  data-no-drag
                  data-preset={name}
                  onClick={() => applyPreset(name)}
                  className={`preset-btn rounded-lg px-4 py-2.5 text-left flex-shrink-0 ${activePreset === name ? "active" : ""}`}
               >
                  <div className="text-sm font-bold text-amt-accent tracking-wide">{translate("perf.preset." + name, name.toUpperCase())}</div>
                  <div className="text-[11px] text-amt-text-dim mt-1 leading-snug">{translate("perf.preset." + name + ".desc", "")}</div>
               </button>
            ))}
         </div>

         <div className="flex-shrink-0 mx-4 mb-4 mt-2 text-[11px] text-amt-text-dim leading-relaxed p-3 border border-amt-border rounded-lg">
            <span>{translate("perf.sidebar.output", "// output: autoexec.cfg", { file: "autoexec.cfg" })}</span>
            <br />
            <span>{translate("perf.sidebar.path", "// path: dota/cfg/", { path: "dota/cfg/" })}</span>
         </div>
      </div>
   );
}
