import { T, useLocale } from "../../bridge/i18n";
import { getActiveStyleIndex, getHeroSelection } from "./helpers";
import { closeStylePreview, deselectStylePreview, selectStyleFromPreview, store } from "./store";
import css from "./gallery.module.css";

export function StylePreviewModal() {
   const { t } = useLocale();
   const heroes = store.use((s) => s.heroes);
   const selections = store.use((s) => s.selections);
   const stylePreview = store.use((s) => s.stylePreview);

   if (!stylePreview) return null;
   const hero = heroes.find((h) => h.id === stylePreview.heroId);
   if (!hero) return null;

   const { tileType, groupIndices, group, groupCover } = stylePreview;
   const heroSel = getHeroSelection(selections, hero.id);
   const activeIdx = getActiveStyleIndex(groupIndices, heroSel, tileType);

   const entries = groupIndices.map((idx) => ({ idx, set: hero.sets[idx]! }));
   const active = activeIdx !== null ? entries.find((e) => e.idx === activeIdx) : entries[0];
   const headThumb = activeIdx !== null ? active?.set.thumbnailUrl || groupCover || hero.thumbnail : groupCover || active?.set.thumbnailUrl || hero.thumbnail;

   return (
      <div
         id="stylePreviewModal"
         className={css.stylePreviewBackdrop}
         onClick={closeStylePreview}
      >
         <div className={css.stylePreviewPanel} onClick={(e) => e.stopPropagation()}>
            <div className={css.stylePreviewHeader}>
               <div className={css.stylePreviewHeaderThumb}>
                  <img id="stylePreviewThumb" src={headThumb} alt="" />
               </div>
               <div className={css.stylePreviewHeaderInfo}>
                  <div id="stylePreviewTitle" className={css.stylePreviewHeaderTitle}>
                     {group}
                  </div>
                  <div id="stylePreviewSub" className={css.stylePreviewHeaderSub}>
                     {entries.length} style variant{entries.length > 1 ? "s" : ""} · {hero.displayName || hero.name}
                  </div>
               </div>
               <button type="button" data-no-drag className={css.stylePreviewClose} onClick={closeStylePreview} title={t("common.close", "Close")} aria-label={t("common.close", "Close")}>
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <path d="M6 18L18 6M6 6l12 12" />
                  </svg>
               </button>
            </div>

            <div id="stylePreviewGrid" className={css.stylePreviewGrid}>
               {entries.map((e) => {
                  const isActive = e.idx === activeIdx;
                  const label = e.set.styleLabel || e.set.name || `Style ${e.idx + 1}`;
                  return (
                     <div
                        key={e.idx}
                        className={`${css.stylePreviewTile} ${isActive ? css.active : ""}`}
                        onClick={() => selectStyleFromPreview(hero, e.idx)}
                     >
                        <div className={css.stylePreviewTileImg}>
                           <img
                              src={e.set.thumbnailUrl || hero.thumbnail}
                              alt={label}
                              onError={(ev) => {
                                 ev.currentTarget.src = hero.thumbnail;
                              }}
                           />
                        </div>
                        <div className={css.stylePreviewTileCheck}>
                           <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                              <path d="M5 13l4 4L19 7" />
                           </svg>
                        </div>
                        <div className={css.stylePreviewTileLabel}>{label}</div>
                     </div>
                  );
               })}
            </div>

            <div className={css.stylePreviewFooter}>
               <button type="button" data-no-drag className={`${css.btn} ${css.ghost}`} onClick={deselectStylePreview}>
                  <T k="common.deselect">Deselect</T>
               </button>
               <button type="button" data-no-drag className={`${css.btn} ${css.primary}`} onClick={closeStylePreview}>
                  <T k="common.done">Done</T>
               </button>
            </div>
         </div>
      </div>
   );
}
