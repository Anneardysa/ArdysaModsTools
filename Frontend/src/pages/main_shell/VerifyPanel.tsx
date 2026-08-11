import { send } from "../../bridge/host";
import { StatusBadge } from "./StatusBadge";
import { openElevationModal, openSyncModal, store } from "./store";
import type { BadgeKind, SetupCheckState } from "./types";
import css from "./shell.module.css";

const VERIFY_KIND: Record<SetupCheckState, BadgeKind> = { pass: "success", fail: "danger", advisory: "warning" };

export function VerifyPanel() {
   const checks = store.use((s) => s.verifyChecks);
   const dotaRunning = store.use((s) => s.dotaRunning);

   if (!checks || checks.checks.length === 0) return null;

   const failed = checks.checks.some((c) => c.state === "fail");
   const advisory = !failed && checks.checks.some((c) => c.state === "advisory");
   const headKind: BadgeKind = failed ? "danger" : advisory ? "warning" : "success";

   return (
      <div id="verify-panel" className={`${css.verifyPanel} ${css.show} ${dotaRunning ? css.isLocked : ""}`}>
         <div className={css.verifyHead}>
            <span className={css.vheadLabel}>{checks.title || "Setup Verification"}</span>
            <StatusBadge kind={headKind} showLabel={false} small />
         </div>
         {checks.checks.map((c) => (
            <button
               key={c.id}
               type="button"
               data-no-drag
               className={css.verifyItem}
               title={c.detail || ""}
               onClick={() => {
                  if (!c.hasOwnDialog) send("patchViewStatus");
                  else if (c.id === "ItemsGameInSync") openSyncModal();
                  else {
                     openElevationModal();
                     send("refreshStatus");
                  }
               }}
            >
               <span className={css.vlabel}>{c.label}</span>
               <StatusBadge kind={VERIFY_KIND[c.state] || "neutral"} text={c.stateText} tooltip={c.detail} small />
            </button>
         ))}
      </div>
   );
}
