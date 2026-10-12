import { DOTA_ICON_PATHS, TORMENTOR_MASK, type DotaIconName } from "./dotaIcons";
import css from "./misc.module.css";

export function DotaIcon({ name, className }: { name: DotaIconName; className: string | undefined }) {
   if (name === "tormentor") {
      return <span className={`${css.maskIcon} ${className ?? ""}`} style={{ WebkitMaskImage: `url(${TORMENTOR_MASK})`, maskImage: `url(${TORMENTOR_MASK})` }} aria-hidden="true" />;
   }
   const icon: { d: string; evenOdd?: boolean } = DOTA_ICON_PATHS[name];
   return (
      <svg className={className} viewBox="0 0 24 24" aria-hidden="true">
         <path d={icon.d} fill="currentColor" fillRule={icon.evenOdd ? "evenodd" : undefined} clipRule={icon.evenOdd ? "evenodd" : undefined} />
      </svg>
   );
}
