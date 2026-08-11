import { T, useLocale } from "../../bridge/i18n";
import { extractItemTag, getSetCategory } from "./helpers";
import type { Hero, HeroSelectionState } from "./types";
import css from "./gallery.module.css";

type Row = { idx: number; name: string; kind: string; thumb: string; onRemove: () => void };

export function SetCart({
   hero,
   heroSel,
   onRemoveSet,
   onRemoveItem,
   onRemoveBase,
   onRemovePrismatic,
}: {
   hero: Hero;
   heroSel: HeroSelectionState;
   onRemoveSet: (idx: number) => void;
   onRemoveItem: (idx: number) => void;
   onRemoveBase: (idx: number) => void;
   onRemovePrismatic: (idx: number) => void;
}) {
   const { t } = useLocale();
   const rows: Row[] = [];

   const push = (idx: number, kind: string, onRemove: (idx: number) => void) => {
      const set = hero.sets[idx];
      if (!set) return;
      const name = set.styleGroup && set.styleLabel ? `${set.styleGroup} · ${set.styleLabel}` : set.name || `Set ${idx + 1}`;
      rows.push({ idx, name, kind, thumb: set.thumbnailUrl || hero.thumbnail || "", onRemove: () => onRemove(idx) });
   };

   if (heroSel.set !== null) {
      push(heroSel.set, getSetCategory(hero.sets[heroSel.set]) === "persona" ? "Persona" : "Set", onRemoveSet);
   }
   heroSel.items.forEach((idx) => push(idx, extractItemTag(hero.sets[idx]) || "Item", onRemoveItem));
   if (heroSel.base !== null) push(heroSel.base, "Base Hero", onRemoveBase);
   if (heroSel.prismatic !== null) push(heroSel.prismatic, "Prismatic", onRemovePrismatic);

   return (
      <aside className={css.smCart}>
         <div className={css.smCartHead}>
            <span className={css.smCartTitle}>
               <T k="heroGallery.selected">Selected</T>
            </span>
            <span id="setCartCount" className={css.smCartCount}>
               {rows.length}
            </span>
         </div>
         <div id="setCartList" className={css.smCartList}>
            {rows.length === 0 ? (
               <div className={css.smCartEmpty}>{t("heroGallery.cartEmpty", "Nothing selected yet — pick a set, item or base from the catalog.")}</div>
            ) : (
               rows.map((r) => (
                  <div key={r.idx} className={css.cartChip} title={`${r.name} · ${r.kind}`}>
                     <img
                        src={r.thumb}
                        alt={r.name}
                        onError={(e) => {
                           e.currentTarget.style.visibility = "hidden";
                        }}
                     />
                     <button
                        type="button"
                        data-no-drag
                        className={css.cartChipX}
                        onClick={r.onRemove}
                        aria-label={`${t("heroGallery.cartRemove", "Remove from selection")} — ${r.name}`}
                     >
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                           <path d="M6 18L18 6M6 6l12 12" />
                        </svg>
                     </button>
                  </div>
               ))
            )}
         </div>
      </aside>
   );
}
