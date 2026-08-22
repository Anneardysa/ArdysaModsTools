import type { RefObject } from "react";
import { send } from "../../bridge/host";
import { T, translate, useLocale } from "../../bridge/i18n";
import { ATTRIBUTE_ICONS } from "./attributeIcons";
import { setCooldown } from "./store";
import type { CooldownState, FilterCategory } from "./types";
import css from "./gallery.module.css";

const ATTR_FILTERS: { id: FilterCategory; labelKey: string; label: string }[] = [
   { id: "str", labelKey: "filter.strength", label: "Strength" },
   { id: "agi", labelKey: "filter.agility", label: "Agility" },
   { id: "int", labelKey: "filter.intelligence", label: "Intelligence" },
   { id: "universal", labelKey: "filter.universal", label: "Universal" },
];

function formatCooldown(totalSec: number): string {
   if (totalSec >= 3600) {
      const h = Math.floor(totalSec / 3600);
      const m = Math.floor((totalSec % 3600) / 60);
      return `${h}h ${m.toString().padStart(2, "0")}m`;
   }
   const m = Math.floor(totalSec / 60);
   const s = totalSec % 60;
   return `${m.toString().padStart(2, "0")}:${s.toString().padStart(2, "0")}`;
}

export function Header({
   filter,
   search,
   onlyWithSets,
   selectionCount,
   cooldown,
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
   cooldown: CooldownState;
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
   const isLocked = cooldown.active && cooldown.remainingSeconds > 0;
   const isDailyLimit = isLocked && cooldown.isDailyLimit;
   const timeStr = isLocked ? formatCooldown(cooldown.remainingSeconds) : "";
   const maxQuota = cooldown.dailyMax || 0;
   const remainingQuota = maxQuota > 0 ? Math.max(0, maxQuota - (cooldown.dailyUsed || 0)) : 0;

   const handleGenerateClick = (e: React.MouseEvent) => {
      if (e.shiftKey || e.ctrlKey) {
         send("resetCooldown");
         setCooldown({ active: false, remainingSeconds: 0, totalSeconds: 600, dailyUsed: 0, dailyMax: 0, isDailyLimit: false });
         return;
      }
      onGenerate();
   };

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
                  <button
                     type="button"
                     data-no-drag
                     className={`${css.btn} ${isLocked ? css.cooldownBtn : css.primary}`}
                     onClick={handleGenerateClick}
                     title={
                        isDailyLimit
                           ? t("hero.cooldown.dailyLimitTitle", `Daily limit reached (${timeStr} until reset)`, { time: timeStr })
                           : isLocked
                           ? t("hero.status.cooldownActive", `Generation on cooldown (${timeStr} remaining)`, { time: timeStr })
                           : maxQuota > 0
                           ? t("hero.cooldown.quotaStatus", `${remainingQuota} generations remaining today`, { remaining: remainingQuota })
                           : t("heroGallery.generate", "Generate ModsPack")
                     }
                  >
                     {isDailyLimit ? (
                        <>
                           <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className={css.btnIcon} aria-hidden="true">
                              <circle cx="12" cy="12" r="10" />
                              <polyline points="12 6 12 12 16 14" />
                           </svg>
                           <T k="hero.cooldown.dailyLimitButton" vars={{ time: timeStr }}>Daily Limit ({timeStr})</T>
                        </>
                     ) : isLocked ? (
                        <>
                           <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className={css.btnIcon} aria-hidden="true">
                              <circle cx="12" cy="12" r="10" />
                              <polyline points="12 6 12 12 16 14" />
                           </svg>
                           <T k="hero.cooldown.button" vars={{ time: timeStr }}>Cooldown ({timeStr})</T>
                        </>
                     ) : (
                        <>
                           <T k="heroGallery.generate">Generate ModsPack</T>
                           {maxQuota > 0 && cooldown.dailyUsed > 0 && <span style={{ opacity: 0.85, fontSize: "0.85em", marginLeft: 4 }}>({remainingQuota}/{maxQuota})</span>}
                        </>
                     )}
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
