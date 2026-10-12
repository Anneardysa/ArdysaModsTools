import { useEffect, useMemo, useState } from "react";
import { T, useLocale } from "../../bridge/i18n";
import { abilityLabel, abilityRank, carrierOf, getBlockedIndices, getHeroSelection, getSetCategory, getSetDisplayName, isBaseActive, isLockedByBase, isPersonaActive, normalizeSlot, slotLabel, slotRank } from "./helpers";
import {
   closeModal,
   deselectHero,
   navigateHero,
   openStylePreview,
   removeParticle,
   selectSet,
   setVisualOrder,
   store,
   toggleBase,
   toggleEffect,
   toggleItem,
   togglePrismatic,
} from "./store";
import { AbilityEffectPanel, abilityIconOf, resolveActiveTag } from "./AbilityEffectPanel";
import { ParticlePanel } from "./ParticlePanel";
import { SetCart } from "./SetCart";
import { SetTile } from "./SetTile";
import type { Hero, HeroSelectionState, SetEntry, TileType, VisualTile } from "./types";
import css from "./gallery.module.css";

type Entry = { set: SetEntry; idx: number };
type SubGroup = { key: string; label: string; entries: Entry[] };

type Category = {
   key: string;
   label: string;
   tileType: TileType;
   entries: Entry[];
   subGroups: SubGroup[];
   notice: string | null;
   locked?: boolean;
};

function buildItemSubGroups(entries: Entry[]): SubGroup[] {
   const bySlot = new Map<string, Entry[]>();
   for (const entry of entries) {
      const slot = normalizeSlot(entry.set.tag);
      const bucket = bySlot.get(slot);
      if (bucket) bucket.push(entry);
      else bySlot.set(slot, [entry]);
   }

   return [...bySlot.entries()]
      .map(([key, group]) => ({ key, label: slotLabel(key), entries: group }))
      .sort((a, b) => slotRank(a.key) - slotRank(b.key) || a.key.localeCompare(b.key));
}

function buildEffectSubGroups(hero: Hero, entries: Entry[]): SubGroup[] {
   const byAbility = new Map<string, Entry[]>();
   for (const entry of entries) {
      const tag = entry.set.tag;
      if (!tag) continue;
      const bucket = byAbility.get(tag);
      if (bucket) bucket.push(entry);
      else byAbility.set(tag, [entry]);
   }

   return [...byAbility.entries()]
      .map(([key, group]) => ({ key, label: abilityLabel(hero, key), entries: group }))
      .sort((a, b) => abilityRank(a.key) - abilityRank(b.key));
}

function buildCategories(hero: Hero, heroSel: HeroSelectionState): Category[] {
   const legacy: Entry[] = [];
   const custom: Entry[] = [];
   const persona: Entry[] = [];
   const item: Entry[] = [];
   const base: Entry[] = [];
   const prismatic: Entry[] = [];
   const effect: Entry[] = [];

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
         case "abilityeffect":
            if (set.tag) effect.push({ set, idx });
            break;
         default:
            legacy.push({ set, idx });
      }
   });

   const personaActive = isPersonaActive(hero, heroSel);
   const baseActive = isBaseActive(heroSel);
   const hasItemsSelected = heroSel.items.length > 0;

   const categories: Category[] = [
      { key: "legacy", label: "Legacy Set", tileType: "set", entries: legacy, subGroups: [], notice: null },
      { key: "custom", label: "Custom Set", tileType: "set", entries: custom, subGroups: [], notice: null },
      {
         key: "persona",
         label: "Persona",
         tileType: "persona",
         entries: persona,
         subGroups: [],
         notice: hasItemsSelected || (heroSel.effects?.length ?? 0) > 0 || (heroSel.particles?.length ?? 0) > 0
            ? "⚠ Selecting a persona will clear items, base, ability effects and particles"
            : null,
      },
      {
         key: "item",
         label: "Items",
         tileType: "item",
         entries: item,
         subGroups: buildItemSubGroups(item),
         notice: personaActive ? "⚠ Items are disabled while a persona is active" : null,
      },
      {
         key: "base",
         label: "Base Hero",
         tileType: "base",
         entries: base,
         subGroups: [],
         notice: personaActive ? "⚠ Base is disabled while a persona is active" : null,
      },
      {
         key: "prismatic",
         label: "Prismatic",
         tileType: "prismatic",
         entries: prismatic,
         subGroups: [],
         notice: baseActive ? null : "⚠ Select a Base Hero first to enable Prismatic",
         locked: !baseActive,
      },
      {
         key: "effect",
         label: "Ability Effects",
         tileType: "effect",
         entries: effect,
         subGroups: buildEffectSubGroups(hero, effect),
         notice: personaActive ? "⚠ Ability effects are disabled while a persona is active" : null,
         locked: personaActive,
      },
   ];
   return categories.filter((c) => c.entries.length > 0);
}

function buildStyleGroups(hero: Hero): Map<string, number[]> {
   const groups = new Map<string, number[]>();
   hero.sets.forEach((set, idx) => {
      if (!set.styleGroup) return;
      const existing = groups.get(set.styleGroup);
      if (existing) existing.push(idx);
      else groups.set(set.styleGroup, [idx]);
   });
   return groups;
}

function buildVisualOrder(categories: Category[], effectTag: string | null): VisualTile[] {
   const order: VisualTile[] = [];
   const seen = new Set<string>();
   for (const cat of categories) {
      const subGroups = cat.key === "effect" ? cat.subGroups.filter((g) => g.key === effectTag) : cat.subGroups;
      const groups = cat.subGroups.length > 0 ? subGroups.map((g) => g.entries) : [cat.entries];
      for (const entries of groups) {
         for (const { set, idx } of entries) {
            if (set.styleGroup) {
               if (seen.has(set.styleGroup)) continue;
               seen.add(set.styleGroup);
            }
            order.push({ idx, tileType: cat.tileType });
         }
      }
   }
   return order;
}

export function SetModal() {
   const { t } = useLocale();
   const heroes = store.use((s) => s.heroes);
   const selections = store.use((s) => s.selections);
   const modalHeroId = store.use((s) => s.modalHeroId);
   const focusedSetIndex = store.use((s) => s.focusedSetIndex);
   const highlightSetIndex = store.use((s) => s.highlightSetIndex);
   const [slideDir, setSlideDir] = useState<1 | -1 | 0>(0);
   const [hoveredBlocked, setHoveredBlocked] = useState<number | null>(null);
   const [effectTab, setEffectTab] = useState<{ heroId: string; tag: string; dir: 1 | -1 | 0 } | null>(null);

   const hero = heroes.find((h) => h.id === modalHeroId) ?? null;
   const heroSel: HeroSelectionState = getHeroSelection(selections, hero?.id ?? "");

   const view = useMemo(() => {
      if (!hero) return null;
      const categories = buildCategories(hero, heroSel);
      const effectGroups = categories.find((c) => c.key === "effect")?.subGroups ?? [];
      const effectTag = resolveActiveTag(effectGroups, heroSel, effectTab?.heroId === hero.id ? effectTab.tag : null);
      return {
         categories,
         effectGroups,
         effectTag,
         styleGroups: buildStyleGroups(hero),
         blocked: getBlockedIndices(hero, heroSel),
         order: buildVisualOrder(categories, effectTag),
      };
   }, [hero, heroSel, effectTab]);

   const effectTag = view?.effectTag ?? null;
   const effectGroups = view?.effectGroups;
   const selectEffectTab = (tag: string) => {
      if (!hero || !effectGroups || tag === effectTag) return;
      const rank = (k: string | null) => effectGroups.findIndex((g) => g.key === k);
      setEffectTab({ heroId: hero.id, tag, dir: rank(tag) > rank(effectTag) ? 1 : -1 });
   };
   useEffect(() => {
      for (const idx of [highlightSetIndex, focusedSetIndex]) {
         const set = idx != null && idx >= 0 ? hero?.sets[idx] : undefined;
         if (set && getSetCategory(set) === "abilityeffect" && set.tag) {
            selectEffectTab(set.tag);
            return;
         }
      }
      // eslint-disable-next-line react-hooks/exhaustive-deps
   }, [highlightSetIndex, focusedSetIndex, hero?.id]);

   useEffect(() => {
      if (view) setVisualOrder(view.order);
   }, [view]);

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

   if (!hero || !view) return null;

   const { categories, styleGroups, blocked } = view;
   const showHeaders = categories.length > 1;

   const blockersOf = (idx: number): number[] => {
      const out = (hero.sets[idx]?.conflicts ?? []).filter(
         (i) => heroSel.set === i || heroSel.base === i || heroSel.prismatic === i || heroSel.items.includes(i)
            || (heroSel.effects?.includes(i) ?? false),
      );
      if (heroSel.base != null && isLockedByBase(hero, heroSel, idx) && !out.includes(heroSel.base)) out.unshift(heroSel.base);
      const carrier = carrierOf(hero, heroSel, idx);
      if (carrier != null && !out.includes(carrier)) out.unshift(carrier);
      return out;
   };
   const litBlockers = hoveredBlocked != null ? new Set(blockersOf(hoveredBlocked)) : null;

   const goHero = (dir: 1 | -1) => {
      setSlideDir(dir);
      navigateHero(dir);
   };

   const renderTile = (cat: Category, { set, idx }: Entry, renderedGroups: Set<string>) => {
      if (set.styleGroup) {
         if (renderedGroups.has(set.styleGroup)) return null;
         renderedGroups.add(set.styleGroup);
      }
      const groupIndices = set.styleGroup ? styleGroups.get(set.styleGroup) ?? [] : [];
      const isBlocked = groupIndices.length > 1 ? groupIndices.every((i) => blocked.has(i)) : blocked.has(idx);
      const blockerName = isBlocked
         ? blockersOf(idx).map((i) => getSetDisplayName(hero.sets[i], i))[0] ?? null
         : null;
      return (
         <SetTile
            key={idx}
            hero={hero}
            set={set}
            idx={idx}
            tileType={cat.tileType}
            heroSel={heroSel}
            groupIndices={groupIndices}
            isFocused={focusedSetIndex === idx}
            isHighlighted={highlightSetIndex === idx}
            isBlocked={isBlocked}
            isBlocking={litBlockers?.has(idx) ?? false}
            blockerName={blockerName}
            showCategoryHeaders={showHeaders}
            onHoverBlocked={setHoveredBlocked}
            onSelect={() => {
               if (cat.tileType === "item") toggleItem(hero, idx);
               else if (cat.tileType === "base") toggleBase(hero, idx);
               else if (cat.tileType === "prismatic") togglePrismatic(hero, idx);
               else if (cat.tileType === "effect") toggleEffect(hero, idx);
               else selectSet(hero, idx);
            }}
            onOpenStylePreview={(group) => openStylePreview(hero, group, cat.tileType)}
            includedIn={(() => {
               const carrier = carrierOf(hero, heroSel, idx);
               return carrier != null ? getSetDisplayName(hero.sets[carrier], carrier) : null;
            })()}
            badgeIconUrl={cat.tileType === "effect" && set.tag ? set.abilityIconUrl ?? abilityIconOf(hero, set.tag) : null}
         />
      );
   };

   const grid = (cat: Category, entries: Entry[], renderedGroups: Set<string>) => (
      <div className={`${css.setCategoryGrid} ${cat.locked ? css.categoryLocked : ""}`}>
         {entries.map((entry) => renderTile(cat, entry, renderedGroups))}
      </div>
   );

   return (
      <div
         id="setModal"
         className={css.modalBackdrop}
         onClick={(e) => {
            if (e.target === e.currentTarget) closeModal();
         }}
      >
         <div className={css.smWrap}>
            <div className={`${css.smPanel} ${css.animateScaleIn}`}>
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
                  <div className={css.smHeadActions}>
                     <div className={css.smNavPair}>
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
                     <button type="button" data-no-drag className={css.smX} onClick={closeModal} title={t("common.close", "Close")} aria-label={t("common.close", "Close")}>
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                           <path d="M6 18L18 6M6 6l12 12" />
                        </svg>
                     </button>
                  </div>
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
                              {cat.key === "effect" && effectTag ? (
                                 <AbilityEffectPanel
                                    hero={hero}
                                    heroSel={heroSel}
                                    groups={cat.subGroups}
                                    activeTag={effectTag}
                                    direction={effectTab?.heroId === hero.id ? effectTab.dir : 0}
                                    locked={!!cat.locked}
                                    onSelectTag={selectEffectTab}
                                    renderTiles={(entries) => entries.map((entry) => renderTile(cat, entry, renderedGroups))}
                                 />
                              ) : cat.subGroups.length > 0
                                 ? cat.subGroups.map((group) => (
                                      <div key={group.key} className={css.setSubSection}>
                                         <div className={css.setSubHeader}>
                                            <span className={css.subLabel}>{group.label}</span>
                                            <span className={css.subRule}>{t("heroGallery.pickOne", "pick 1")}</span>
                                            <div className={css.categoryLine} />
                                            <span className={css.categoryCount}>{group.entries.length}</span>
                                         </div>
                                         {grid(cat, group.entries, renderedGroups)}
                                      </div>
                                   ))
                                 : grid(cat, cat.entries, renderedGroups)}
                           </div>
                        );
                     })}
                     {(hero.particleHosts?.length ?? 0) > 0 && <ParticlePanel hero={hero} heroSel={heroSel} />}
                  </div>

                  <SetCart
                     hero={hero}
                     heroSel={heroSel}
                     onRemoveSet={(idx) => selectSet(hero, idx)}
                     onRemoveItem={(idx) => toggleItem(hero, idx)}
                     onRemoveBase={(idx) => toggleBase(hero, idx)}
                     onRemovePrismatic={(idx) => togglePrismatic(hero, idx)}
                     onRemoveEffect={(idx) => toggleEffect(hero, idx)}
                     onRemoveParticle={(slot) => removeParticle(hero, slot)}
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
         </div>
      </div>
   );
}
