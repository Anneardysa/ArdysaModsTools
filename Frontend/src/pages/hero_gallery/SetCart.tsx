import { T, useLocale } from "../../bridge/i18n";
import { MAX_PARTICLES, abilityLabel, canCarryParticles, extractItemTag, findParticle, getSetCategory, getSetDisplayName, getSetNoticeLines, isPersonaActive } from "./helpers";
import { NoticeBadge } from "./SetTile";
import { openParticlePicker, store } from "./store";
import type { Hero, HeroSelectionState } from "./types";
import css from "./gallery.module.css";

type Row = { key: string; name: string; kind: string; thumb: string | null; notices: string[]; onRemove: () => void };

export function SetCart({
   hero,
   heroSel,
   onRemoveSet,
   onRemoveItem,
   onRemoveBase,
   onRemovePrismatic,
   onRemoveEffect,
   onRemoveParticle,
}: {
   hero: Hero;
   heroSel: HeroSelectionState;
   onRemoveSet: (idx: number) => void;
   onRemoveItem: (idx: number) => void;
   onRemoveBase: (idx: number) => void;
   onRemovePrismatic: (idx: number) => void;
   onRemoveEffect: (idx: number) => void;
   onRemoveParticle: (slot: number) => void;
}) {
   const { t } = useLocale();
   const catalog = store.use((s) => s.particles);
   const rows: Row[] = [];

   const push = (idx: number, kind: string, onRemove: (idx: number) => void) => {
      const set = hero.sets[idx];
      if (!set) return;
      rows.push({
         key: `s${idx}`,
         name: getSetDisplayName(set, idx),
         kind,
         thumb: set.thumbnailUrl || hero.thumbnail || "",
         notices: getSetNoticeLines(hero, [idx]),
         onRemove: () => onRemove(idx),
      });
   };

   if (heroSel.set !== null) {
      push(heroSel.set, getSetCategory(hero.sets[heroSel.set]) === "persona" ? "Persona" : "Set", onRemoveSet);
   }
   heroSel.items.forEach((idx) => push(idx, extractItemTag(hero.sets[idx]) || "Item", onRemoveItem));
   if (heroSel.base !== null) push(heroSel.base, "Base Hero", onRemoveBase);
   if (heroSel.prismatic !== null) push(heroSel.prismatic, "Prismatic", onRemovePrismatic);
   (heroSel.effects ?? []).forEach((idx) => {
      const tag = extractItemTag(hero.sets[idx]);
      push(idx, tag ? `Ability Effect · ${abilityLabel(hero, tag)}` : "Ability Effect", onRemoveEffect);
   });
   (heroSel.particles ?? []).forEach((pick, slot) => {
      const host = hero.particleHosts?.find((h) => h.id === pick.host)?.label ?? pick.host;
      rows.push({
         key: `p${slot}`,
         name: findParticle(catalog, pick.id)?.name ?? pick.id,
         kind: `${t("heroGallery.particles.kind", "Particle")} · ${host}`,
         thumb: null,
         notices: [t("heroGallery.particles.fpsNotice", "Unusual particles can lower your FPS.")],
         onRemove: () => onRemoveParticle(slot),
      });
   });

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
               <div className={css.smCartEmpty}>{t("heroGallery.cartEmpty", "Nothing selected yet. Pick a set, item or base from the catalog.")}</div>
            ) : (
               rows.map((r) => (
                  <div key={r.key} className={css.cartRow}>
                     <div className={css.cartRowThumb}>
                        {r.thumb == null ? (
                           <span className={css.fxMark} aria-hidden="true" />
                        ) : (
                           <img
                              src={r.thumb}
                              alt=""
                              loading="lazy"
                              decoding="async"
                              onError={(e) => {
                                 e.currentTarget.style.visibility = "hidden";
                              }}
                           />
                        )}
                     </div>
                     <div className={css.cartRowText}>
                        <span className={css.cartRowName} title={r.name}>
                           {r.name}
                        </span>
                        <span className={css.cartRowKind}>{r.kind}</span>
                     </div>
                     <NoticeBadge lines={r.notices} className={css.noticeBadgeInline} />
                     <button
                        type="button"
                        data-no-drag
                        className={css.cartRowX}
                        onClick={r.onRemove}
                        title={t("heroGallery.cartRemove", "Remove from selection")}
                        aria-label={`${t("heroGallery.cartRemove", "Remove from selection")}: ${r.name}`}
                     >
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                           <path d="M6 18L18 6M6 6l12 12" />
                        </svg>
                     </button>
                  </div>
               ))
            )}
         </div>
         {(hero.particleHosts?.length ?? 0) > 0 && <CartParticleAction hero={hero} heroSel={heroSel} />}
      </aside>
   );
}

function CartParticleAction({ hero, heroSel }: { hero: Hero; heroSel: HeroSelectionState }) {
   const { t } = useLocale();
   const count = heroSel.particles?.length ?? 0;
   const reason = isPersonaActive(hero, heroSel)
      ? t("heroGallery.particles.persona", "⚠ Particles are disabled while a persona is active")
      : !canCarryParticles(hero, heroSel)
         ? t("heroGallery.particles.needLayer", "⚠ Pick a set, item or arcana first. Particles ride on a worn item.")
         : null;
   const full = count >= MAX_PARTICLES;

   return (
      <div className={css.smCartFoot}>
         <button
            type="button"
            data-no-drag
            className={`${css.btn} ${css.ptAdd} ${css.ptCartAdd}`}
            disabled={reason != null || full}
            onClick={() => openParticlePicker(hero, count)}
         >
            <span>{full ? t("heroGallery.particles.title", "Particles") : t("heroGallery.particles.add", "Add particle")}</span>
            <span>{count}/{MAX_PARTICLES}</span>
         </button>
         {reason && <p className={css.ptCartHint}>{reason}</p>}
      </div>
   );
}
