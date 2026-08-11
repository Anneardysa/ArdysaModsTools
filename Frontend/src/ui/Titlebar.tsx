import type { ReactNode } from "react";
import { send, startDragUnlessInteractive } from "../bridge/host";
import { useLocale } from "../bridge/i18n";
import { BrandGlyph } from "./BrandMark";
import css from "./Titlebar.module.css";

export function Titlebar({
   titleKey,
   title,
   icon,
   badge,
   minimize = false,
   closeVisible = true,
   closeLabelKey = "common.close",
   closeLabel = "Close",
   onClose,
   children,
}: {
   titleKey: string;
   title: string;
   icon?: ReactNode;
   badge?: ReactNode;
   minimize?: boolean;
   closeVisible?: boolean;
   closeLabelKey?: string;
   closeLabel?: string;
   onClose?: () => void;
   children?: ReactNode;
}) {
   const { t } = useLocale();

   return (
      <header className={css.bar} onMouseDown={startDragUnlessInteractive}>
         <div className={css.brand}>
            <span className={css.glyph}>{icon ?? <BrandGlyph height={16} />}</span>
            <span className={css.name}>{t(titleKey, title)}</span>
            {badge != null && <span className={css.badge}>{badge}</span>}
         </div>

         <div className={css.controls}>
            {children}
            {minimize && (
               <TitlebarButton
                  labelKey="shell.titlebar.minimize"
                  label="Minimize"
                  onClick={() => send("minimize")}
               >
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
                     <line x1="5" y1="12" x2="19" y2="12" />
                  </svg>
               </TitlebarButton>
            )}
            {closeVisible && (
               <TitlebarButton
                  labelKey={closeLabelKey}
                  label={closeLabel}
                  className={css.close}
                  onClick={onClose ?? (() => send("close"))}
               >
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
                     <line x1="6" y1="6" x2="18" y2="18" />
                     <line x1="18" y1="6" x2="6" y2="18" />
                  </svg>
               </TitlebarButton>
            )}
         </div>
      </header>
   );
}

export function TitlebarButton({
   labelKey,
   label,
   onClick,
   className,
   children,
}: {
   labelKey: string;
   label: string;
   onClick: () => void;
   className?: string;
   children: ReactNode;
}) {
   const { t } = useLocale();
   const text = t(labelKey, label);

   return (
      <button
         type="button"
         data-no-drag
         className={className ? `${css.btn} ${className}` : css.btn}
         title={text}
         aria-label={text}
         onClick={onClick}
      >
         {children}
      </button>
   );
}
