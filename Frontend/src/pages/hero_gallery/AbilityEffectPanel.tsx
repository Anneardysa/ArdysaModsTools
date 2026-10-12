import { useState, type ReactNode } from "react";
import { useLocale } from "../../bridge/i18n";
import { carrierOf } from "./helpers";
import { toggleEffect } from "./store";
import type { Hero, HeroSelectionState, SetEntry } from "./types";
import css from "./gallery.module.css";

export type EffectGroup = { key: string; label: string; entries: { set: SetEntry; idx: number }[] };

export function abilityIconOf(hero: Hero, tag: string): string | null {
   return hero.abilityIcons?.[tag] ?? null;
}

export function resolveActiveTag(groups: EffectGroup[], heroSel: HeroSelectionState, requested: string | null): string | null {
   if (requested && groups.some((g) => g.key === requested)) return requested;
   const picked = new Set(heroSel.effects ?? []);
   return (groups.find((g) => g.entries.some((e) => picked.has(e.idx))) ?? groups[0])?.key ?? null;
}

function AbilityIcon({ url, label, className }: { url: string | null; label: string; className?: string }) {
   const [failed, setFailed] = useState(false);
   if (!url || failed) {
      return (
         <span className={`${className} ${css.abilityIconFallback}`} aria-hidden="true">
            {label.charAt(0).toUpperCase()}
         </span>
      );
   }
   return <img className={className} src={url} alt="" loading="lazy" decoding="async" onError={() => setFailed(true)} />;
}

export function AbilityEffectPanel({
   hero,
   heroSel,
   groups,
   activeTag,
   direction,
   locked,
   onSelectTag,
   renderTiles,
}: {
   hero: Hero;
   heroSel: HeroSelectionState;
   groups: EffectGroup[];
   activeTag: string;
   direction: 1 | -1 | 0;
   locked: boolean;
   onSelectTag: (tag: string) => void;
   renderTiles: (entries: EffectGroup["entries"]) => ReactNode;
}) {
   const { t } = useLocale();
   const picked = new Set(heroSel.effects ?? []);
   const isOn = (idx: number) => picked.has(idx) || carrierOf(hero, heroSel, idx) != null;
   const active = groups.find((g) => g.key === activeTag) ?? groups[0];
   if (!active) return null;

   const pickedIdx = active.entries.find((e) => picked.has(e.idx))?.idx ?? null;
   const carriedIdx = active.entries.find((e) => carrierOf(hero, heroSel, e.idx) != null)?.idx ?? null;
   const isDefault = pickedIdx == null && carriedIdx == null;
   const baseIcon = abilityIconOf(hero, active.key);
   const defaultLabel = t("heroGallery.effectDefault", "Default");

   return (
      <div>
         <div className={[css.abilityRail, locked ? css.categoryLocked : ""].filter(Boolean).join(" ")} role="tablist" aria-label={t("heroGallery.abilityRail", "Abilities")}>
            {groups.map((g) => {
               const hasPick = g.entries.some((e) => isOn(e.idx));
               const isActive = g.key === active.key;
               return (
                  <button
                     key={g.key}
                     type="button"
                     role="tab"
                     data-no-drag
                     aria-selected={isActive}
                     title={`${g.label} · ${g.entries.length}`}
                     className={[css.abilityTab, isActive ? css.active : "", hasPick ? css.hasPick : ""].filter(Boolean).join(" ")}
                     onClick={() => onSelectTag(g.key)}
                  >
                     <AbilityIcon url={abilityIconOf(hero, g.key)} label={g.label} className={css.abilityTabIcon} />
                     <span className={css.abilityTabName}>{g.label}</span>
                     <span className={css.abilityTabCount}>{g.entries.length}</span>
                  </button>
               );
            })}
         </div>

         <div className={css.setSubHeader}>
            <span className={css.subLabel}>{active.label}</span>
            <span className={css.subRule}>{t("heroGallery.pickOne", "pick 1")}</span>
            <div className={css.categoryLine} />
            <span className={css.categoryCount}>{active.entries.length}</span>
         </div>

         <div
            key={active.key}
            role="tabpanel"
            className={[css.setCategoryGrid, locked ? css.categoryLocked : "", direction === 1 ? css.animateSlideLeft : direction === -1 ? css.animateSlideRight : ""]
               .filter(Boolean)
               .join(" ")}
         >
            <div
               className={[css.setTile, css.itemTile, isDefault ? css.selected : "", carriedIdx != null ? css.blocked : ""].filter(Boolean).join(" ")}
               title={
                  carriedIdx != null
                     ? `${active.label}: set by a picked item`
                     : t("heroGallery.effectDefaultHint", "No effect: the ability keeps its own look")
               }
               onClick={() => {
                  if (!locked && pickedIdx != null) toggleEffect(hero, pickedIdx);
               }}
            >
               <div className={`${css.setTileImg} ${css.effectDefaultImg}`}>
                  <AbilityIcon url={baseIcon} label={active.label} className={css.effectDefaultIcon} />
               </div>
               <div className={css.setTileBody}>
                  <span className={css.setTileName}>{defaultLabel}</span>
                  {isDefault && <span className={css.setStatus}>✓ {t("heroGallery.selectedTile", "Selected")}</span>}
               </div>
            </div>
            {renderTiles(active.entries)}
         </div>
      </div>
   );
}
