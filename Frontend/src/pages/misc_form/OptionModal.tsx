import { useEffect, useRef, type ReactNode } from "react";
import { T, translate, useLocale } from "../../bridge/i18n";
import { ETHEREAL_EFFECTS } from "./ethereal-effects";
import type { Choice, MiscOption } from "./store";
import { ThumbImage } from "./ThumbImage";
import { getThumbUrl } from "./thumbnails";
import css from "./misc.module.css";

const MAX_ETHEREAL_SLOTS = 2;

function ChoiceCard({
   thumbUrl,
   name,
   selected,
   wrapName,
   onSelect,
   badges,
}: {
   thumbUrl: string | null;
   name: string;
   selected: boolean;
   wrapName?: boolean;
   onSelect: () => void;
   badges?: ReactNode;
}) {
   return (
      <div className={css.choiceCard}>
         {badges}
         <button
            type="button"
            data-no-drag
            className={`${css.choiceCardBtn} ${selected ? css.selected : ""}`}
            onClick={onSelect}
         >
            <ThumbImage
               src={thumbUrl}
               alt=""
               letter={name.charAt(0).toUpperCase()}
               imgClassName={css.choiceThumb}
               placeholderClassName={css.choiceLetter}
            />
            <div className={`${css.choiceName} ${wrapName ? css.wrap : ""}`} title={wrapName ? name : undefined}>
               {name}
            </div>
         </button>
      </div>
   );
}

export function StyleOverlay({
   opt,
   choice,
   currentSelection,
   onSelectStyle,
   onClose,
}: {
   opt: MiscOption;
   choice: Choice;
   currentSelection: string | undefined;
   onSelectStyle: (styleId: string) => void;
   onClose: () => void;
}) {
   const { t } = useLocale();

   return (
      <div className={`${css.ov} ${css.styleOverlayZ}`}>
         <div className={css.ovScrim} onClick={onClose} />
         <div className={css.mfDialog}>
            <div className={css.mfDialogHead}>
               <button type="button" data-no-drag className={css.mfBack} onClick={onClose} title={t("common.back", "Back")} aria-label={t("common.back", "Back")}>
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <path d="M10 19l-7-7m0 0l7-7m-7 7h18" />
                  </svg>
               </button>
               <div className={css.mfDialogThumb}>
                  <ThumbImage
                     src={getThumbUrl(opt, choice.id)}
                     alt=""
                     letter={choice.name.charAt(0).toUpperCase()}
                     imgClassName={css.dialogThumbImg}
                     placeholderClassName={css.choiceLetter}
                  />
               </div>
               <div className={css.mfDialogTitles}>
                  <h3 className={css.mfDialogTitle}>{choice.name}</h3>
                  <p className={css.mfDialogSub}>
                     <T k="miscForm.selectStyle">Select a style</T>
                  </p>
               </div>
            </div>
            <div className={css.choicesGrid}>
               {(choice.styles ?? []).map((s) => (
                  <ChoiceCard
                     key={s.id}
                     thumbUrl={getThumbUrl(opt, s.id)}
                     name={s.name}
                     selected={s.id === currentSelection}
                     wrapName
                     onSelect={() => onSelectStyle(s.id)}
                  />
               ))}
            </div>
            <div className={css.mfDialogFoot} style={{ justifyContent: "flex-end" }}>
               <button type="button" data-no-drag className={`${css.btn} ${css.primary}`} onClick={onClose}>
                  <T k="common.done">Done</T>
               </button>
            </div>
         </div>
      </div>
   );
}

export function EtherealOverlay({
   opt,
   choiceId,
   selections,
   onToggle,
   onClose,
}: {
   opt: MiscOption;
   choiceId: string;
   selections: string[];
   onToggle: (name: string) => void;
   onClose: () => void;
}) {
   const { t } = useLocale();
   const selectedCount = selections.length;

   return (
      <div className={`${css.ov} ${css.etherealOverlayZ}`}>
         <div className={css.ovScrim} onClick={onClose} />
         <div className={css.mfDialog}>
            <div className={css.mfDialogHead}>
               <button type="button" data-no-drag className={css.mfBack} onClick={onClose} title={t("common.back", "Back")} aria-label={t("common.back", "Back")}>
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <path d="M10 19l-7-7m0 0l7-7m-7 7h18" />
                  </svg>
               </button>
               <div className={css.mfDialogThumb}>
                  <ThumbImage
                     src={getThumbUrl(opt, choiceId)}
                     alt=""
                     letter={(choiceId || "E").charAt(0).toUpperCase()}
                     imgClassName={css.dialogThumbImg}
                     placeholderClassName={css.choiceLetter}
                  />
               </div>
               <div className={css.mfDialogTitles}>
                  <h3 className={css.mfDialogTitle}>
                     <T k="miscForm.ethereal.title">✦ Ethereal Effects</T>
                  </h3>
                  <p className={css.mfDialogSub}>{t("miscForm.etherealSlot", `Select up to ${MAX_ETHEREAL_SLOTS} effects`, { max: MAX_ETHEREAL_SLOTS })}</p>
               </div>
            </div>
            <div className={css.etherealGrid}>
               {Object.keys(ETHEREAL_EFFECTS).map((name) => {
                  const isSelected = selections.includes(name);
                  const isDisabled = !isSelected && selectedCount >= MAX_ETHEREAL_SLOTS;
                  return (
                     <div
                        key={name}
                        className={`${css.ethRow} ${isSelected ? css.selected : ""} ${isDisabled ? css.disabled : ""}`}
                        onClick={!isDisabled || isSelected ? () => onToggle(name) : undefined}
                     >
                        <div className={css.ethBox}>
                           <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                              <path d="M5 13l4 4L19 7" />
                           </svg>
                        </div>
                        <span className={css.ethName} title={name}>
                           {name}
                        </span>
                     </div>
                  );
               })}
            </div>
            <div className={css.mfDialogFoot}>
               <span className={css.etherealCounter}>
                  {translate("miscForm.ethereal.counter", `${selectedCount}/${MAX_ETHEREAL_SLOTS} selected`, {
                     selected: selectedCount,
                     max: MAX_ETHEREAL_SLOTS,
                  })}
               </span>
               <button type="button" data-no-drag className={`${css.btn} ${css.primary}`} onClick={onClose}>
                  <T k="common.done">Done</T>
               </button>
            </div>
         </div>
      </div>
   );
}

export function OptionModal({
   options,
   index,
   selections,
   styleOverlay,
   etherealOverlay,
   etherealSelections,
   onNavigate,
   onClose,
   onSelectBaseChoice,
   onCloseStyleOverlay,
   onSelectStyle,
   onOpenEtherealOverlay,
   onCloseEtherealOverlay,
   onToggleEthereal,
}: {
   options: MiscOption[];
   index: number;
   selections: Record<string, string>;
   styleOverlay: { optId: string; choice: Choice } | null;
   etherealOverlay: { optId: string; choiceId: string } | null;
   etherealSelections: string[];
   onNavigate: (dir: number) => void;
   onClose: () => void;
   onSelectBaseChoice: (optId: string, choice: Choice) => void;
   onCloseStyleOverlay: () => void;
   onSelectStyle: (styleId: string) => void;
   onOpenEtherealOverlay: (optId: string, choiceId: string) => void;
   onCloseEtherealOverlay: () => void;
   onToggleEthereal: (name: string) => void;
}) {
   const { t } = useLocale();
   const opt = options[index];
   const scrollRef = useRef<HTMLDivElement>(null);

   useEffect(() => {
      const onKeyDown = (e: KeyboardEvent) => {
         if (styleOverlay) {
            if (e.key === "Escape") {
               onCloseStyleOverlay();
               e.preventDefault();
            }
            return;
         }
         if (e.key === "ArrowLeft") {
            onNavigate(-1);
            e.preventDefault();
         } else if (e.key === "ArrowRight") {
            onNavigate(1);
            e.preventDefault();
         } else if (e.key === "Escape") {
            onClose();
            e.preventDefault();
         }
      };
      document.addEventListener("keydown", onKeyDown);
      return () => document.removeEventListener("keydown", onKeyDown);
   }, [styleOverlay, onNavigate, onClose, onCloseStyleOverlay]);

   if (!opt) return null;

   const currentSelection = selections[opt.id] || opt.choices?.[0]?.id || "default";
   const headerThumbUrl = getThumbUrl(opt, currentSelection);
   const isCourier = opt.id?.toLowerCase() === "courier";
   const showArrows = options.length > 1;

   useEffect(() => {
      const el = scrollRef.current?.querySelector(`.${css.selected}`);
      el?.scrollIntoView({ block: "center", behavior: "smooth" });
   }, [opt.id, currentSelection]);

   return (
      <>
         <div className={`${css.ov} ${css.optionModalZ}`}>
            <div className={css.ovScrim} onClick={onClose} />
            <div className={css.smWrap}>
               <button
                  type="button"
                  data-no-drag
                  className={css.navArrow}
                  style={{ visibility: showArrows ? "visible" : "hidden" }}
                  onClick={() => onNavigate(-1)}
                  title={t("shell.carousel.prev", "Previous")}
                  aria-label={t("shell.carousel.prev", "Previous")}
               >
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <path d="M15 19l-7-7 7-7" />
                  </svg>
               </button>

               <div className={`${css.smPanel} ${css.animateBounceIn}`}>
                  <div className={css.smHead}>
                     <div className={css.smHeadLeft}>
                        <div className={css.mfModalThumb}>
                           <ThumbImage
                              src={headerThumbUrl}
                              alt=""
                              letter={opt.name.charAt(0).toUpperCase()}
                              imgClassName={css.dialogThumbImg}
                              placeholderClassName={css.choiceLetter}
                           />
                        </div>
                        <div>
                           <h2 className={css.modalTitle}>{opt.name}</h2>
                           <p className={css.smHint}>
                              <T k="miscForm.optionHint">← → browse options · click to select</T>
                           </p>
                        </div>
                     </div>
                     <button type="button" data-no-drag className={css.smX} onClick={onClose} title={t("common.close", "Close")} aria-label={t("common.close", "Close")}>
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                           <path d="M6 18L18 6M6 6l12 12" />
                        </svg>
                     </button>
                  </div>

                  <div ref={scrollRef} className={`${css.choicesGrid} ${css.modalChoices}`}>
                     {(opt.choices ?? []).map((c) => {
                        const isDirectlySelected = c.id === currentSelection;
                        const hasSelectedStyle = c.styles?.some((s) => s.id === currentSelection);
                        const hasStyles = !!c.styles && c.styles.length > 0;
                        return (
                           <ChoiceCard
                              key={c.id}
                              thumbUrl={getThumbUrl(opt, c.id)}
                              name={c.name}
                              selected={isDirectlySelected || !!hasSelectedStyle}
                              onSelect={() => onSelectBaseChoice(opt.id, c)}
                              badges={
                                 <>
                                    {hasStyles && <span className={`${css.choiceBadge} ${css.stylesBadge}`}>Styles</span>}
                                    {isCourier && (
                                       <button
                                          type="button"
                                          data-no-drag
                                          className={`${css.choiceBadge} ${css.etherealBadge}`}
                                          onClick={(e) => {
                                             e.stopPropagation();
                                             onOpenEtherealOverlay(opt.id, c.id);
                                          }}
                                       >
                                          Ethereal
                                       </button>
                                    )}
                                 </>
                              }
                           />
                        );
                     })}
                  </div>

                  <div className={css.smFoot}>
                     <button type="button" data-no-drag className={`${css.btn} ${css.primary}`} onClick={onClose}>
                        <T k="common.done">Done</T>
                     </button>
                  </div>
               </div>

               <button
                  type="button"
                  data-no-drag
                  className={css.navArrow}
                  style={{ visibility: showArrows ? "visible" : "hidden" }}
                  onClick={() => onNavigate(1)}
                  title={t("shell.carousel.next", "Next")}
                  aria-label={t("shell.carousel.next", "Next")}
               >
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <path d="M9 5l7 7-7 7" />
                  </svg>
               </button>
            </div>
         </div>

         {styleOverlay && (
            <StyleOverlay
               opt={opt}
               choice={styleOverlay.choice}
               currentSelection={selections[styleOverlay.optId]}
               onSelectStyle={onSelectStyle}
               onClose={onCloseStyleOverlay}
            />
         )}

         {etherealOverlay && (
            <EtherealOverlay
               opt={opt}
               choiceId={etherealOverlay.choiceId}
               selections={etherealSelections}
               onToggle={onToggleEthereal}
               onClose={onCloseEtherealOverlay}
            />
         )}
      </>
   );
}
