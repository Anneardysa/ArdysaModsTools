import { translate, useLocale } from "../../bridge/i18n";
import { getActiveStyleIndex, getCategoryTag, getSetDisplayName, getSetNoticeLines } from "./helpers";
import type { Hero, HeroSelectionState, SetEntry, TileType } from "./types";
import css from "./gallery.module.css";

const TILE_CLASS: Partial<Record<TileType, string>> = {
   item: "itemTile",
   base: "baseTile",
   persona: "personaTile",
   prismatic: "prismaticTile",
   effect: "itemTile",
};

export function NoticeBadge({ lines, className }: { lines: string[]; className?: string }) {
   if (lines.length === 0) return null;
   const text = lines.join("\n");
   return (
      <span className={[css.noticeBadge, className].filter(Boolean).join(" ")} role="img" title={text} aria-label={text}>
         !
      </span>
   );
}

export function SetTile({
   hero,
   set,
   idx,
   tileType,
   heroSel,
   groupIndices,
   isFocused,
   isHighlighted,
   isBlocked,
   isBlocking,
   blockerName,
   showCategoryHeaders,
   onHoverBlocked,
   onSelect,
   onOpenStylePreview,
   badgeIconUrl,
   includedIn,
}: {
   hero: Hero;
   set: SetEntry;
   idx: number;
   tileType: TileType;
   heroSel: HeroSelectionState;
   groupIndices: number[];
   isFocused: boolean;
   isHighlighted: boolean;
   isBlocked: boolean;
   isBlocking: boolean;
   blockerName: string | null;
   showCategoryHeaders: boolean;
   onHoverBlocked: (idx: number | null) => void;
   onSelect: () => void;
   onOpenStylePreview: (groupName: string) => void;
   badgeIconUrl?: string | null;
   includedIn?: string | null;
}) {
   const styleGroup = set.styleGroup || null;
   const hasStyles = !!styleGroup && groupIndices.length > 1;
   const activeStyleIdx = hasStyles ? getActiveStyleIndex(groupIndices, heroSel, tileType) : null;

   let isSelected: boolean;
   if (hasStyles) isSelected = activeStyleIdx !== null;
   else if (tileType === "item") isSelected = heroSel.items.includes(idx);
   else if (tileType === "base") isSelected = heroSel.base === idx;
   else if (tileType === "prismatic") isSelected = heroSel.prismatic === idx;
   else if (tileType === "effect") isSelected = heroSel.effects?.includes(idx) ?? false;
   else isSelected = heroSel.set === idx;

   const activeSet: SetEntry | null = hasStyles && activeStyleIdx !== null ? (hero.sets[activeStyleIdx] ?? null) : null;
   const groupCover = hasStyles ? set.styleGroupThumbnail || null : null;
   const thumbUrl = activeSet?.thumbnailUrl || groupCover || set.thumbnailUrl || hero.thumbnail;

   const activeLabel = activeSet && (activeSet.styleLabel || activeSet.name);
   const statusText = isSelected
      ? (hasStyles && activeLabel ? `✓ ${activeLabel}` : `✓ ${translate("heroGallery.selectedTile", "Selected")}`)
      : includedIn ? `✓ in ${includedIn}` : null;

   const displayName = styleGroup || getSetDisplayName(set, idx);
   const tag = tileType === "item" || tileType === "effect" || showCategoryHeaders ? null : getCategoryTag(set);
   const showToggle = tileType !== "set";

   useLocale();
   const notices = getSetNoticeLines(hero, hasStyles ? groupIndices : [idx]);

   const title = [
      includedIn
         ? `${displayName}: already part of ${includedIn}. Remove it to pick this effect on its own`
         : isBlocked
         ? blockerName
            ? `${displayName}: cannot be combined with ${blockerName}`
            : `${displayName}: cannot be combined with your current selection`
         : displayName,
      ...notices.map((n) => `⚠ ${n}`),
   ].join("\n");

   return (
      <div
         className={[
            css.setTile,
            TILE_CLASS[tileType] ? css[TILE_CLASS[tileType]!] : "",
            isSelected ? css.selected : "",
            isFocused ? css.focused : "",
            isHighlighted ? css.highlighted : "",
            isBlocked ? css.blocked : "",
            isBlocking ? css.blocking : "",
         ]
            .filter(Boolean)
            .join(" ")}
         data-set-index={idx}
         title={title}
         aria-disabled={isBlocked || undefined}
         onMouseEnter={isBlocked ? () => onHoverBlocked(idx) : undefined}
         onMouseLeave={isBlocked ? () => onHoverBlocked(null) : undefined}
         onClick={() => {
            if (isBlocked) return;
            if (hasStyles) onOpenStylePreview(styleGroup!);
            else onSelect();
         }}
      >
         <div className={css.setTileImg}>
            <img
               src={thumbUrl}
               alt={displayName}
               loading="lazy"
               decoding="async"
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
            {badgeIconUrl && (
               <img
                  className={css.abilityInset}
                  src={badgeIconUrl}
                  alt=""
                  loading="lazy"
                  decoding="async"
                  onError={(e) => {
                     e.currentTarget.style.display = "none";
                  }}
               />
            )}
            {hasStyles && <span className={css.styleCountBadge}>{groupIndices.length} styles</span>}
            <NoticeBadge lines={notices} />
         </div>
         <div className={css.setTileBody}>
            <span className={css.setTileName}>{displayName}</span>
            {tag && <span className={css.setTagBadge}>{tag}</span>}
            {statusText && <span className={css.setStatus}>{statusText}</span>}
         </div>
      </div>
   );
}
