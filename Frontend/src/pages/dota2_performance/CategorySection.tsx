import { translate, translatePlural, useLocale } from "../../bridge/i18n";
import { CvarRow } from "./CvarRow";
import { toggleCategory } from "./store";
import type { Category, CvarValues } from "./types";

export function CategorySection({
   name,
   category,
   collapsed,
   currentValues,
   originalValues,
}: {
   name: string;
   category: Category;
   collapsed: boolean;
   currentValues: CvarValues;
   originalValues: CvarValues;
}) {
   useLocale();
   const catId = name.toLowerCase().replace(/[^a-z0-9]+/g, "_");
   const title = translate("perf.category." + catId, name);
   const cvarEntries = Object.entries(category.cvars);
   const countLabel = translatePlural("perf.cvarCount", cvarEntries.length);

   return (
      <div className="glass rounded-lg overflow-hidden fade-in">
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
            <div className="flex items-center gap-3">
               <span className="text-sm font-bold tracking-wider">[ {title} ]</span>
               <span className="text-xs text-amt-text-dim">{countLabel}</span>
            </div>
            <span className="chevron text-amt-text-muted text-xs transition-transform duration-200" style={{ transform: collapsed ? "rotate(-90deg)" : "" }}>
               ▼
            </span>
         </div>
         <div className={`category-content ${collapsed ? "" : "open"}`}>
            {cvarEntries.map(([cvar, cfg]) => {
               const value = currentValues[cvar] ?? cfg.default;
               const isModified = originalValues[cvar] !== undefined && originalValues[cvar] !== value;
               return <CvarRow key={cvar} cvar={cvar} cfg={cfg} value={value} isModified={isModified} />;
            })}
         </div>
      </div>
   );
}
