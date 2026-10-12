import { useLocale } from "../../bridge/i18n";
import { DotaIcon } from "./DotaIcon";
import { iconForOption } from "./dotaIcons";
import { isCustomized, navGroups } from "./helpers";
import { setActiveOption, type MiscOption, type Selections } from "./store";
import css from "./misc.module.css";

export function Sidebar({
   options,
   selections,
   activeId,
   flashResetId,
}: {
   options: MiscOption[];
   selections: Selections;
   activeId: string | undefined;
   flashResetId: string | null;
}) {
   const { t } = useLocale();
   const changedLabel = t("miscForm.changedMark", "changed");

   return (
      <nav className={css.side} aria-label={t("miscForm.navLabel", "Option categories")}>
         {navGroups(options).map((g) => (
            <section key={g.category} className={css.sideGroup}>
               <h2 className={css.sideHead}>{g.category}</h2>
               {g.options.map((o) => {
                  const active = o.id === activeId;
                  const changed = isCustomized(o, selections);
                  return (
                     <button
                        type="button"
                        data-no-drag
                        key={o.id}
                        className={[css.sideItem, active ? css.active : "", flashResetId === o.id ? css.flashReset : ""].filter(Boolean).join(" ")}
                        aria-current={active ? "page" : undefined}
                        onClick={() => setActiveOption(o.id)}
                     >
                        <DotaIcon name={iconForOption(o.id)} className={css.sideIco} />
                        <span className={css.sideLabel}>{o.name}</span>
                        {changed && (
                           <span className={css.sideMark} title={changedLabel}>
                              <span className={css.srOnly}>{changedLabel}</span>
                           </span>
                        )}
                     </button>
                  );
               })}
            </section>
         ))}
      </nav>
   );
}
