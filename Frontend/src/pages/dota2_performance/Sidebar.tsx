import { T, translate, useLocale } from "../../bridge/i18n";
import { PRESETS } from "./data";
import { Icon } from "./icons";
import { applyPreset, store } from "./store";
import type { PresetName } from "./types";

const PRESET_ORDER: PresetName[] = ["potato", "low", "medium", "high", "ultra", "competitive"];

function PresetSpec({ name }: { name: PresetName }) {
   const p = PRESETS[name];
   const fps = p.fps_max === "0" ? translate("perf.spec.uncapped", "uncapped") : `${p.fps_max} FPS`;
   const scale = `${Math.round(parseFloat(p.mat_viewportscale ?? "1") * 100)}%${p.r_dota_fsr_upsample === "1" ? " FSR" : ""}`;
   const cleanup = (p.amt_remove_foliage === "1" ? 1 : 0) + (p.amt_remove_props === "1" ? 1 : 0);

   return (
      <div className="preset-spec">
         <span title="fps_max">{fps}</span>
         <span title="mat_viewportscale">{scale}</span>
         {cleanup > 0 && (
            <span title={translate("perf.cleanup.title", "MAP CLEANUP")}>
               <Icon name="terrain" size={11} />
               {cleanup === 2 ? translate("perf.spec.cleanupAll", "clean map") : translate("perf.spec.cleanupFoliage", "no foliage")}
            </span>
         )}
      </div>
   );
}

export function Sidebar() {
   useLocale();
   const activePreset = store.use((s) => s.activePreset);

   return (
      <div className="w-[248px] flex-shrink-0 flex flex-col border-r border-amt-border" style={{ minHeight: 0 }}>
         <div className="px-4 pt-4 pb-2 text-[11px] text-amt-text-dim tracking-[2px] flex-shrink-0">
            <T k="perf.presets">[ PRESETS ]</T>
         </div>

         <div className="flex-1 min-h-0 overflow-y-auto px-4 pb-2 flex flex-col gap-2">
            {PRESET_ORDER.map((name) => (
               <button
                  key={name}
                  type="button"
                  data-no-drag
                  data-preset={name}
                  aria-pressed={activePreset === name}
                  onClick={() => applyPreset(name)}
                  className={`preset-btn px-3.5 py-2.5 text-left flex-shrink-0 ${activePreset === name ? "active" : ""}`}
               >
                  <div className="preset-name text-sm font-bold tracking-wide">{translate("perf.preset." + name, name.toUpperCase())}</div>
                  <div className="text-[11px] text-amt-text-dim mt-0.5 leading-snug">{translate("perf.preset." + name + ".desc", "")}</div>
                  <PresetSpec name={name} />
               </button>
            ))}
         </div>

         <div className="flex-shrink-0 px-4 py-3 border-t border-amt-border text-[11px] text-amt-text-dim leading-relaxed">
            <div>{translate("perf.sidebar.output", "// output: autoexec.cfg", { file: "autoexec.cfg" })}</div>
            <div>{translate("perf.sidebar.path", "// path: dota/cfg/", { path: "dota/cfg/" })}</div>
         </div>
      </div>
   );
}
