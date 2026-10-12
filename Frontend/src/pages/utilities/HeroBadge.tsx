import { useEffect, useState } from "react";
import { send } from "../../bridge/host";
import { T, useLocale } from "../../bridge/i18n";
import { Button } from "../../ui/Button";
import { BADGE_TIERS, BAR_COLORS, badgeSrc, LEVEL_MAX, LEVEL_MIN, tierForLevel, tierOf, tierTo, xpNeed } from "./badges";
import { Field, Segment, Slider } from "./RankChanger";
import { store, type BadgeRequest } from "./store";
import css from "./utilities.module.css";

type Translate = (key: string, fallback?: string, vars?: Record<string, string | number>) => string;

const DEFAULT_DRAFT: BadgeRequest = {
   show: "tier",
   tier: 5,
   level: "custom",
   customLevel: LEVEL_MAX,
   progress: "max",
   customProgress: 50,
   onlyTier: null,
};

function normalise(r: BadgeRequest): string {
   return JSON.stringify([
      r.show,
      r.show === "tier" ? r.tier : null,
      r.show === "hide" ? "hide" : r.level,
      r.show !== "hide" && r.level === "custom" ? r.customLevel : null,
      r.progress,
      r.progress === "custom" ? r.customProgress : null,
      r.onlyTier,
   ]);
}

const changesNothing = (r: BadgeRequest) => {
   const artChanges = r.show === "hide" || (r.show === "tier" && r.tier !== r.onlyTier);
   return !artChanges && (r.show === "hide" || r.level === "keep") && r.progress === "keep";
};

const shownTier = (r: BadgeRequest): number | null =>
   r.show === "tier" ? r.tier : r.show === "keep" ? r.onlyTier : null;

const barTier = (r: BadgeRequest) =>
   r.show === "tier"
      ? r.tier
      : r.onlyTier !== null
        ? r.onlyTier
        : r.show !== "hide" && r.level === "custom"
          ? tierForLevel(r.customLevel)
          : -1;

const barPercent = (r: BadgeRequest) => (r.progress === "max" ? 100 : r.progress === "custom" ? r.customProgress : null);

export function HeroBadge() {
   const { t } = useLocale();
   const hydrated = store.use((s) => s.hydrated);
   const applied = store.use((s) => s.badgeApplied);
   const saved = store.use((s) => s.badgeSaved);
   const busy = store.use((s) => s.busy);
   const isModInstalled = store.use((s) => s.isModInstalled);

   const [draft, setDraft] = useState<BadgeRequest>(DEFAULT_DRAFT);

   useEffect(() => {
      setDraft(saved ? { ...DEFAULT_DRAFT, ...saved } : DEFAULT_DRAFT);
   }, [saved]);

   const set = (patch: Partial<BadgeRequest>) => setDraft((d) => ({ ...d, ...patch }));

   const pickTier = (tier: number) =>
      set({
         show: "tier",
         tier,
         customLevel: tierForLevel(draft.customLevel) === tier ? draft.customLevel : BADGE_TIERS[tier]!.from,
      });

   const pickLevel = (customLevel: number) =>
      set(draft.show === "tier" ? { customLevel, tier: tierForLevel(customLevel) } : { customLevel });

   const keepBadge = () =>
      set({
         show: "keep",
         customLevel:
            draft.onlyTier !== null && tierForLevel(draft.customLevel) !== draft.onlyTier
               ? BADGE_TIERS[draft.onlyTier]!.from
               : draft.customLevel,
      });

   const pickScope = (onlyTier: number | null) =>
      set({
         onlyTier,
         customLevel:
            draft.show === "keep" && onlyTier !== null && tierForLevel(draft.customLevel) !== onlyTier
               ? BADGE_TIERS[onlyTier]!.from
               : draft.customLevel,
      });

   if (!hydrated) {
      return (
         <section className={css.panel}>
            <p className={css.loading}>
               <T k="utilities.loading">Reading what is installed…</T>
            </p>
         </section>
      );
   }

   const hidden = draft.show === "hide";
   const scoped = draft.onlyTier !== null;
   const scopeName = scoped ? t(tierOf(draft.onlyTier!).key, tierOf(draft.onlyTier!).name) : "";
   const fixedTier = draft.show === "keep" ? shownTier(draft) : null;
   const levelMin = fixedTier === null ? LEVEL_MIN : BADGE_TIERS[fixedTier]!.from;
   const levelMax = fixedTier === null ? LEVEL_MAX : tierTo(fixedTier);
   const xpHidden = draft.progress !== "keep" && (hidden || draft.level !== "custom");
   const unchanged = applied && saved !== null && normalise(draft) === normalise(saved);
   const nothing = changesNothing(draft);
   const canApply = !busy && isModInstalled && !nothing && !unchanged;

   return (
      <section className={css.panel} aria-busy={busy}>
         <div className={css.scroll}>
            {!isModInstalled && (
               <div className={css.modsRequiredBanner} role="alert">
                  <div className={css.bannerContent}>
                     <strong className={css.bannerTitle}>
                        <T k="rank.banner.modsRequiredTitle">Mods Not Installed</T>
                     </strong>
                     <p className={css.bannerText}>
                        <T k="badge.banner.modsRequired">
                           Hero Badge Changer needs an installed mod package. Install Modspack, Skin Selector or Miscellaneous first.
                        </T>
                     </p>
                  </div>
               </div>
            )}

            <header className={css.head}>
               <h1 className={css.h1}>
                  <T k="utilities.tool.heroBadge">Hero Badge</T>
               </h1>
               <p className={css.lede}>
                  <T k="badge.lede">
                     Redraws the hero level badge your own client shows. Other players still see your real badges and levels.
                  </T>
               </p>
               <p className={applied ? css.stateOn : css.stateOff}>
                  {applied && saved ? (
                     <T k="badge.state.applied" values={{ summary: describe(saved, t) }}>{`Applied: ${describe(saved, t)}`}</T>
                  ) : (
                     <T k="badge.state.clean">No override. The game draws its own badges.</T>
                  )}
               </p>
            </header>

            <div className={css.cols}>
               <div className={css.colMain}>
                  <Field labelKey="badge.field.scope" label="Which heroes">
                     <div className={css.segment} role="radiogroup" aria-label={t("badge.field.scope", "Which heroes")}>
                        <Segment
                           name="badgeScope"
                           checked={!scoped}
                           onChange={() => pickScope(null)}
                           labelKey="badge.scope.all"
                           label="Every hero"
                           hintKey="badge.scope.all.hint"
                           hint="Every badge you see changes, heroes with no badge yet included."
                           disabled={busy}
                        />
                        <Segment
                           name="badgeScope"
                           checked={scoped}
                           onChange={() => pickScope(draft.onlyTier ?? 0)}
                           labelKey="badge.scope.tier"
                           label="One tier only"
                           hintKey="badge.scope.tier.hint"
                           hint="Only heroes whose real badge is the tier you pick below."
                           disabled={busy}
                        />
                     </div>
                     {scoped && (
                        <>
                           <div className={css.scopeRow} role="radiogroup" aria-label={t("badge.scope.pick", "Their badge now")}>
                              <span className={css.inlineLabel}>
                                 <T k="badge.scope.pick">Their badge now</T>
                              </span>
                              {BADGE_TIERS.map((b) => (
                                 <label key={b.tier} className={`${css.scopeChip} ${draft.onlyTier === b.tier ? css.scopeChipOn : ""}`}>
                                    <input
                                       type="radio"
                                       name="badgeScopeTier"
                                       className={css.srOnly}
                                       checked={draft.onlyTier === b.tier}
                                       disabled={busy}
                                       onChange={() => pickScope(b.tier)}
                                    />
                                    <img src={badgeSrc(b.tier)} alt="" draggable={false} />
                                    <span>{t(b.key, b.name)}</span>
                                 </label>
                              ))}
                           </div>
                           <p className={css.warn}>
                              <T k="badge.scope.note" values={{ tier: scopeName }}>
                                 {`Dota marks badges by tier, not by hero: every ${scopeName} hero changes, other players' too. A hero that levels out of ${scopeName} loses the change.`}
                              </T>
                           </p>
                        </>
                     )}
                  </Field>

                  <Field labelKey="badge.field.show" label="Badge">
                     <div className={css.badgeWall} role="radiogroup" aria-label={t("badge.field.show", "Badge")}>
                        {BADGE_TIERS.map((b) => {
                           const on = draft.show === "tier" && draft.tier === b.tier;
                           return (
                              <label key={b.tier} className={`${css.medal} ${on ? css.medalOn : ""}`}>
                                 <input
                                    type="radio"
                                    name="badgeShow"
                                    className={css.srOnly}
                                    checked={on}
                                    disabled={busy}
                                    onChange={() => pickTier(b.tier)}
                                 />
                                 <img className={css.badgeArt} src={badgeSrc(b.tier)} alt="" draggable={false} />
                                 <span className={css.medalName}>{t(b.key, b.name)}</span>
                                 <span className={css.badgeRange}>{levelRange(b.tier, t)}</span>
                              </label>
                           );
                        })}

                        <span className={css.wallRule} aria-hidden="true" />

                        <BadgeOption
                           checked={draft.show === "keep"}
                           disabled={busy}
                           onChange={keepBadge}
                           glyph="keep"
                           labelKey="badge.show.keep"
                           label="As is"
                        />
                        <BadgeOption
                           checked={hidden}
                           disabled={busy}
                           onChange={() => set({ show: "hide" })}
                           glyph="hide"
                           labelKey="badge.show.hide"
                           label="Hidden"
                        />
                     </div>
                     <p className={css.hint}>
                        {draft.show === "tier" && scoped ? (
                           <T k="badge.show.tier.scopedHint" values={{ tier: scopeName }}>{`Only ${scopeName} heroes wear this badge.`}</T>
                        ) : draft.show === "tier" ? (
                           <T k="badge.show.tier.hint">Every hero wears this badge, including heroes you have not levelled yet.</T>
                        ) : hidden ? (
                           <T k="badge.show.hide.hint">No badge anywhere: hero grid, hero page, top bar and portrait.</T>
                        ) : (
                           <T k="badge.show.keep.hint">Each hero keeps the badge it really has.</T>
                        )}
                     </p>
                  </Field>

                  <Field labelKey="badge.field.level" label="Level on the badge">
                     {hidden ? (
                        <p className={css.hint}>
                           <T k="badge.level.hiddenBadge">A hidden badge carries no number.</T>
                        </p>
                     ) : (
                        <>
                           <div className={css.segment} role="radiogroup" aria-label={t("badge.field.level", "Level on the badge")}>
                              <Segment
                                 name="badgeLevel"
                                 checked={draft.level === "keep"}
                                 onChange={() => set({ level: "keep" })}
                                 labelKey="badge.level.keep"
                                 label="Real level"
                                 hintKey="badge.level.keep.hint"
                                 hint="Each hero shows the level it really has."
                                 disabled={busy}
                              />
                              <Segment
                                 name="badgeLevel"
                                 checked={draft.level === "custom"}
                                 onChange={() => set({ level: "custom" })}
                                 labelKey="badge.level.custom"
                                 label="One level"
                                 hintKey="badge.level.custom.hint"
                                 hint="Every badge shows the number you pick."
                                 disabled={busy}
                              />
                              <Segment
                                 name="badgeLevel"
                                 checked={draft.level === "hide"}
                                 onChange={() => set({ level: "hide" })}
                                 labelKey="badge.level.hide"
                                 label="No number"
                                 hintKey="badge.level.hide.hint"
                                 hint="Badges are drawn without a level."
                                 disabled={busy}
                              />
                           </div>
                           {draft.level === "custom" && (
                              <Slider
                                 label={t("badge.level.value", "Level")}
                                 value={draft.customLevel}
                                 min={levelMin}
                                 max={levelMax}
                                 step={1}
                                 disabled={busy}
                                 readout={`${draft.customLevel} · ${t(tierOf(tierForLevel(draft.customLevel)).key, tierOf(tierForLevel(draft.customLevel)).name)}`}
                                 onChange={pickLevel}
                              />
                           )}
                        </>
                     )}
                  </Field>

                  <Field labelKey="badge.field.progress" label="Level bar on the hero page">
                     <div className={css.segment} role="radiogroup" aria-label={t("badge.field.progress", "Level bar on the hero page")}>
                        <Segment
                           name="badgeProgress"
                           checked={draft.progress === "keep"}
                           onChange={() => set({ progress: "keep" })}
                           labelKey="badge.progress.keep"
                           label="Real progress"
                           hintKey="badge.progress.keep.hint"
                           hint="The bar and XP numbers stay as the game reports them."
                           disabled={busy}
                        />
                        <Segment
                           name="badgeProgress"
                           checked={draft.progress === "max"}
                           onChange={() => set({ progress: "max" })}
                           labelKey="badge.progress.max"
                           label="Full"
                           hintKey="badge.progress.max.hint"
                           hint="The bar is filled; XP numbers are hidden."
                           disabled={busy}
                        />
                        <Segment
                           name="badgeProgress"
                           checked={draft.progress === "custom"}
                           onChange={() => set({ progress: "custom" })}
                           labelKey="badge.progress.custom"
                           label="Custom"
                           hintKey="badge.progress.custom.hint"
                           hint="Fill the bar to a percentage you pick."
                           disabled={busy}
                        />
                     </div>
                     {xpHidden && (
                        <p className={css.hint}>
                           <T k="badge.progress.xpHidden">Pick one level to show the XP numbers under the bar; without one they are hidden.</T>
                        </p>
                     )}
                     {draft.progress === "custom" && (
                        <Slider
                           label={t("badge.progress.value", "Filled")}
                           value={draft.customProgress}
                           min={0}
                           max={100}
                           step={1}
                           disabled={busy}
                           readout={`${draft.customProgress}%`}
                           onChange={(customProgress) => set({ customProgress })}
                        />
                     )}
                  </Field>
               </div>

               <aside className={css.colSide}>
                  <Field labelKey="badge.field.preview" label="Preview">
                     <BadgePreview draft={draft} />
                     <p className={css.hint}>
                        <T k="badge.preview.hint">A close likeness. Dota draws the final one with its own font and effects.</T>
                     </p>
                  </Field>

                  <Field labelKey="badge.field.reach" label="Where it shows">
                     <ul className={css.reach}>
                        <li><T k="badge.reach.grid">Hero grid and hero picker</T></li>
                        <li><T k="badge.reach.page">Hero page badge and level bar</T></li>
                        <li><T k="badge.reach.hud">Top bar and unit portrait in a match</T></li>
                        <li><T k="badge.reach.postgame">Post-game screen, tooltips, showcase</T></li>
                     </ul>
                     {!scoped && (
                        <p className={css.warn}>
                           <T k="badge.reach.shared">
                              The badge art is shared by every hero and every player: the top bar shows your pick on all ten players, on your screen only.
                           </T>
                        </p>
                     )}
                  </Field>
               </aside>
            </div>
         </div>

         <footer className={css.actions}>
            <p className={css.summary}>
               {describe(draft, t)}
               {(nothing || unchanged) && (
                  <span className={css.warn}>
                     {" "}
                     {nothing ? (
                        <T k="badge.summary.nothing">Pick at least one thing to change.</T>
                     ) : (
                        <T k="rank.summary.noChange">Nothing to change.</T>
                     )}
                  </span>
               )}
               <span className={css.restart}>
                  <T k="badge.summary.restart">
                     Rebuilds the mod package: close Dota 2 first, then restart it to see the change.
                  </T>
               </span>
            </p>

            <Button
               variant="ghost"
               disabled={!applied || busy || !isModInstalled}
               onClick={() => {
                  store.set({ runKind: "reset", runTool: "heroBadge" });
                  send("badgeReset");
               }}
               title={t("badge.action.reset.tip", "Put the game's own badges back.")}
            >
               <T k="rank.action.reset">Reset</T>
            </Button>
            <Button
               variant="primary"
               disabled={!canApply}
               onClick={() => {
                  store.set({ runKind: "apply", runTool: "heroBadge" });
                  send("badgeApply", { request: draft });
               }}
            >
               {busy ? <T k="rank.action.working">Working…</T> : <T k="rank.action.apply">Apply</T>}
            </Button>
         </footer>
      </section>
   );
}

function BadgeOption({
   checked,
   disabled,
   onChange,
   glyph,
   labelKey,
   label,
}: {
   checked: boolean;
   disabled: boolean;
   onChange: () => void;
   glyph: "keep" | "hide";
   labelKey: string;
   label: string;
}) {
   return (
      <label className={`${css.medal} ${css.badgeOption} ${checked ? css.medalOn : ""}`}>
         <input type="radio" name="badgeShow" className={css.srOnly} checked={checked} disabled={disabled} onChange={onChange} />
         <svg className={css.badgeGlyph} viewBox="0 0 48 48" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
            <path d="M10 8h28l-3 22-11 12-11-12z" strokeLinejoin="miter" />
            {glyph === "hide" && <path d="M6 44 42 4" />}
         </svg>
         <span className={css.medalName}>
            <T k={labelKey}>{label}</T>
         </span>
      </label>
   );
}

function BadgePreview({ draft }: { draft: BadgeRequest }) {
   const { t } = useLocale();
   const hidden = draft.show === "hide";
   const pct = barPercent(draft);
   const [dark, light] = BAR_COLORS[barTier(draft)]!;
   const level = !hidden && draft.level === "custom" ? draft.customLevel : null;
   const need = level === null ? null : xpNeed(level);

   return (
      <div className={css.badgeStage}>
         <div className={css.badgeFrame}>
            {shownTier(draft) !== null && (
               <img className={css.badgeFrameArt} src={badgeSrc(shownTier(draft)!)} alt="" draggable={false} />
            )}
            {draft.show === "keep" && draft.onlyTier === null && (
               <span className={css.badgeFrameNote}>
                  <T k="badge.preview.own">Your real badge</T>
               </span>
            )}
            {hidden && (
               <span className={css.badgeFrameNote}>
                  <T k="badge.preview.none">No badge</T>
               </span>
            )}
            {!hidden && draft.level === "custom" && <span className={css.badgeFrameLevel}>{draft.customLevel}</span>}
         </div>

         <div className={css.badgeBarRow}>
            <span className={css.badgeBarLabel}>
               {pct === null
                  ? t("badge.preview.realProgress", "Real progress")
                  : need === null
                    ? `${pct}%`
                    : `${t("badge.preview.xp", "{have} XP / {need} XP", {
                         have: Math.round((need * pct) / 100).toLocaleString("en-US"),
                         need: need.toLocaleString("en-US"),
                      })} · ${pct}%`}
            </span>
            <div className={css.badgeBar} aria-hidden="true">
               {pct !== null && (
                  <span style={{ transform: `scaleX(${pct / 100})`, background: `linear-gradient(90deg, ${dark}, ${light})` }} />
               )}
            </div>
         </div>
      </div>
   );
}

function levelRange(tier: number, t: Translate): string {
   const from = BADGE_TIERS[tier]!.from;
   const to = tierTo(tier);
   return from === to
      ? t("badge.tier.level", "Lv {from}", { from })
      : t("badge.tier.levels", "Lv {from}-{to}", { from, to });
}

function describe(r: BadgeRequest, t: Translate): string {
   const parts: string[] = [];
   if (r.onlyTier !== null) {
      const only = tierOf(r.onlyTier);
      parts.push(t("badge.summary.only", "Only {tier} heroes", { tier: t(only.key, only.name) }));
   }
   if (r.show === "tier") parts.push(t(tierOf(r.tier).key, tierOf(r.tier).name));
   else if (r.show === "hide") parts.push(t("badge.summary.hidden", "Badge hidden"));
   else parts.push(t("badge.summary.ownBadge", "Own badge"));

   if (r.show !== "hide") {
      if (r.level === "custom") parts.push(t("badge.summary.level", "Level {level}", { level: r.customLevel }));
      else if (r.level === "hide") parts.push(t("badge.summary.noLevel", "No level"));
   }

   if (r.progress === "max") parts.push(t("badge.summary.barFull", "Bar full"));
   else if (r.progress === "custom") parts.push(t("badge.summary.barAt", "Bar at {pct}%", { pct: r.customProgress }));

   return parts.join(" · ");
}
