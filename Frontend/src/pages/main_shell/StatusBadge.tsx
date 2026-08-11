import type { BadgeKind } from "./types";
import css from "./shell.module.css";

const SB_ICON_PATHS: Record<BadgeKind, string> = {
   neutral: '<circle cx="12" cy="12" r="9" />',
   info: '<circle cx="12" cy="12" r="9" /><path d="M12 16v-4" /><path d="M12 8h.01" />',
   success: '<path d="M20 6 9 17l-5-5" />',
   warning:
      '<path d="m21.7 18-8-14a2 2 0 0 0-3.4 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.7-3" /><path d="M12 9v4" /><path d="M12 17h.01" />',
   danger: '<path d="M18 6 6 18" /><path d="m6 6 12 12" />',
   loading: '<path d="M21 12a9 9 0 1 1-6.2-8.6" />',
};

export function StatusBadge({
   id,
   kind,
   text,
   tooltip,
   small,
   showLabel = true,
   liveRegion,
}: {
   id?: string;
   kind: BadgeKind;
   text?: string | null;
   tooltip?: string;
   small?: boolean;
   showLabel?: boolean;
   liveRegion?: boolean;
}) {
   return (
      <span
         id={id}
         className={`${css.statusBadge} ${small ? css.sbSm : ""}`}
         data-kind={kind}
         title={tooltip}
         role={liveRegion ? "status" : undefined}
         aria-live={liveRegion ? "polite" : undefined}
      >
         <span className={css.sbPulse} aria-hidden="true" />
         <span key={kind} className={`${css.sbIcon} ${css.sbIn}`} aria-hidden="true">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" dangerouslySetInnerHTML={{ __html: SB_ICON_PATHS[kind] }} />
         </span>
         {showLabel && (
            <span key={text ?? ""} className={`${css.sbLabel} ${css.sbIn}`}>
               <span>{text}</span>
            </span>
         )}
      </span>
   );
}
