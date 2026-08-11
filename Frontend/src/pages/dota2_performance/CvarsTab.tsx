import { CategorySection } from "./CategorySection";
import { SETTINGS } from "./data";
import { store } from "./store";

export function CvarsTab() {
   const currentValues = store.use((s) => s.currentValues);
   const originalValues = store.use((s) => s.originalValues);
   const collapsedCategories = store.use((s) => s.collapsedCategories);

   return (
      <div id="tabCvars" className="flex-1 overflow-y-auto p-5 space-y-3">
         {Object.entries(SETTINGS).map(([name, category]) => (
            <CategorySection
               key={name}
               name={name}
               category={category}
               collapsed={!!collapsedCategories[name]}
               currentValues={currentValues}
               originalValues={originalValues}
            />
         ))}
      </div>
   );
}
