import { useRef } from "react";
import { T } from "../../bridge/i18n";
import type { MiscOption, Selections } from "./store";
import { ThumbImage } from "./ThumbImage";
import { getThumbUrl } from "./thumbnails";
import css from "./misc.module.css";

export function OptionsGrid({
   options,
   selections,
   flashResetId,
   onOpen,
}: {
   options: MiscOption[];
   selections: Selections;
   flashResetId: string | null;
   onOpen: (index: number) => void;
}) {
   const animateRef = useRef(true);
   const animate = animateRef.current;
   animateRef.current = false;

   if (options.length === 0) {
      return (
         <div className={css.optionsGrid}>
            <div className={css.gridEmpty}>
               <T k="miscForm.noOptions">No options available</T>
            </div>
         </div>
      );
   }

   return (
      <div className={css.optionsGrid}>
         {options.map((opt, i) => {
            const selected = selections[opt.id] || opt.choices?.[0]?.id || "default";
            const selectedChoice = opt.choices?.find((c) => c.id === selected);
            const thumbUrl = getThumbUrl(opt, selected);
            const letter = (selectedChoice?.name || opt.name).charAt(0).toUpperCase();
            const defaultChoice = opt.choices?.[0]?.id || "default";
            const isChanged = selected !== defaultChoice;
            const flashing = flashResetId === opt.id;

            return (
               <button
                  type="button"
                  key={opt.id}
                  data-no-drag
                  className={[
                     css.tileCard,
                     isChanged ? css.selected : "",
                     animate ? css.animateFadeInUp : "",
                     flashing ? css.flashReset : "",
                  ]
                     .filter(Boolean)
                     .join(" ")}
                  style={animate ? { animationDelay: `${i * 15}ms` } : undefined}
                  onClick={() => onOpen(i)}
               >
                  <ThumbImage
                     src={thumbUrl}
                     alt=""
                     letter={letter}
                     imgClassName={css.tileThumb}
                     placeholderClassName={css.thumbLetter}
                  />
                  <div className={css.tileName}>{opt.name}</div>
                  <div className={css.tileChoice}>{selectedChoice?.name || "Default"}</div>
               </button>
            );
         })}
      </div>
   );
}
