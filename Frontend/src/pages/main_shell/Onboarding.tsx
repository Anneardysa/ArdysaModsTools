import { useLayoutEffect, useRef, useState } from "react";
import { useLocale } from "../../bridge/i18n";
import { finishOnboarding, onboardNext, store } from "./store";
import css from "./shell.module.css";

type Rect = { left: number; top: number; width: number; height: number };
type CardPos = { left: number; top: number };

const FALLBACK_CARD = { width: 300, height: 190 };

function computePosition(targetId: string, pad: number, cardEl: HTMLDivElement | null): { spot: Rect; card: CardPos } {
   const vw = window.innerWidth;
   const vh = window.innerHeight;
   const el = document.getElementById(targetId);

   const spot: Rect = el
      ? (() => {
           const r = el.getBoundingClientRect();
           return { left: r.left - pad, top: r.top - pad, width: r.width + pad * 2, height: r.height + pad * 2 };
        })()
      : { left: vw / 2 - 80, top: vh / 2 - 24, width: 160, height: 48 };

   const cw = cardEl?.offsetWidth || FALLBACK_CARD.width;
   const ch = cardEl?.offsetHeight || FALLBACK_CARD.height;
   const gap = 16;
   const m = 12;
   let cx: number, cy: number;
   if (spot.left + spot.width + gap + cw <= vw - m) {
      cx = spot.left + spot.width + gap;
      cy = spot.top;
   } else if (spot.left - gap - cw >= m) {
      cx = spot.left - gap - cw;
      cy = spot.top;
   } else if (spot.top + spot.height + gap + ch <= vh - m) {
      cx = spot.left;
      cy = spot.top + spot.height + gap;
   } else {
      cx = spot.left;
      cy = spot.top - gap - ch;
   }
   cx = Math.max(m, Math.min(cx, vw - cw - m));
   cy = Math.max(m, Math.min(cy, vh - ch - m));
   return { spot, card: { left: cx, top: cy } };
}

export function Onboarding() {
   const { t } = useLocale();
   const ob = store.use((s) => s.onboarding);
   const cardRef = useRef<HTMLDivElement>(null);
   const [pos, setPos] = useState<{ spot: Rect; card: CardPos } | null>(null);

   const step = ob?.steps[ob.index];

   useLayoutEffect(() => {
      if (!step) return;
      const reposition = () => setPos(computePosition(step.target, typeof step.pad === "number" ? step.pad : 8, cardRef.current));
      reposition();
      window.addEventListener("resize", reposition);
      return () => window.removeEventListener("resize", reposition);
      // eslint-disable-next-line react-hooks/exhaustive-deps
   }, [step?.target, step?.title, step?.desc, ob?.index]);

   if (!ob || !step || !pos) return null;

   const last = ob.index === ob.steps.length - 1;

   return (
      <div id="onboard" className={`${css.onboard} ${css.show}`} aria-hidden="false">
         <div
            id="onboard-spot"
            className={css.onboardSpot}
            style={{ left: pos.spot.left, top: pos.spot.top, width: pos.spot.width, height: pos.spot.height }}
         >
            <span className={css.onboardGlow} />
         </div>
         <div id="onboard-card" ref={cardRef} className={css.onboardCard} style={{ left: pos.card.left, top: pos.card.top }}>
            <div className={css.obHead}>
               <span id="ob-counter" className={css.obCounter}>
                  [ {ob.index + 1} / {ob.steps.length} ]
               </span>
               <div id="ob-dots" className={css.obDots}>
                  {ob.steps.map((_, i) => (
                     <span key={i} className={`${css.obDot} ${i === ob.index ? css.active : ""} ${i < ob.index ? css.done : ""}`} />
                  ))}
               </div>
            </div>
            <div id="ob-title" className={css.obTitle}>
               {step.title}
            </div>
            <div className={css.obSep} />
            <div id="ob-desc" className={css.obDesc}>
               {step.desc}
            </div>
            <div className={css.obActions}>
               <button
                  id="ob-skip"
                  type="button"
                  data-no-drag
                  className={`${css.obBtn} ${css.ghost}`}
                  style={{ visibility: last ? "hidden" : "visible" }}
                  onClick={finishOnboarding}
               >
                  {t("shell.onboard.skip", "Skip")}
               </button>
               <button id="ob-next" type="button" data-no-drag className={`${css.obBtn} ${css.primary}`} onClick={onboardNext}>
                  {last ? t("shell.onboard.gotIt", "Got it") : t("shell.onboard.next", "Next →")}
               </button>
            </div>
         </div>
      </div>
   );
}

