import { extractItemTag, getActiveStyleIndex, getCategoryTag } from "./helpers";
import type { Hero, HeroSelectionState, SetEntry, TileType } from "./types";
import css from "./gallery.module.css";

const TILE_CLASS: Partial<Record<TileType, string>> = {
   item: "itemTile",
   base: "baseTile",
   persona: "personaTile",
   prismatic: "prismaticTile",
};

export function SetTile({
   hero,
   set,
   idx,
   tileType,
   heroSel,
   isFocused,
   isHighlighted,
   onSelect,
   onOpenStylePreview,
}: {
   hero: Hero;
   set: SetEntry;
   idx: number;
   tileType: TileType;
   heroSel: HeroSelectionState;
   isFocused: boolean;
   isHighlighted: boolean;
   onSelect: () => void;
   onOpenStylePreview: (groupName: string) => void;
}) {
   const styleGroup = set.styleGroup || null;
   const groupIndices = styleGroup ? hero.sets.reduce<number[]>((acc, s, i) => (s.styleGroup === styleGroup ? [...acc, i] : acc), []) : [];
   const hasStyles = !!styleGroup && groupIndices.length > 1;
   const activeStyleIdx = hasStyles ? getActiveStyleIndex(groupIndices, heroSel, tileType) : null;

   let isSelected: boolean;
   if (hasStyles) isSelected = activeStyleIdx !== null;
   else if (tileType === "item") isSelected = heroSel.items.includes(idx);
   else if (tileType === "base") isSelected = heroSel.base === idx;
   else if (tileType === "prismatic") isSelected = heroSel.prismatic === idx;
   else isSelected = heroSel.set === idx;

   const activeSet: SetEntry | null = hasStyles && activeStyleIdx !== null ? (hero.sets[activeStyleIdx] ?? null) : null;
   const groupCover = hasStyles ? set.styleGroupThumbnail || null : null;
   const thumbUrl = activeSet?.thumbnailUrl || groupCover || set.thumbnailUrl || hero.thumbnail;

   const activeLabel = activeSet && (activeSet.styleLabel || activeSet.name);
   const statusText = isSelected ? (hasStyles && activeLabel ? `✓ ${activeLabel}` : "✓ Selected") : null;

   const tag = tileType === "item" ? extractItemTag(set) : getCategoryTag(set);
   const showToggle = tileType === "item" || tileType === "base" || tileType === "persona" || tileType === "prismatic";
   const displayName = styleGroup || set.name || `Set ${idx + 1}`;

   return (
      <div
         className={[css.setTile, TILE_CLASS[tileType] ? css[TILE_CLASS[tileType]!] : "", isSelected ? css.selected : "", isFocused ? css.focused : "", isHighlighted ? css.highlighted : ""]
            .filter(Boolean)
            .join(" ")}
         data-set-index={idx}
         onClick={() => (hasStyles ? onOpenStylePreview(styleGroup!) : onSelect())}
      >
         <div className={css.setTileImg}>
            <img
               src={thumbUrl}
               alt={displayName}
               onError={(e) => {
                  e.currentTarget.src = hero.thumbnail;
               }}
            />
            {showToggle && (
               <div className={css.toggleIndicator}>
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <path d="M5 13l4 4L19 7" />
                  </svg>
               </div>
            )}
            {hasStyles && <span className={css.styleCountBadge}>{groupIndices.length} styles</span>}
         </div>
         <div className={css.setTileBody}>
            {tag && <span className={tileType === "item" ? css.itemTagBadge : css.setTagBadge}>{tag}</span>}
            {statusText && <span className={css.setStatus}>{statusText}</span>}
         </div>
      </div>
   );
}
