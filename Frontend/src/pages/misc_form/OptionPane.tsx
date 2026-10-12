import { useEffect, useRef, useState } from "react";
import { T, useLocale } from "../../bridge/i18n";
import { DotaIcon } from "./DotaIcon";
import { iconForOption } from "./dotaIcons";
import {
   MAX_ETHEREAL_SLOTS,
   defaultChoiceId,
   etherealKey,
   filterChoices,
   hasEthereal,
   isChanged,
   isChoiceSelected,
   parseEthereal,
   selectedChoiceId,
   shownStyleId,
} from "./helpers";
import { cycleStyle, resetOption, selectChoice, store, type Choice, type MiscOption, type Selections } from "./store";
import { ThumbImage } from "./ThumbImage";
import { getThumbUrl } from "./thumbnails";
import css from "./misc.module.css";

function ChoiceCard({ opt, choice, selectedId }: { opt: MiscOption; choice: Choice; selectedId: string }) {
   const { t } = useLocale();
   const selected = isChoiceSelected(choice, selectedId);
   const [shownId, setShownId] = useState(() => shownStyleId(choice, selectedId));

   useEffect(() => {
      if (selected) setShownId(shownStyleId(choice, selectedId));
   }, [selected, selectedId, choice]);

   const styles = choice.styles ?? [];
   const targetId = shownId ?? choice.id;
   const isDefault = choice.id === defaultChoiceId(opt);
   const styleIndex = Math.max(0, styles.findIndex((s) => s.id === targetId));
   const cycle = (dir: 1 | -1) => setShownId(cycleStyle(opt.id, choice, targetId, dir));

   return (
      <div className={css.card}>
         <button
            type="button"
            data-no-drag
            className={`${css.cardBtn} ${selected ? css.selected : ""}`}
            aria-pressed={selected}
            onClick={() => selectChoice(opt.id, targetId)}
         >
            <span className={css.cardArt}>
               {isDefault ? (
                  <DotaIcon name={iconForOption(opt.id)} className={css.cardDefaultIco} />
               ) : (
                  <ThumbImage
                     src={getThumbUrl(opt, targetId)}
                     alt=""
                     letter={choice.name.charAt(0).toUpperCase()}
                     imgClassName={css.cardThumb}
                     placeholderClassName={css.cardLetter}
                  />
               )}
            </span>
            <span className={css.cardName} title={choice.name}>
               {choice.name}
            </span>
            <span className={css.cardState} aria-hidden="true">
               {selected ? <T k="miscForm.card.selected">Selected</T> : <T k="miscForm.card.select">Select</T>}
            </span>
         </button>
         {styles.length > 1 && (
            <div className={css.pager}>
               <button type="button" data-no-drag className={css.pagerBtn} onClick={() => cycle(-1)} aria-label={t("miscForm.style.prev", "Previous style")}>
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <path d="M15 19l-7-7 7-7" />
                  </svg>
               </button>
               <span className={css.pagerCount} aria-live="polite">
                  {styleIndex + 1} / {styles.length}
               </span>
               <button type="button" data-no-drag className={css.pagerBtn} onClick={() => cycle(1)} aria-label={t("miscForm.style.next", "Next style")}>
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <path d="M9 5l7 7-7 7" />
                  </svg>
               </button>
            </div>
         )}
      </div>
   );
}

export function OptionPane({ opt, selections, search }: { opt: MiscOption; selections: Selections; search: string }) {
   const { t } = useLocale();
   const scrollRef = useRef<HTMLDivElement>(null);
   const selectedId = selectedChoiceId(opt, selections);
   const choices = filterChoices(opt.choices ?? [], search);
   const etherealCount = parseEthereal(selections[etherealKey(opt)]).length;

   useEffect(() => {
      const el = scrollRef.current;
      if (!el) return;
      el.scrollTop = 0;
      const card = el.querySelector(`.${css.selected}`);
      if (!card) return;
      const box = el.getBoundingClientRect();
      const r = card.getBoundingClientRect();
      el.scrollTop = r.top - box.top - (el.clientHeight - r.height) / 2;
   }, [opt.id]);

   return (
      <section className={css.pane} aria-labelledby="paneTitle">
         <header className={css.paneHead}>
            <div className={css.paneTitleRow}>
               <DotaIcon name={iconForOption(opt.id)} className={css.paneIco} />
               <h2 id="paneTitle" className={css.paneTitle}>
                  {opt.name}
               </h2>
            </div>
            <div className={css.paneTools}>
               <div className={css.search}>
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <path d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                  </svg>
                  <input
                     type="text"
                     data-no-drag
                     value={search}
                     onChange={(e) => store.set({ search: e.target.value })}
                     placeholder={t("miscForm.search", `Search ${opt.name}`, { name: opt.name })}
                     aria-label={t("miscForm.search", `Search ${opt.name}`, { name: opt.name })}
                  />
               </div>
               <button type="button" data-no-drag className={`${css.btn} ${css.ghost}`} disabled={!isChanged(opt, selections)} onClick={() => resetOption(opt.id)}>
                  <T k="miscForm.reset">Reset to default</T>
               </button>
            </div>
         </header>

         <div className={css.paneBar}>
            <span className={css.paneMeta}>
               {t("miscForm.choiceCount", `${opt.choices?.length ?? 0} choices`, { count: opt.choices?.length ?? 0 })}
               <span className={css.paneSep} aria-hidden="true">
                  /
               </span>
               <T k="miscForm.current">Current</T>: <span className={css.paneCurrent}>{selectedId}</span>
            </span>
            {hasEthereal(opt) && (
               <button type="button" data-no-drag className={`${css.btn} ${css.ghost}`} onClick={() => store.set({ etherealOpen: true })}>
                  <T k="miscForm.ethereal.open">Ethereal effects</T>
                  <span className={css.btnCount}>
                     {etherealCount}/{MAX_ETHEREAL_SLOTS}
                  </span>
               </button>
            )}
         </div>

         <div ref={scrollRef} className={css.paneScroll}>
            {choices.length > 0 ? (
               <div className={css.cardGrid}>
                  {choices.map((c) => (
                     <ChoiceCard key={c.id} opt={opt} choice={c} selectedId={selectedId} />
                  ))}
               </div>
            ) : (
               <div className={css.paneEmpty}>
                  {search.trim() ? (
                     <>
                        <p>{t("miscForm.noMatch", `Nothing matches "${search.trim()}"`, { query: search.trim() })}</p>
                        <button type="button" data-no-drag className={`${css.btn} ${css.ghost}`} onClick={() => store.set({ search: "" })}>
                           <T k="miscForm.clearSearch">Clear search</T>
                        </button>
                     </>
                  ) : (
                     <p>
                        <T k="miscForm.noChoices">This option has no choices yet.</T>
                     </p>
                  )}
               </div>
            )}
         </div>
      </section>
   );
}
