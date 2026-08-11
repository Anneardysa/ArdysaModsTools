import { useEffect, useState } from "react";
import { send } from "../../bridge/host";
import { useLocale } from "../../bridge/i18n";
import { carouselGo, store } from "./store";
import css from "./shell.module.css";

const AUTO_ADVANCE_MS = 5000;

export function Carousel() {
   const { t } = useLocale();
   const slides = store.use((s) => s.carouselSlides);
   const index = store.use((s) => s.carouselIndex);
   const [paused, setPaused] = useState(false);

   useEffect(() => {
      if (slides.length <= 1 || paused) return;
      if (window.matchMedia?.("(prefers-reduced-motion: reduce)").matches) return;

      const id = window.setInterval(() => {
         if (!document.hidden) carouselGo(store.get().carouselIndex + 1);
      }, AUTO_ADVANCE_MS);
      return () => window.clearInterval(id);
   }, [slides, index, paused]);

   if (slides.length === 0) return null;

   return (
      <div id="carousel" className={`${css.carousel} ${slides.length > 1 ? css.multi : ""}`} onMouseEnter={() => setPaused(true)} onMouseLeave={() => setPaused(false)}>
         <div id="carousel-track" className={css.carouselTrack} style={{ transform: `translateX(-${index * 100}%)` }}>
            {slides.map((s, i) => (
               <div key={i} className={`${css.slide} ${s.link ? css.link : ""}`} onClick={s.link ? () => send("openUrl", { url: s.link }) : undefined}>
                  <img
                     alt={s.title || ""}
                     src={s.image}
                     onError={(e) => {
                        e.currentTarget.style.display = "none";
                     }}
                  />
                  {s.title && <div className={css.cap}>{s.title}</div>}
               </div>
            ))}
         </div>
         <button
            type="button"
            data-no-drag
            className={`${css.carArrow} ${css.prev}`}
            title={t("shell.carousel.prev", "Previous")}
            onClick={(e) => {
               e.stopPropagation();
               carouselGo(index - 1);
            }}
         >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
               <polyline points="15 18 9 12 15 6" />
            </svg>
         </button>
         <button
            type="button"
            data-no-drag
            className={`${css.carArrow} ${css.next}`}
            title={t("shell.carousel.next", "Next")}
            onClick={(e) => {
               e.stopPropagation();
               carouselGo(index + 1);
            }}
         >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
               <polyline points="9 6 15 12 9 18" />
            </svg>
         </button>
         <div id="car-dots" className={css.carDots}>
            {slides.map((_, i) => (
               <span
                  key={i}
                  className={`${css.carDot} ${i === index ? css.active : ""}`}
                  onClick={(e) => {
                     e.stopPropagation();
                     carouselGo(i);
                  }}
               />
            ))}
         </div>
      </div>
   );
}
