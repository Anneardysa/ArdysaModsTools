import { translate, translatePlural, useLocale } from "../../bridge/i18n";
import { CvarRow } from "./CvarRow";
import { Icon, type IconName } from "./icons";
import { toggleCategory } from "./store";
import type { CvarConfig, CvarValues } from "./types";

const CATEGORY_ICONS: Record<string, IconName> = {
   "DISPLAY & FPS": "display",
   "VISUAL TOGGLES": "eye",
   QUALITY: "graph",
   "ENGINE TWEAKS": "settings",
   "VSYNC & LATENCY": "timer",
   NETWORK: "globe",
};

export function CategorySection({
   name,
   entries,
   collapsed,
   currentValues,
   originalValues,
}: {
   name: string;
   entries: [string, CvarConfig][];
   collapsed: boolean;
   currentValues: CvarValues;
   originalValues: CvarValues;
}) {
   useLocale();
   const catId = name.toLowerCase().replace(/[^a-z0-9]+/g, "_");
   const title = translate("perf.category." + catId, name);
   const countLabel = translatePlural("perf.cvarCount", entries.length, `${entries.length} settings`);
   const value = (cvar: string, cfg: CvarConfig) => currentValues[cvar] ?? cfg.default;
   const isModified = (cvar: string, cfg: CvarConfig) => originalValues[cvar] !== undefined && originalValues[cvar] !== value(cvar, cfg);
   const changed = entries.filter(([cvar, cfg]) => isModified(cvar, cfg)).length;

   return (
      <div className="glass overflow-hidden">
         <div
            className="category-header flex items-center justify-between px-5 py-4"
            data-no-drag
            role="button"
            tabIndex={0}
            aria-expanded={!collapsed}
            onClick={() => toggleCategory(name)}
            onKeyDown={(e) => {
               if (e.key === "Enter" || e.key === " ") {
                  e.preventDefault();
                  toggleCategory(name);
               }
            }}
         >
            <div className="flex items-center gap-3 min-w-0">
               <Icon name={CATEGORY_ICONS[name] ?? "settings"} size={18} />
               <span className="text-sm font-bold tracking-wider">{title}</span>
               <span className="text-xs text-amt-text-dim">{countLabel}</span>
               {changed > 0 && <span className="text-xs text-amt-amber">{translatePlural("perf.category.changed", changed, `${changed} changed`)}</span>}
            </div>
            <span className="text-amt-text-dim flex transition-transform duration-200" style={{ transform: collapsed ? "rotate(-90deg)" : "" }}>
               <Icon name="chevron_down" size={14} />
            </span>
         </div>
         <div className={`category-content ${collapsed ? "" : "open"}`}>
            <div className="cvar-grid">
               {entries.map(([cvar, cfg]) => (
                  <CvarRow key={cvar} cvar={cvar} cfg={cfg} value={value(cvar, cfg)} isModified={isModified(cvar, cfg)} />
               ))}
            </div>
         </div>
      </div>
   );
}
