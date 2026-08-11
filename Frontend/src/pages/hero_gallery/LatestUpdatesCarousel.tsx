import { useEffect, useRef, useState } from "react";
import { T } from "../../bridge/i18n";
import type { LatestUpdate } from "./types";
import css from "./gallery.module.css";

const CARD_WIDTH = 150;
const GAP = 10;
const WRAPPER_PADDING = 24;
const PLACEHOLDER =
   "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'%3E%3Crect fill='%23111' width='100' height='100'/%3E%3Ctext x='50' y='55' text-anchor='middle' fill='%23333' font-size='10'%3E?%3C/text%3E%3C/svg%3E";

function daysText(daysAgo: number): string {
   if (daysAgo === 0) return "Today";
   if (daysAgo === 1) return "Yesterday";
   return `${daysAgo}d ago`;
}

function UpdateCard({ update, onSelect }: { update: LatestUpdate; onSelect: () => void }) {
   return (
      <div className={css.updateCard} onClick={onSelect}>
         <div className={css.updateCardImage}>
            <img
               src={update.setThumbnail || PLACEHOLDER}
               alt={update.heroName}
               onError={(e) => {
                  e.currentTarget.onerror = null;
                  e.currentTarget.src = PLACEHOLDER;
               }}
            />
            <div className={css.updateCardGradient} />
            {update.daysAgo <= 2 && <span className={css.newBadge}>NEW</span>}
         </div>
         <div className={css.updateCardContent}>
            <p className={css.updateCardHero}>{update.heroName}</p>
            <p className={css.updateCardDate}>{daysText(update.daysAgo)}</p>
         </div>
      </div>
   );
}

export function LatestUpdatesCarousel({
   updates,
   collapsed,
   onToggleCollapsed,
   onSelectUpdate,
}: {
   updates: LatestUpdate[];
   collapsed: boolean;
   onToggleCollapsed: () => void;
   onSelectUpdate: (heroId: string, setIndex: number) => void;
}) {
   const wrapperRef = useRef<HTMLDivElement>(null);
   const [index, setIndex] = useState(0);
   const [visibleCards, setVisibleCards] = useState(1);

   const count = updates.length;

   useEffect(() => {
      const measure = () => {
         const wrapper = wrapperRef.current;
         if (!wrapper) return;
         const width = wrapper.clientWidth - WRAPPER_PADDING;
         setVisibleCards(Math.max(1, Math.floor((width + GAP) / (CARD_WIDTH + GAP))));
      };
      measure();
      window.addEventListener("resize", measure);
      return () => window.removeEventListener("resize", measure);
   }, [count]);

   useEffect(() => {
      const wrapper = wrapperRef.current;
      if (!wrapper) return;
      const onWheel = (e: WheelEvent) => {
         const maxScrollIndex = Math.max(0, count - visibleCards);
         if (maxScrollIndex <= 0) return;
         e.preventDefault();
         setIndex((i) => Math.max(0, Math.min(maxScrollIndex, i + (e.deltaY > 0 ? 1 : -1))));
      };
      wrapper.addEventListener("wheel", onWheel, { passive: false });
      return () => wrapper.removeEventListener("wheel", onWheel);
   }, [count, visibleCards]);

   if (count === 0) return null;

   const maxScrollIndex = Math.max(0, count - visibleCards);
   const clampedIndex = Math.min(index, maxScrollIndex);
   const offset = clampedIndex * (CARD_WIDTH + GAP);
   const step = Math.max(1, Math.floor(visibleCards / 2));

   const showDots = count > visibleCards && maxScrollIndex > 0;
   const totalDots = Math.min(maxScrollIndex + 1, 5);
   const dotStep = totalDots > 1 ? maxScrollIndex / (totalDots - 1) : 0;

   return (
      <section id="latestUpdatesSection" className={css.luSection}>
         <div className={css.luHead}>
            <div className={css.luHeadLeft}>
               <span className={css.luTitle}>
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <path d="M13 10V3L4 14h7v7l9-11h-7z" />
                  </svg>
                  <T k="heroGallery.latestUpdates">Latest Updates</T>
               </span>
               <span id="updatesCount" className={css.updatesCount}>{count} new</span>
            </div>
            <button type="button" data-no-drag className={`${css.btn} ${css.ghost}`} onClick={onToggleCollapsed}>
               {collapsed ? "Show" : <T k="heroGallery.toggleHide">Hide</T>}
               <svg
                  style={{ width: 13, height: 13, transition: "transform 0.3s", transform: collapsed ? "rotate(180deg)" : "rotate(0deg)" }}
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  viewBox="0 0 24 24"
                  aria-hidden="true"
               >
                  <path d="M5 15l7-7 7 7" />
               </svg>
            </button>
         </div>

         <div className={`${css.latestUpdatesContainer} ${collapsed ? css.collapsed : ""}`}>
            <div className={css.carouselWrapper} ref={wrapperRef}>
               <button
                  type="button"
                  data-no-drag
                  className={`${css.carouselArrow} ${css.carouselArrowLeft} ${clampedIndex <= 0 ? css.disabled : ""}`}
                  onClick={() => setIndex(Math.max(0, clampedIndex - step))}
                  aria-label="Previous"
               >
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <path d="M15 19l-7-7 7-7" />
                  </svg>
               </button>

               <div className={css.carouselTrack} style={{ transform: `translateX(-${offset}px)` }}>
                  {updates.map((u, i) => (
                     <UpdateCard key={`${u.heroId}-${u.setIndex}-${i}`} update={u} onSelect={() => onSelectUpdate(u.heroId, u.setIndex)} />
                  ))}
               </div>

               <button
                  type="button"
                  data-no-drag
                  className={`${css.carouselArrow} ${css.carouselArrowRight} ${clampedIndex >= maxScrollIndex ? css.disabled : ""}`}
                  onClick={() => setIndex(Math.min(maxScrollIndex, clampedIndex + step))}
                  aria-label="Next"
               >
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <path d="M9 5l7 7-7 7" />
                  </svg>
               </button>
            </div>

            {showDots && (
               <div className={css.carouselDots}>
                  {Array.from({ length: totalDots }, (_, i) => {
                     const dotIndex = Math.round(i * dotStep);
                     const isActive =
                        (i === 0 && clampedIndex <= dotStep / 2) ||
                        (i === totalDots - 1 && clampedIndex >= maxScrollIndex - dotStep / 2) ||
                        (clampedIndex >= dotIndex - dotStep / 2 && clampedIndex < dotIndex + dotStep / 2);
                     return (
                        <button
                           key={i}
                           type="button"
                           data-no-drag
                           className={`${css.carouselDot} ${isActive ? css.active : ""}`}
                           aria-label={`Page ${i + 1}`}
                           onClick={() => setIndex(dotIndex)}
                        />
                     );
                  })}
               </div>
            )}
         </div>
      </section>
   );
}
