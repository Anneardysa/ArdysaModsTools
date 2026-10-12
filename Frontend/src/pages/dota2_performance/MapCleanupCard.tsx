import { T, translate, useLocale } from "../../bridge/i18n";
import { Icon } from "./icons";
import { store, toggleCvar } from "./store";
import type { MapCleanupKey } from "./types";

const ROWS: { key: MapCleanupKey; id: string; label: string; tip: string }[] = [
   { key: "amt_remove_foliage", id: "foliage", label: "Remove Foliage", tip: "Bushes, grass, flowers, vines and roots. Default terrain only." },
   { key: "amt_remove_props", id: "props", label: "Remove Map Props", tip: "Rocks, bones, debris, walls, water edges, wildlife and banners. Default terrain only." },
];

export function MapCleanupCard() {
   useLocale();
   const currentValues = store.use((s) => s.currentValues);
   const originalValues = store.use((s) => s.originalValues);
   const mounted = store.use((s) => s.mapCleanup.mounted);
   const busy = store.use((s) => s.busy);

   return (
      <section className="glass" aria-labelledby="mapCleanupTitle">
         <div className="flex items-center justify-between gap-4 px-5 pt-4 pb-3">
            <div id="mapCleanupTitle" className="flex items-center gap-3">
               <Icon name="terrain" size={18} />
               <span className="text-sm font-bold tracking-wider whitespace-nowrap">
                  <T k="perf.cleanup.title">MAP CLEANUP</T>
               </span>
            </div>
            <span className="text-xs text-amt-text-dim text-right">
               // <T k="perf.cleanup.hint">applied with APPLY, removed with DELETE: restart Dota 2 to see it</T>
            </span>
         </div>

         {!mounted && (
            <div className="mx-5 mb-3 flex items-center gap-2 text-xs font-semibold text-amt-amber">
               <Icon name="warning" size={14} />
               <T k="perf.cleanup.notMounted">Mods are not active in Dota 2: run Patch Update so these files load.</T>
            </div>
         )}

         <div className="grid grid-cols-2 gap-3 px-5 pb-5">
            {ROWS.map(({ key, id, label, tip }) => {
               const isOn = (currentValues[key] ?? "0") !== "0";
               const installed = originalValues[key];
               const isModified = installed !== undefined && installed !== (currentValues[key] ?? "0");
               const name = translate(`perf.cleanup.${id}.label`, label);
               return (
                  <button key={key} type="button" role="switch" aria-checked={isOn} data-no-drag disabled={busy} className={`map-tile ${isOn ? "on" : ""}`} onClick={() => toggleCvar(key)}>
                     <div className="flex-1 min-w-0">
                        <div className="flex items-center">
                           <span className="text-sm font-semibold">{name}</span>
                           {isModified && (
                              <span className="diff-badge">
                                 <T k="perf.cvar.modified">mod</T>
                              </span>
                           )}
                        </div>
                        <div className="text-xs text-amt-text-dim mt-1 leading-relaxed">{translate(`perf.cleanup.${id}.tip`, tip)}</div>
                        {installed !== undefined && (
                           <div className="map-state">
                              <Icon name={installed === "1" ? "check_circle" : "disabled"} size={12} />
                              {installed === "1" ? translate("perf.cleanup.installed", "in game now") : translate("perf.cleanup.notInstalled", "not in game")}
                           </div>
                        )}
                     </div>
                     <span className={`toggle-track ${isOn ? "on" : ""}`} aria-hidden="true">
                        <span className="toggle-thumb" />
                     </span>
                  </button>
               );
            })}
         </div>
      </section>
   );
}
