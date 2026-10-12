import { T, translate, useLocale } from "../../bridge/i18n";
import { CategorySection } from "./CategorySection";
import { SETTINGS } from "./data";
import { Icon } from "./icons";
import { MapCleanupCard } from "./MapCleanupCard";
import { setSearch, store } from "./store";
import type { CvarConfig } from "./types";

function matches(cvar: string, cfg: CvarConfig, q: string): boolean {
   return [cvar, translate("perf.cvar." + cvar + ".label", cfg.label), translate("perf.cvar." + cvar + ".tip", cfg.tip)].some((s) => s.toLowerCase().includes(q));
}

export function CvarsTab() {
   useLocale();
   const currentValues = store.use((s) => s.currentValues);
   const originalValues = store.use((s) => s.originalValues);
   const collapsedCategories = store.use((s) => s.collapsedCategories);
   const search = store.use((s) => s.search);
   const q = search.trim().toLowerCase();

   const sections = Object.entries(SETTINGS)
      .map(([name, category]) => ({ name, entries: Object.entries(category.cvars).filter(([cvar, cfg]) => !q || matches(cvar, cfg, q)) }))
      .filter((s) => s.entries.length > 0);

   return (
      <div id="tabCvars" className="flex-1 overflow-y-auto p-5 space-y-3">
         {!q && <MapCleanupCard />}

         {sections.map(({ name, entries }) => (
            <CategorySection
               key={name}
               name={name}
               entries={entries}
               // A search shows every match, so it opens collapsed categories rather than hiding hits.
               collapsed={!q && !!collapsedCategories[name]}
               currentValues={currentValues}
               originalValues={originalValues}
            />
         ))}

         {sections.length === 0 && (
            <div className="glass px-5 py-8 flex flex-col items-center gap-3 text-center">
               <Icon name="search" size={22} className="text-amt-text-dim" />
               <div className="text-sm text-amt-text">{translate("perf.search.empty", "No setting matches \"{query}\".", { query: search.trim() })}</div>
               <button type="button" data-no-drag onClick={() => setSearch("")} className="action-btn px-4 py-2 text-xs font-bold border border-amt-border-strong text-amt-text-dim hover:text-amt-text">
                  <T k="perf.search.clear">Clear search</T>
               </button>
            </div>
         )}
      </div>
   );
}
