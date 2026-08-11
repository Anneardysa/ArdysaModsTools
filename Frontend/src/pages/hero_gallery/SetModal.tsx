import { useEffect, useState } from "react";
import { T, useLocale } from "../../bridge/i18n";
import { getSetCategory, isBaseActive, isPersonaActive } from "./helpers";
import {
   closeModal,
   deselectHero,
   navigateHero,
   openStylePreview,
   selectSet,
   store,
   toggleBase,
   toggleItem,
   togglePrismatic,
} from "./store";
import { SetCart } from "./SetCart";
import { SetTile } from "./SetTile";
import type { Hero, HeroSelectionState, SetEntry, TileType } from "./types";
import css from "./gallery.module.css";

type Category = {
   key: string;
   label: string;
   tileType: TileType;
   entries: { set: SetEntry; idx: number }[];
   notice: string | null;
   locked?: boolean;
};

function buildCategories(hero: Hero, heroSel: HeroSelectionState): Category[] {
   const legacy: { set: SetEntry; idx: number }[] = [];
   const custom: { set: SetEntry; idx: number }[] = [];
   const persona: { set: SetEntry; idx: number }[] = [];
   const item: { set: SetEntry; idx: number }[] = [];
   const base: { set: SetEntry; idx: number }[] = [];
   const prismatic: { set: SetEntry; idx: number }[] = [];

   hero.sets.forEach((set, idx) => {
      switch (getSetCategory(set)) {
         case "persona":
            persona.push({ set, idx });
            break;
         case "item":
            item.push({ set, idx });
            break;
         case "basehero":
            base.push({ set, idx });
            break;
         case "customset":
            custom.push({ set, idx });
            break;
         case "prismatic":
            prismatic.push({ set, idx });
            break;
         default:
            legacy.push({ set, idx });
      }
   });

   const personaActive = isPersonaActive(hero, heroSel);
   const baseActive = isBaseActive(heroSel);
   const hasItemsSelected = heroSel.items.length > 0;

   const categories: Category[] = [
      { key: "legacy", label: "Legacy Set", tileType: "set", entries: legacy, notice: null },
      { key: "custom", label: "Custom Set", tileType: "set", entries: custom, notice: null },
      {
         key: "persona",
         label: "Persona",
         tileType: "persona",
         entries: persona,
         notice: hasItemsSelected ? "⚠ Selecting a persona will clear items and base" : null,
      },
      {
         key: "item",
         label: "Items",
         tileType: "item",
         entries: item,
         notice: personaActive ? "⚠ Items are disabled while a persona is active" : null,
      },
      {
         key: "base",
         label: "Base Hero",
         tileType: "base",
         entries: base,
         notice: personaActive ? "⚠ Base is disabled while a persona is active" : null,
      },
      {
         key: "prismatic",
         label: "Prismatic",
         tileType: "prismatic",
         entries: prismatic,
         notice: baseActive ? null : "⚠ Select a Base Hero first to enable Prismatic",
         locked: !baseActive,
      },
   ];
   return categories.filter((c) => c.entries.length > 0);
}

const TILE = 133;
const GAP = 12;
const CHROME = 42;
const TRAY = 233;

function panelMaxWidth(categories: Category[]): number {
   const counts = categories.map((c) => {
      const seen = new Set<string>();
      let n = 0;
      for (const { set } of c.entries) {
         const group = set.styleGroup;
         if (group) {
            if (seen.has(group)) continue;
            seen.add(group);
         }
         n++;
      }
      return n;
   });
   const cols = Math.min(5, Math.max(3, ...counts, 3));
   return cols * TILE + (cols - 1) * GAP + CHROME + TRAY;
}

export function SetModal() {
   const { t } = useLocale();
   const heroes = store.use((s) => s.heroes);
   const selections = store.use((s) => s.selections);
   const modalHeroId = store.use((s) => s.modalHeroId);
   const focusedSetIndex = store.use((s) => s.focusedSetIndex);
   const highlightSetIndex = store.use((s) => s.highlightSetIndex);
   const [slideDir, setSlideDir] = useState<1 | -1 | 0>(0);

   const hero = heroes.find((h) => h.id === modalHeroId) ?? null;

   useEffect(() => {
      if (focusedSetIndex < 0) return;
      const tile = document.querySelector(`[data-set-index="${focusedSetIndex}"]`);
      tile?.scrollIntoView({ behavior: "smooth", block: "nearest" });
   }, [focusedSetIndex, modalHeroId]);

   useEffect(() => {
      if (highlightSetIndex == null) return;
      const tile = document.querySelector(`[data-set-index="${highlightSetIndex}"]`);
      tile?.scrollIntoView({ behavior: "smooth", block: "nearest" });
   }, [highlightSetIndex]);

   if (!hero) return null;

   const heroSel: HeroSelectionState = selections[hero.id] ?? { set: null, items: [], base: null, prismatic: null };
   const categories = buildCategories(hero, heroSel);
   const showHeaders = categories.length > 1;
   const maxWidth = panelMaxWidth(categories);

   const goHero = (dir: 1 | -1) => {
      setSlideDir(dir);
      navigateHero(dir);
   };

   return (
      <div
         id="setModal"
         className={css.modalBackdrop}
         onClick={(e) => {
            if (e.target === e.currentTarget) closeModal();
         }}
      >
         <div className={css.smWrap}>
            <button
               type="button"
               data-no-drag
               className={css.navArrow}
               onClick={() => goHero(-1)}
               title={t("heroGallery.prevHero.title", "Previous hero")}
               aria-label={t("heroGallery.prevHero.title", "Previous hero")}
            >
               <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <path d="M15 19l-7-7 7-7" />
               </svg>
            </button>

            <div className={`${css.smPanel} ${css.animateScaleIn}`} style={{ maxWidth }}>
               <div className={css.smHead}>
                  <div className={css.smHeadLeft}>
                     <img id="modalHeroImg" src={hero.thumbnail} alt="" />
                     <div>
                        <h2 id="modalHeroName">{hero.displayName || hero.name}</h2>
                        <p className={css.smHint}>
                           <T k="heroGallery.smHint">↑↓ browse sets · Enter select · ←→ change hero</T>
                        </p>
                     </div>
                  </div>
                  <button type="button" data-no-drag className={css.smX} onClick={closeModal} title={t("common.close", "Close")} aria-label={t("common.close", "Close")}>
                     <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <path d="M6 18L18 6M6 6l12 12" />
                     </svg>
                  </button>
               </div>

               <div className={css.smBody}>
                  <div
                     id="modalSetsGrid"
                     className={`${css.smSetsGrid} ${slideDir === 1 ? css.animateSlideLeft : slideDir === -1 ? css.animateSlideRight : ""}`}
                     key={hero.id}
                  >
                     {categories.map((cat) => {
                        const renderedGroups = new Set<string>();
                        return (
                           <div key={cat.key} className={css.setCategorySection}>
                              {showHeaders && (
                                 <div className={css.setCategoryHeader}>
                                    <span className={css.categoryLabel}>{cat.label}</span>
                                    <div className={css.categoryLine} />
                                    <span className={css.categoryCount}>{cat.entries.length}</span>
                                 </div>
                              )}
                              {cat.notice && <div className={css.exclusionNotice}>{cat.notice}</div>}
                              <div className={`${css.setCategoryGrid} ${cat.locked ? css.categoryLocked : ""}`}>
                                 {cat.entries.map(({ set, idx }) => {
                                    if (set.styleGroup) {
                                       if (renderedGroups.has(set.styleGroup)) return null;
                                       renderedGroups.add(set.styleGroup);
                                    }
                                    return (
                                       <SetTile
                                          key={idx}
                                          hero={hero}
                                          set={set}
                                          idx={idx}
                                          tileType={cat.tileType}
                                          heroSel={heroSel}
                                          isFocused={focusedSetIndex === idx}
                                          isHighlighted={highlightSetIndex === idx}
                                          onSelect={() => {
                                             if (cat.tileType === "item") toggleItem(hero, idx);
                                             else if (cat.tileType === "base") toggleBase(hero, idx);
                                             else if (cat.tileType === "prismatic") togglePrismatic(hero, idx);
                                             else selectSet(hero, idx);
                                          }}
                                          onOpenStylePreview={(group) => openStylePreview(hero, group, cat.tileType)}
                                       />
                                    );
                                 })}
                              </div>
                           </div>
                        );
                     })}
                  </div>

                  <SetCart
                     hero={hero}
                     heroSel={heroSel}
                     onRemoveSet={(idx) => selectSet(hero, idx)}
                     onRemoveItem={(idx) => toggleItem(hero, idx)}
                     onRemoveBase={(idx) => toggleBase(hero, idx)}
                     onRemovePrismatic={(idx) => togglePrismatic(hero, idx)}
                  />
               </div>

               <div className={css.smFoot}>
                  <button
                     type="button"
                     data-no-drag
                     className={`${css.btn} ${css.ghost}`}
                     onClick={() => {
                        deselectHero(hero.id);
                        closeModal();
                     }}
                  >
                     <T k="heroGallery.removeSelection">Remove Selection</T>
                  </button>
                  <button type="button" data-no-drag className={`${css.btn} ${css.primary}`} onClick={closeModal}>
                     <T k="common.done">Done</T>
                  </button>
               </div>
            </div>

            <button
               type="button"
               data-no-drag
               className={css.navArrow}
               onClick={() => goHero(1)}
               title={t("heroGallery.nextHero.title", "Next hero")}
               aria-label={t("heroGallery.nextHero.title", "Next hero")}
            >
               <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <path d="M9 5l7 7-7 7" />
               </svg>
            </button>
         </div>
      </div>
   );
}
