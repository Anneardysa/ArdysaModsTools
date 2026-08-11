import { useEffect, useRef, useState } from "react";
import { send } from "../../bridge/host";
import { T, useLocale } from "../../bridge/i18n";
import { dropUpdateCard, store } from "./store";
import css from "./shell.module.css";

function daysText(daysAgo: number): string {
   if (daysAgo === 0) return "Today";
   if (daysAgo === 1) return "Yesterday";
   return `${daysAgo}d ago`;
}

export function UpdatesStrip() {
   const { t } = useLocale();
   const cards = store.use((s) => s.updatesCards);
   const total = store.use((s) => s.updatesTotal);
   const rowRef = useRef<HTMLDivElement>(null);
   const [atStart, setAtStart] = useState(true);
   const [atEnd, setAtEnd] = useState(true);

   const updateArrowState = () => {
      const row = rowRef.current;
      if (!row) return;
      const max = row.scrollWidth - row.clientWidth;
      setAtStart(row.scrollLeft <= 2);
      setAtEnd(row.scrollLeft >= max - 2);
   };

   useEffect(() => {
      updateArrowState();
      const row = rowRef.current;
      if (!row) return;
      const onWheel = (e: WheelEvent) => {
         if (row.scrollWidth <= row.clientWidth) return;
         e.preventDefault();
         row.scrollLeft += e.deltaY;
      };
      row.addEventListener("wheel", onWheel, { passive: false });
      window.addEventListener("resize", updateArrowState);
      return () => {
         row.removeEventListener("wheel", onWheel);
         window.removeEventListener("resize", updateArrowState);
      };
      // eslint-disable-next-line react-hooks/exhaustive-deps
   }, [cards.length]);

   const scroll = (dir: 1 | -1) => {
      const row = rowRef.current;
      if (!row) return;
      row.scrollLeft += dir * Math.max(row.clientWidth - 80, 160);
   };

   if (cards.length === 0) return null;

   return (
      <div id="updates-strip" className={`${css.updatesStrip} ${css.show}`}>
         <div className={css.updatesHead}>
            <span className={css.updatesTitle}>
               <T k="heroGallery.latestUpdates">Latest Updates</T>
            </span>
            <span id="updates-count" className={css.updatesCount}>
               {total} new
            </span>
         </div>
         <div className={css.updatesWrap}>
            <button
               id="updates-prev"
               type="button"
               data-no-drag
               className={`${css.updatesArrow} ${css.prev} ${atStart ? css.disabled : ""}`}
               title={t("shell.carousel.prev", "Previous")}
               onClick={() => scroll(-1)}
            >
               <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <polyline points="15 18 9 12 15 6" />
               </svg>
            </button>
            <div id="updates-row" ref={rowRef} className={css.updatesRow} onScroll={updateArrowState}>
               {cards.map((c, i) => (
                  <button
                     key={`${c.heroId ?? c.heroName}-${i}`}
                     type="button"
                     data-no-drag
                     className={css.updateCard}
                     title={c.setName || ""}
                     onClick={() => send("skinSelector")}
                  >
                     <img
                        src={c.setThumbnail || ""}
                        alt=""
                        loading="lazy"
                        onError={() => {
                           dropUpdateCard(i);
                           requestAnimationFrame(updateArrowState);
                        }}
                     />
                     <div className={css.ucCap}>
                        <div className={css.ucHero}>{c.heroName || ""}</div>
                        <div className={css.ucDate}>{daysText(c.daysAgo)}</div>
                     </div>
                     {c.daysAgo <= 2 && <span className={css.ucNew}>NEW</span>}
                  </button>
               ))}
            </div>
            <button
               id="updates-next"
               type="button"
               data-no-drag
               className={`${css.updatesArrow} ${css.next} ${atEnd ? css.disabled : ""}`}
               title={t("shell.carousel.next", "Next")}
               onClick={() => scroll(1)}
            >
               <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <polyline points="9 6 15 12 9 18" />
               </svg>
            </button>
         </div>
      </div>
   );
}
