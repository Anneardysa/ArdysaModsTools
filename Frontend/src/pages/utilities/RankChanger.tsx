import { useEffect, useState } from "react";
import { send } from "../../bridge/host";
import { T, useLocale } from "../../bridge/i18n";
import { Button } from "../../ui/Button";
import {
   clampLbRank,
   LB_RANK_MAX,
   MEDALS,
   MMR_MAX,
   type Medal,
   medalForMmr,
   medalOf,
   medalSrc,
   mmrOf,
   pipSrc,
   STAR_COUNTS,
   tierHasStars,
} from "./ranks";
import { store, type RankRequest } from "./store";
import css from "./utilities.module.css";

const DEFAULT_DRAFT: RankRequest = {
   replaceEveryRank: false,
   currentTier: 4,
   currentStars: 1,
   targetTier: 7,
   targetStars: 5,
   applyStars: true,
   applyProfile: true,
   applyScoreboard: true,
   targetLeaderboard: 0,
   profileMmr: 5420,
   rankConfidence: 100,
   leaderboardRank: 6,
};

export function RankChanger() {
   const { t } = useLocale();
   const hydrated = store.use((s) => s.hydrated);
   const applied = store.use((s) => s.applied);
   const saved = store.use((s) => s.saved);
   const busy = store.use((s) => s.busy);
   const isModInstalled = store.use((s) => s.isModInstalled);

   const [draft, setDraft] = useState<RankRequest>(DEFAULT_DRAFT);

   useEffect(() => {
      setDraft(saved ? { ...DEFAULT_DRAFT, ...saved } : DEFAULT_DRAFT);
   }, [saved]);

   const set = (patch: Partial<RankRequest>) => setDraft((d) => ({ ...d, ...patch }));

   const target = medalOf(draft.targetTier, draft.targetLeaderboard);
   const current = medalOf(draft.currentTier, 0);

   const pickMedal = (medal: Medal) => {
      let lbRank = draft.leaderboardRank;
      if (medal.tier === 8 && medal.lb > 0) {
         if (lbRank < 1 || lbRank > LB_RANK_MAX[medal.lb]!) {
            lbRank = medal.lb === 3 ? 6 : medal.lb === 2 ? 50 : 500;
         }
      }
      set({
         targetTier: medal.tier,
         targetLeaderboard: medal.lb,
         profileMmr: mmrOf(medal, draft.targetStars),
         leaderboardRank: lbRank,
      });
   };

   const pickStars = (targetStars: number) => set({ targetStars, profileMmr: mmrOf(target, targetStars) });

   const pickMmr = (profileMmr: number) => {
      const match = medalForMmr(profileMmr);
      set({
         profileMmr,
         targetTier: match.medal.tier,
         targetLeaderboard: match.medal.lb,
         targetStars: tierHasStars(match.medal.tier) ? match.stars : draft.targetStars,
         leaderboardRank: clampLbRank(draft.leaderboardRank, match.medal.lb),
      });
   };

   const starsActive =
      tierHasStars(draft.targetTier) && (draft.replaceEveryRank || tierHasStars(draft.currentTier));

   const sameRank =
      applied && saved
         ? sameRequest(draft, saved)
         : !draft.replaceEveryRank &&
           draft.targetTier === draft.currentTier &&
           (draft.targetTier !== 8 || draft.targetLeaderboard === 0) &&
           (!starsActive || draft.targetStars === draft.currentStars);

   const canApply = hydrated && !busy && !sameRank && isModInstalled;

   const wipesPips = !draft.replaceEveryRank && (draft.targetTier === 8 || draft.targetTier === 0);

   if (!hydrated) {
      return (
         <section className={css.panel}>
            <p className={css.loading}>
               <T k="utilities.loading">Reading what is installed…</T>
            </p>
         </section>
      );
   }

   const activeMedal = applied && saved ? medalOf(saved.targetTier, saved.targetLeaderboard) : medalOf(draft.currentTier, 0);
   const activeStars = applied && saved ? saved.targetStars : draft.currentStars;
   const activeHasStars = applied && saved ? (saved.applyStars && tierHasStars(saved.targetTier)) : tierHasStars(draft.currentTier);

   return (
      <section className={css.panel} aria-busy={busy}>
         <div className={css.scroll}>
            {!isModInstalled && (
               <div className={css.modsRequiredBanner} role="alert">
                  <svg className={css.bannerIcon} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
                     <line x1="12" y1="9" x2="12" y2="13" />
                     <line x1="12" y1="17" x2="12.01" y2="17" />
                  </svg>
                  <div className={css.bannerContent}>
                     <strong className={css.bannerTitle}>
                        <T k="rank.banner.modsRequiredTitle">Mods Not Installed</T>
                     </strong>
                     <p className={css.bannerText}>
                        <T k="rank.banner.modsRequired">
                           Rank Changer requires an installed mod package (game/_ArdysaMods or game/mod). Please install Modspack, Skin Selector, or Miscellaneous first.
                        </T>
                     </p>
                  </div>
               </div>
            )}
            <header className={css.head}>
               <div className={css.headRow}>
                  <div>
                     <h1 className={css.h1}>
                        <T k="utilities.tool.rankChanger">Rank Changer</T>
                     </h1>
                     <p className={css.lede}>
                        <T k="rank.lede">
                           Redraws the medal your own client shows. Other players still see your real rank.
                        </T>
                     </p>
                     <p className={applied ? css.stateOn : css.stateOff}>
                        {applied && saved ? (
                           <T k="rank.state.applied" values={{ rank: describe(saved, t) }}>{`Applied — ${describe(saved, t)}`}</T>
                        ) : (
                           <T k="rank.state.clean">No override — the game draws its own medals.</T>
                        )}
                     </p>
                  </div>

                  <div className={css.activeRankBadge}>
                     <div className={css.activeMedalWrap}>
                        <img
                           className={css.activeMedalArt}
                           src={medalSrc(activeMedal)}
                           alt=""
                           draggable={false}
                        />
                        {activeHasStars && (
                           <img className={css.activePips} src={pipSrc(activeStars)} alt="" draggable={false} />
                        )}
                     </div>
                     <div className={css.activeMeta}>
                        <span className={`${css.activeStatusTag} ${applied ? css.activeStatusOn : css.activeStatusOff}`}>
                           {applied ? (
                              <T k="rank.state.activeBadge">MODIFIED RANK</T>
                           ) : (
                              <T k="rank.state.defaultBadge">ORIGINAL RANK</T>
                           )}
                        </span>
                        <span className={css.activeRankTitle}>
                           {applied && saved ? describe(saved, t) : describeSide(medalOf(draft.currentTier, 0), draft.currentStars, tierHasStars(draft.currentTier), t)}
                        </span>
                     </div>
                  </div>
               </div>
            </header>

            <div className={css.cols}>
               <div className={css.colMain}>
                  <Field labelKey="rank.field.target" label="Show this instead">
                     <div className={css.medals} role="radiogroup" aria-label={t("rank.field.target", "Show this instead")}>
                        {MEDALS.map((medal) => (
                           <label key={medal.id} className={`${css.medal} ${target.id === medal.id ? css.medalOn : ""}`}>
                              <input
                                 type="radio"
                                 name="targetMedal"
                                 className={css.srOnly}
                                 checked={target.id === medal.id}
                                 disabled={busy}
                                 onChange={() => pickMedal(medal)}
                              />
                              <img className={css.medalArt} src={medalSrc(medal)} alt="" draggable={false} />
                              <span className={css.medalName}>{t(medal.key, medal.name)}</span>
                           </label>
                        ))}
                     </div>

                     <div className={css.row}>
                        <span className={css.inlineLabel}>
                           <T k="rank.field.stars">Stars</T>
                        </span>
                        <StarPicker
                           name="targetStars"
                           value={draft.targetStars}
                           onChange={pickStars}
                           disabled={busy || !starsActive}
                        />
                        {!tierHasStars(draft.targetTier) && (
                           <span className={css.hint}>
                              <T k="rank.field.stars.none" values={{ tier: t(target.key, target.name) }}>
                                 {`${target.name} carries no stars.`}
                              </T>
                           </span>
                        )}
                     </div>

                     {wipesPips && (
                        <p className={css.warn} role="alert">
                           <T k="rank.field.stars.wipeWarning">
                              The star overlay is shared by every player's medal — picking this here also
                              erases everyone else's stars in your scoreboard, even in "Only my rank" mode.
                           </T>
                        </p>
                     )}
                  </Field>

                  <Field labelKey="rank.field.numbers" label="Profile numbers">
                     <Slider
                        label={t("rank.surface.profile", "MMR in profile")}
                        value={draft.profileMmr}
                        min={0}
                        max={MMR_MAX}
                        step={10}
                        disabled={busy}
                        readout={`${draft.profileMmr.toLocaleString()}${draft.profileMmr >= MMR_MAX ? "+" : ""} MMR`}
                        onChange={pickMmr}
                     />
                     <div className={css.sliderRow}>
                        <Slider
                           label={t("rank.surface.scoreboard", "Rank Confidence")}
                           value={draft.rankConfidence}
                           min={0}
                           max={100}
                           step={1}
                           disabled={busy}
                           readout={`${draft.rankConfidence}%`}
                           onChange={(rankConfidence) => set({ rankConfidence })}
                        />
                        {draft.targetTier === 8 && draft.targetLeaderboard > 0 && (
                           <div className={css.sliderCompactWrap}>
                              <Slider
                                 label={t("rank.surface.leaderboard", "Leaderboard rank")}
                                 value={draft.leaderboardRank}
                                 min={1}
                                 max={LB_RANK_MAX[draft.targetLeaderboard]!}
                                 step={1}
                                 disabled={busy}
                                 readout={`#${draft.leaderboardRank}`}
                                 onChange={(leaderboardRank) => set({ leaderboardRank })}
                              />
                           </div>
                        )}
                     </div>
                  </Field>
               </div>

               <aside className={css.colSide}>
                  <Field labelKey="rank.field.mode" label="Mode">
                     <div className={css.segment} role="radiogroup" aria-label={t("rank.field.mode", "Mode")}>
                        <Segment
                           name="mode"
                           checked={draft.replaceEveryRank}
                           onChange={() => set({ replaceEveryRank: true })}
                           labelKey="rank.mode.every"
                           label="Replace every rank"
                           hintKey="rank.mode.every.hint"
                           hint="Every medal in a scoreboard becomes the one you pick."
                           disabled={busy}
                        />
                        <Segment
                           name="mode"
                           checked={!draft.replaceEveryRank}
                           onChange={() => set({ replaceEveryRank: false })}
                           labelKey="rank.mode.mine"
                           label="Only my rank"
                           hintKey="rank.mode.mine.hint"
                           hint="Only your own medal changes; everyone else stays truthful."
                           disabled={busy}
                        />
                     </div>
                  </Field>

                  {!draft.replaceEveryRank && (
                     <Field labelKey="rank.field.current" label="Your current rank">
                        <div className={css.row}>
                           <img className={css.currentArt} src={medalSrc(current)} alt="" draggable={false} />
                           <label className={css.selectWrap}>
                              <span className={css.srOnly}>{t("rank.field.currentTier", "Current medal")}</span>
                              <select
                                 className={css.select}
                                 value={draft.currentTier}
                                 disabled={busy}
                                 onChange={(e) => set({ currentTier: Number(e.target.value) })}
                              >
                                 {MEDALS.filter((m) => m.lb === 0).map((medal) => (
                                    <option key={medal.tier} value={medal.tier}>
                                       {t(medal.key, medal.name)}
                                    </option>
                                 ))}
                              </select>
                           </label>
                        </div>
                        <StarPicker
                           name="currentStars"
                           value={draft.currentStars}
                           onChange={(currentStars) => set({ currentStars })}
                           disabled={busy || !tierHasStars(draft.currentTier)}
                        />
                        <p className={css.hint}>
                           <T k="rank.field.current.hint">
                              Dota never tells the app your rank, so pick the medal you actually have — that is
                              the one the chosen medal replaces.
                           </T>
                        </p>
                     </Field>
                  )}
               </aside>
            </div>
         </div>

         <footer className={css.actions}>
            <div className={css.preview} aria-hidden="true">
               <img className={css.previewArt} src={medalSrc(target)} alt="" draggable={false} />
               {starsActive && <img className={css.previewPips} src={pipSrc(draft.targetStars)} alt="" draggable={false} />}
            </div>

            <p className={css.summary}>
               {draft.replaceEveryRank ? (
                  <T k="rank.summary.every" values={{ rank: describe(draft, t) }}>{`Every medal → ${describe(draft, t)}`}</T>
               ) : (
                  <T
                     k="rank.summary.mine"
                     values={{ from: describeSide(current, draft.currentStars, starsActive, t), to: describe(draft, t) }}
                  >
                     {`${describeSide(current, draft.currentStars, starsActive, t)} → ${describe(draft, t)}`}
                  </T>
               )}
               {sameRank && (
                  <span className={css.warn}>
                     {" "}
                     <T k="rank.summary.noChange">Nothing to change.</T>
                  </span>
               )}
               <span className={css.restart}>
                  <T k="rank.summary.restart">
                     Rebuilds the mod package, so Dota 2 must be closed — and restarted afterwards to see it.
                  </T>
               </span>
            </p>

            <Button
               variant="ghost"
               disabled={!applied || busy || !isModInstalled}
               onClick={() => {
                  store.set({ runKind: "reset", runTool: "rankChanger" });
                  send("rankReset");
               }}
               title={t("rank.action.reset.tip", "Put the game's own medals back.")}
            >
               <T k="rank.action.reset">Reset</T>
            </Button>
            <Button
               variant="primary"
               disabled={!canApply}
               onClick={() => {
                  store.set({ runKind: "apply", runTool: "rankChanger" });
                  send("rankApply", { request: draft });
               }}
               title={!isModInstalled ? t("rank.action.apply.modsRequired", "Install mods first to apply Rank Changer") : undefined}
            >
               {busy ? <T k="rank.action.working">Working…</T> : <T k="rank.action.apply">Apply</T>}
            </Button>
         </footer>
      </section>
   );
}

export function Field({ labelKey, label, children }: { labelKey: string; label: string; children: React.ReactNode }) {
   return (
      <div className={css.field}>
         <h2 className={css.fieldLabel}>
            <T k={labelKey}>{label}</T>
         </h2>
         {children}
      </div>
   );
}

export function Segment({
   name,
   checked,
   onChange,
   labelKey,
   label,
   hintKey,
   hint,
   disabled,
}: {
   name: string;
   checked: boolean;
   onChange: () => void;
   labelKey: string;
   label: string;
   hintKey: string;
   hint: string;
   disabled?: boolean;
}) {
   return (
      <label className={`${css.segmentItem} ${checked ? css.segmentOn : ""}`}>
         <input type="radio" name={name} className={css.srOnly} checked={checked} disabled={disabled} onChange={onChange} />
         <span className={css.segmentName}>
            <T k={labelKey}>{label}</T>
         </span>
         <span className={css.segmentHint}>
            <T k={hintKey}>{hint}</T>
         </span>
      </label>
   );
}

function StarPicker({
   name,
   value,
   onChange,
   disabled,
}: {
   name: string;
   value: number;
   onChange: (stars: number) => void;
   disabled?: boolean;
}) {
   const { t } = useLocale();

   return (
      <div
         className={`${css.stars} ${disabled ? css.starsOff : ""}`}
         role="radiogroup"
         aria-label={t("rank.field.stars", "Stars")}
      >
         {STAR_COUNTS.map((stars) => (
            <label key={stars} className={css.star} title={String(stars)}>
               <input
                  type="radio"
                  name={name}
                  className={css.srOnly}
                  checked={value === stars}
                  disabled={disabled}
                  onChange={() => onChange(stars)}
               />
               <span className={css.srOnly}>{stars}</span>
               <svg viewBox="0 0 24 24" aria-hidden="true" className={stars <= value ? css.starOn : css.starOff}>
                  <path
                     d="M12 3.2l2.6 5.4 5.9.8-4.3 4.2 1 5.9-5.2-2.8-5.2 2.8 1-5.9L3.5 9.4l5.9-.8z"
                     fill={stars <= value ? "currentColor" : "none"}
                     stroke="currentColor"
                     strokeWidth="1.6"
                     strokeLinejoin="round"
                  />
               </svg>
            </label>
         ))}
      </div>
   );
}

export function Slider({
   label,
   value,
   min,
   max,
   step,
   readout,
   disabled,
   onChange,
}: {
   label: string;
   value: number;
   min: number;
   max: number;
   step: number;
   readout: string;
   disabled?: boolean;
   onChange: (next: number) => void;
}) {
   return (
      <label className={`${css.slider} ${disabled ? css.sliderOff : ""}`}>
         <span className={css.sliderHead}>
            <span className={css.sliderName}>{label}</span>
            <output className={css.sliderValue}>{readout}</output>
         </span>
         <input
            type="range"
            className={css.range}
            value={value}
            min={min}
            max={max}
            step={step}
            disabled={disabled}
            onChange={(e) => onChange(Number(e.target.value))}
         />
      </label>
   );
}

function describe(r: RankRequest, t: (key: string, fallback?: string) => string): string {
   const medal = medalOf(r.targetTier, r.targetLeaderboard);
   return describeSide(medal, r.targetStars, r.applyStars && tierHasStars(r.targetTier), t);
}

function describeSide(
   medal: Medal,
   stars: number,
   withStars: boolean,
   t: (key: string, fallback?: string) => string,
): string {
   const name = t(medal.key, medal.name);
   return withStars ? `${name} ★${stars}` : name;
}

function sameRequest(a: RankRequest, b: RankRequest): boolean {
   return (
      a.replaceEveryRank === b.replaceEveryRank &&
      a.currentTier === b.currentTier &&
      a.currentStars === b.currentStars &&
      a.targetTier === b.targetTier &&
      a.targetStars === b.targetStars &&
      a.applyStars === b.applyStars &&
      a.applyProfile === b.applyProfile &&
      a.applyScoreboard === b.applyScoreboard &&
      a.targetLeaderboard === b.targetLeaderboard &&
      a.profileMmr === b.profileMmr &&
      a.rankConfidence === b.rankConfidence &&
      a.leaderboardRank === b.leaderboardRank
   );
}
