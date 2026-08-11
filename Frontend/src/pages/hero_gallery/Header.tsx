import type { RefObject } from "react";
import { T, translate, useLocale } from "../../bridge/i18n";
import { ATTRIBUTE_ICONS } from "./attributeIcons";
import type { FilterCategory } from "./types";
import css from "./gallery.module.css";

const ATTR_FILTERS: { id: FilterCategory; labelKey: string; label: string }[] = [
   { id: "str", labelKey: "filter.strength", label: "Strength" },
   { id: "agi", labelKey: "filter.agility", label: "Agility" },
   { id: "int", labelKey: "filter.intelligence", label: "Intelligence" },
   { id: "universal", labelKey: "filter.universal", label: "Universal" },
];

export function Header({
   filter,
   search,
   onlyWithSets,
   selectionCount,
   onFilterChange,
   onSearchChange,
   onToggleHasSets,
   onSave,
   onLoad,
   onClearAll,
   onGenerate,
   searchInputRef,
}: {
   filter: FilterCategory;
   search: string;
   onlyWithSets: boolean;
   selectionCount: number;
   onFilterChange: (cat: FilterCategory) => void;
   onSearchChange: (q: string) => void;
   onToggleHasSets: () => void;
   onSave: () => void;
   onLoad: () => void;
   onClearAll: () => void;
   onGenerate: () => void;
   searchInputRef: RefObject<HTMLInputElement>;
}) {
   const { t } = useLocale();

   return (
      <header className={css.galleryHeader}>
         <div className={css.ghRows}>
            <div className={css.ghTop}>
               <div className={css.search}>
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                  </svg>
                  <input
                     ref={searchInputRef}
                     id="searchInput"
                     type="text"
                     value={search}
                     onChange={(e) => onSearchChange(e.target.value)}
                     placeholder={t("progress.preview.search", "Search heroes...")}
                  />
               </div>

               <div className={css.ghActions}>
                  <button type="button" data-no-drag className={`${css.btn} ${css.ghost}`} onClick={onSave} title={t("heroGallery.savePreset.title", "Save selections to file")}>
                     <T k="common.save">Save</T>
                  </button>
                  <button type="button" data-no-drag className={`${css.btn} ${css.ghost}`} onClick={onLoad} title={t("heroGallery.loadPreset.title", "Load selections from file")}>
                     <T k="common.load">Load</T>
                  </button>
                  <button type="button" data-no-drag className={`${css.btn} ${css.ghost}`} onClick={onClearAll}>
                     <T k="heroGallery.clearAll">Clear All</T>
                  </button>
                  <span className={css.ghDivider} aria-hidden="true" />
                  <div className={css.selCounter}>
                     <span id="selectionCount">{selectionCount}</span>
                     <T k="heroGallery.selected">Selected</T>
                  </div>
                  <button type="button" data-no-drag className={`${css.btn} ${css.primary}`} onClick={onGenerate}>
                     <T k="heroGallery.generate">Generate ModsPack</T>
                  </button>
               </div>
            </div>

            <div className={css.ghFilters}>
               <button type="button" data-no-drag className={`${css.filterPill} ${filter === "all" ? css.active : ""}`} onClick={() => onFilterChange("all")}>
                  <T k="heroGallery.allHeroes">All Heroes</T>
               </button>
               <button
                  type="button"
                  data-no-drag
                  className={`${css.togglePill} ${onlyWithSets ? css.active : ""}`}
                  onClick={onToggleHasSets}
                  aria-pressed={onlyWithSets}
                  title={t("heroGallery.hasSetsToggle.title", "Show only heroes that have custom sets")}
               >
                  <span className={css.toggleDot}>{onlyWithSets ? "●" : "○"}</span> <T k="heroGallery.hasSets">Has Sets</T>
               </button>
               {ATTR_FILTERS.map((f) => (
                  <button key={f.id} type="button" data-no-drag className={`${css.filterPill} ${filter === f.id ? css.active : ""}`} onClick={() => onFilterChange(f.id)}>
                     <img src={ATTRIBUTE_ICONS[f.id]} alt={translate(f.labelKey, f.label)} />
                     <T k={f.labelKey}>{f.label}</T>
                  </button>
               ))}
               <div className={css.ghSpacer} />
               <button type="button" data-no-drag className={`${css.filterPill} ${filter === "favorites" ? css.active : ""}`} onClick={() => onFilterChange("favorites")}>
                  <span className={css.star}>★</span> <T k="heroGallery.favorites">Favorites</T>
               </button>
            </div>
         </div>
      </header>
   );
}
