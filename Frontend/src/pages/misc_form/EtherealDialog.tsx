import { T, translate, useLocale } from "../../bridge/i18n";
import { ETHEREAL_EFFECTS } from "./ethereal-effects";
import { MAX_ETHEREAL_SLOTS, etherealKey, parseEthereal, selectedChoiceId } from "./helpers";
import { store, toggleEthereal, type MiscOption, type Selections } from "./store";
import { ThumbImage } from "./ThumbImage";
import { getThumbUrl } from "./thumbnails";
import css from "./misc.module.css";

export function EtherealDialog({ opt, selections }: { opt: MiscOption; selections: Selections }) {
   const { t } = useLocale();
   const chosen = parseEthereal(selections[etherealKey(opt)]);
   const current = selectedChoiceId(opt, selections);
   const close = () => store.set({ etherealOpen: false });

   return (
      <div className={`${css.ov} ${css.etherealOverlayZ}`} role="dialog" aria-modal="true" aria-labelledby="etherealTitle">
         <div className={css.ovScrim} onClick={close} />
         <div className={css.mfDialog}>
            <div className={css.mfDialogHead}>
               <button type="button" data-no-drag className={css.mfBack} onClick={close} title={t("common.back", "Back")} aria-label={t("common.back", "Back")}>
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <path d="M10 19l-7-7m0 0l7-7m-7 7h18" />
                  </svg>
               </button>
               <div className={css.mfDialogThumb}>
                  <ThumbImage
                     src={getThumbUrl(opt, current)}
                     alt=""
                     letter={current.charAt(0).toUpperCase()}
                     imgClassName={css.dialogThumbImg}
                     placeholderClassName={css.cardLetter}
                  />
               </div>
               <div className={css.mfDialogTitles}>
                  <h3 id="etherealTitle" className={css.mfDialogTitle}>
                     <T k="miscForm.ethereal.title">Ethereal Effects</T>
                  </h3>
                  <p className={css.mfDialogSub}>
                     {t("miscForm.etherealSlot", `Select up to ${MAX_ETHEREAL_SLOTS} effects`, { max: MAX_ETHEREAL_SLOTS })} · {opt.name}: {current}
                  </p>
               </div>
            </div>
            <div className={css.etherealGrid}>
               {Object.keys(ETHEREAL_EFFECTS).map((name) => {
                  const isSelected = chosen.includes(name);
                  const isDisabled = !isSelected && chosen.length >= MAX_ETHEREAL_SLOTS;
                  return (
                     <button
                        type="button"
                        data-no-drag
                        key={name}
                        className={`${css.ethRow} ${isSelected ? css.selected : ""}`}
                        aria-pressed={isSelected}
                        disabled={isDisabled}
                        onClick={() => toggleEthereal(opt, name)}
                     >
                        <span className={css.ethBox}>
                           <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                              <path d="M5 13l4 4L19 7" />
                           </svg>
                        </span>
                        <span className={css.ethName} title={name}>
                           {name}
                        </span>
                     </button>
                  );
               })}
            </div>
            <div className={css.mfDialogFoot}>
               <span className={css.etherealCounter}>
                  {translate("miscForm.ethereal.counter", `${chosen.length}/${MAX_ETHEREAL_SLOTS} selected`, {
                     selected: chosen.length,
                     max: MAX_ETHEREAL_SLOTS,
                  })}
               </span>
               <button type="button" data-no-drag className={`${css.btn} ${css.primary}`} onClick={close}>
                  <T k="common.done">Done</T>
               </button>
            </div>
         </div>
      </div>
   );
}
