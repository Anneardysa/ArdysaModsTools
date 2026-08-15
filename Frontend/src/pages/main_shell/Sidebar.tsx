import type { ReactNode } from "react";
import { send } from "../../bridge/host";
import { T, useLocale } from "../../bridge/i18n";
import { StatusBadge } from "./StatusBadge";
import { store } from "./store";
import type { ButtonKey } from "./types";
import { VerifyPanel } from "./VerifyPanel";
import css from "./shell.module.css";

function NavButton({
   id,
   buttonKey,
   messageType,
   icon,
   labelKey,
   labelFallback,
   highlight,
   extraClass,
   chevron,
   soonTag,
}: {
   id?: string;
   buttonKey?: ButtonKey;
   messageType?: string;
   icon: ReactNode;
   labelKey: string;
   labelFallback: string;
   highlight?: boolean;
   extraClass?: string;
   chevron?: boolean;
   soonTag?: boolean;
}) {
   const enabled = buttonKey ? store.use((s) => s.buttons[buttonKey] !== false) : true;

   if (soonTag) {
      return (
         <button type="button" id={id} className={`${css.navBtn} ${css.isDisabled}`} disabled>
            {icon}
            <span>
               <T k={labelKey}>{labelFallback}</T>
            </span>
            <span className={css.tagSoon}>
               <T k="shell.tag.soon">SOON</T>
            </span>
         </button>
      );
   }

   return (
      <button
         type="button"
         id={id}
         data-no-drag
         className={`${css.navBtn} ${extraClass ?? ""} ${!enabled ? css.isDisabled : ""} ${highlight ? css.highlight : ""}`}
         onClick={(e) => {
            e.stopPropagation();
            if (!enabled) return;
            if (messageType) send(messageType);
         }}
      >
         {icon}
         <span>
            <T k={labelKey}>{labelFallback}</T>
         </span>
         {chevron && (
            <svg className={css.chev} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
               <polyline points="6 9 12 15 18 9" />
            </svg>
         )}
      </button>
   );
}

export function Sidebar() {
   const { t } = useLocale();
   const buttons = store.use((s) => s.buttons);
   const patchButton = store.use((s) => s.patchButton);
   const playState = store.use((s) => s.playState);
   const dotaRunning = store.use((s) => s.dotaRunning);

   const highlight = !!buttons.detectHighlight;
   const patchClass = patchButton.isError ? css.error : patchButton.status === "ready" ? css.ready : patchButton.status === "needUpdate" ? css.update : "";

   return (
      <aside className={`${css.sidebar} notch`}>
         <div className={css.group}>
            <div className={css.groupLabel}>
               <T k="shell.group.detectPath">Detect Path</T>
            </div>
            <NavButton
               id="btn-autoDetect"
               buttonKey="autoDetect"
               messageType="autoDetect"
               highlight={highlight}
               labelKey="shell.nav.autoDetect"
               labelFallback="Auto Detect"
               icon={
                  <svg className={css.ico} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <circle cx="11" cy="11" r="7" />
                     <line x1="21" y1="21" x2="16.65" y2="16.65" />
                  </svg>
               }
            />
            <NavButton
               id="btn-manualDetect"
               buttonKey="manualDetect"
               messageType="manualDetect"
               highlight={highlight}
               labelKey="shell.nav.manualDetect"
               labelFallback="Manual Detect"
               icon={
                  <svg className={css.ico} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <path d="M3 7h5l2 2h11v9a2 2 0 0 1-2 2H3z" />
                  </svg>
               }
            />
         </div>

         <div className={css.group}>
            <div className={css.groupLabel}>
               <T k="shell.group.mods">Mods</T>
            </div>
            <NavButton
               id="btn-hero"
               buttonKey="hero"
               messageType="skinSelector"
               labelKey="shell.nav.skinSelector"
               labelFallback="Skin Selector"
               icon={
                  <svg className={css.ico} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <path d="M12 2l2.4 5 5.6.8-4 4 1 5.5L12 19.8 6.9 22.3l1-5.5-4-4 5.6-.8z" />
                  </svg>
               }
            />
            <NavButton
               id="btn-misc"
               buttonKey="misc"
               messageType="miscellaneous"
               labelKey="shell.nav.miscellaneous"
               labelFallback="Miscellaneous"
               icon={
                  <svg className={css.ico} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
                     <circle cx="5" cy="12" r="1.6" fill="currentColor" />
                     <circle cx="12" cy="12" r="1.6" fill="currentColor" />
                     <circle cx="19" cy="12" r="1.6" fill="currentColor" />
                  </svg>
               }
            />
         </div>

         <div className={css.group}>
            <div className={css.groupLabel}>
               <T k="shell.group.tools">Tools</T>
            </div>
            <NavButton
               id="btn-patch"
               buttonKey="patch"
               messageType="patchClick"
               extraClass={`${css.patch} ${patchClass}`}
               chevron
               labelKey="shell.nav.patchUpdate"
               labelFallback="Patch Update"
               icon={
                  <svg className={css.ico} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <path d="M21 12a9 9 0 1 1-3-6.7L21 8" />
                     <path d="M21 3v5h-5" />
                  </svg>
               }
            />
            <NavButton
               id="btn-disable"
               buttonKey="disable"
               messageType="disable"
               labelKey="shell.nav.disableMods"
               labelFallback="Disable Mods"
               icon={
                  <svg className={css.ico} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <circle cx="12" cy="12" r="9" />
                     <line x1="6" y1="6" x2="18" y2="18" />
                  </svg>
               }
            />
         </div>

         <div className={css.group}>
            <div className={css.groupLabel}>
               <T k="shell.group.extra">Extra</T>
            </div>
            <NavButton
               id="btn-tweak"
               buttonKey="tweak"
               messageType="tweak"
               labelKey="shell.nav.performanceTweak"
               labelFallback="Performance Tweak"
               icon={
                  <svg className={css.ico} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
                     <line x1="4" y1="8" x2="20" y2="8" />
                     <circle cx="9" cy="8" r="2.4" fill="currentColor" stroke="none" />
                     <line x1="4" y1="14" x2="20" y2="14" />
                     <circle cx="15" cy="14" r="2.4" fill="currentColor" stroke="none" />
                     <line x1="4" y1="20" x2="20" y2="20" />
                     <circle cx="11" cy="20" r="2.4" fill="currentColor" stroke="none" />
                  </svg>
               }
            />
            <NavButton
               id="btn-utilities"
               soonTag
               labelKey="shell.nav.utilities"
               labelFallback="Utilities"
               icon={
                  <svg className={css.ico} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <path d="M3 9h18v11a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1z" />
                     <path d="M8 9V6a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v3" />
                     <line x1="12" y1="13" x2="12" y2="17" />
                  </svg>
               }
            />
         </div>

         <div className={css.sbSpacer} />

         <button
            id="btn-play"
            type="button"
            data-no-drag
            className={`${css.playBtn} ${!playState.enabled ? css.isDisabled : ""}`}
            disabled={!playState.enabled}
            title={playState.reason}
            onClick={() => playState.enabled && send("playDota")}
         >
            <svg className={css.ico} viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
               <polygon points="7 4 20 12 7 20" />
            </svg>
            <span>{playState.label || t("play.button", "PLAY DOTA 2")}</span>
         </button>


         <VerifyPanel />

         <div className={css.statusCard}>
            <StatusFooterBadge />
            <button
               id="status-refresh"
               type="button"
               data-no-drag
               className={`${css.statusRefresh} ${dotaRunning ? css.isDisabled : ""}`}
               title={t("shell.status.refresh", "Refresh status")}
               // Guarded in JS as well as by the class's pointer-events, matching send()'s own
               // is-disabled refusal: a sweep cannot act on an install the game holds open.
               onClick={() => !dotaRunning && send("refreshStatus")}
            >
               <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <path d="M21 12a9 9 0 1 1-3-6.7L21 8" />
                  <path d="M21 3v5h-5" />
               </svg>
            </button>
         </div>

         <div className={css.group} style={{ gap: "var(--sp-2)" }}>
            <div className={css.footerRow}>
               <button type="button" data-no-drag className={css.social} title="Discord" onClick={() => send("openUrl", { url: "https://discord.gg/5xKg4fyumv" })}>
                  <img src="https://cdn.simpleicons.org/discord/8b949e" alt="Discord" />
               </button>
               <button type="button" data-no-drag className={css.social} title="YouTube" onClick={() => send("openUrl", { url: "https://youtube.com/@Ardysa" })}>
                  <img src="https://cdn.simpleicons.org/youtube/8b949e" alt="YouTube" />
               </button>
               <button type="button" data-no-drag className={`${css.social} ${css.support}`} title={t("shell.social.support", "Support the project")} onClick={() => send("support")}>
                  ♥ <T k="shell.social.supportBtn">SUPPORT</T>
               </button>
            </div>
            <div id="version" className={css.version}>
               {store.use((s) => s.version) || t("shell.version.checking", "Checking…")}
            </div>
         </div>
      </aside>
   );
}

function StatusFooterBadge() {
   const status = store.use((s) => s.status);
   const { t } = useLocale();
   return <StatusBadge id="status-badge" kind={status.kind} text={status.text || t("shell.status.notChecked", "Not Checked")} tooltip={status.tooltip} liveRegion />;
}
