import { T, useLocale } from "../../bridge/i18n";
import { getSelectionSummary, hasAnySelection, customSetCount } from "./helpers";
import type { Hero, Selections } from "./types";
import css from "./gallery.module.css";

function HeroCard({
   hero,
   index,
   selected,
   favorite,
   summary,
   onOpen,
   onToggleFavorite,
}: {
   hero: Hero;
   index: number;
   selected: boolean;
   favorite: boolean;
   summary: string | null;
   onOpen: () => void;
   onToggleFavorite: () => void;
}) {
   const { t } = useLocale();
   const setCount = customSetCount(hero);
   const locked = setCount === 0;
   const name = hero.displayName || hero.name;
   const noSetsLabel = t("heroGallery.noSetsYet", "No sets yet");

   return (
      <div
         className={`${css.heroCard} ${css.animateFadeIn} ${selected ? css.selected : ""} ${locked ? css.locked : ""}`}
         data-hero-id={hero.id}
         style={{ animationDelay: `${Math.min(index * 15, 200)}ms` }}
      >
         <div
            className={css.hcInner}
            role={locked ? undefined : "button"}
            tabIndex={locked ? undefined : 0}
            aria-label={locked ? undefined : name}
            onClick={locked ? undefined : onOpen}
            onKeyDown={
               locked
                  ? undefined
                  : (e) => {
                       if (e.key === "Enter" || e.key === " ") {
                          e.preventDefault();
                          onOpen();
                       }
                    }
            }
         >
            <div className={css.hcThumb}>
               <img
                  className={css.hcImg}
                  src={hero.thumbnail || ""}
                  alt={name}
                  onError={(e) => {
                     e.currentTarget.src =
                        "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'%3E%3Crect fill='%23111' width='100' height='100'/%3E%3Ctext x='50' y='55' text-anchor='middle' fill='%23333' font-size='12'%3E?%3C/text%3E%3C/svg%3E";
                  }}
               />
               <div className={css.hcVeil} />
               {locked ? (
                  <div className={css.hcLock} title={noSetsLabel}>
                     <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <path d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
                     </svg>
                  </div>
               ) : (
                  <>
                     <div className={css.checkIndicator}>
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                           <path d="M5 13l4 4L19 7" />
                        </svg>
                     </div>
                     <button
                        type="button"
                        data-no-drag
                        className={`${css.hcFav} ${favorite ? css.on : ""}`}
                        aria-label={favorite ? "Remove from favorites" : "Add to favorites"}
                        aria-pressed={favorite}
                        onClick={(e) => {
                           e.stopPropagation();
                           onToggleFavorite();
                        }}
                     >
                        {favorite ? "★" : "☆"}
                     </button>
                  </>
               )}
               <div className={css.hcMeta}>
                  <div className={css.hcName}>{name}</div>
                  {locked ? (
                     <div className={`${css.hcSub} ${css.muted}`}>{noSetsLabel}</div>
                  ) : summary ? (
                     <div className={css.hcSub}>{summary}</div>
                  ) : (
                     <div className={`${css.hcSub} ${css.muted}`}>
                        {setCount} set{setCount > 1 ? "s" : ""}
                     </div>
                  )}
               </div>
            </div>
         </div>
      </div>
   );
}

export function HeroGrid({
   heroes,
   selections,
   favorites,
   onOpen,
   onToggleFavorite,
}: {
   heroes: Hero[];
   selections: Selections;
   favorites: Set<string>;
   onOpen: (heroId: string) => void;
   onToggleFavorite: (heroId: string) => void;
}) {
   if (heroes.length === 0) {
      return (
         <div id="emptyState" className={`${css.flex} ${css.emptyState}`}>
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
               <path d="M9.172 16.172a4 4 0 015.656 0M9 10h.01M15 10h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            <p className={css.esTitle}>
               <T k="progress.preview.empty">No heroes found</T>
            </p>
            <p className={css.esSub}>
               <T k="heroGallery.noHeroesSub">Try adjusting your search or filters</T>
            </p>
         </div>
      );
   }

   return (
      <div id="heroGrid" className={css.heroGrid}>
         {heroes.map((hero, index) => {
            const sel = selections[hero.id];
            return (
               <HeroCard
                  key={hero.id}
                  hero={hero}
                  index={index}
                  selected={hasAnySelection(sel)}
                  favorite={favorites.has(hero.id)}
                  summary={getSelectionSummary(hero, sel)}
                  onOpen={() => onOpen(hero.id)}
                  onToggleFavorite={() => onToggleFavorite(hero.id)}
               />
            );
         })}
      </div>
   );
}
