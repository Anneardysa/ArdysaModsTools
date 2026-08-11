import type { ReactNode } from "react";
import { send, useEscape } from "../../bridge/host";
import { T, useLocale } from "../../bridge/i18n";
import { Button } from "../../ui/Button";
import { Footer } from "../../ui/Footer";
import { Titlebar } from "../../ui/Titlebar";
import { WaveBackdrop } from "../../ui/Backdrop";
import { store } from "./store";
import css from "./status.module.css";


type Verdict = "ready" | "needUpdate" | "disabled" | "error" | "notInstalled";

function verdictOf(status: string | undefined): Verdict {
   switch (status) {
      case "Ready":
         return "ready";
      case "NeedUpdate":
         return "needUpdate";
      case "Disabled":
         return "disabled";
      case "Error":
         return "error";
      default:
         return "notInstalled";
   }
}

const ICONS: Record<Verdict, ReactNode> = {
   ready: <polyline points="20 6 9 17 4 12" />,
   needUpdate: (
      <>
         <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
         <line x1="12" y1="9" x2="12" y2="13" />
         <line x1="12" y1="17" x2="12.01" y2="17" />
      </>
   ),
   disabled: (
      <>
         <circle cx="12" cy="12" r="10" />
         <line x1="4.93" y1="4.93" x2="19.07" y2="19.07" />
      </>
   ),
   error: (
      <>
         <circle cx="12" cy="12" r="10" />
         <line x1="15" y1="9" x2="9" y2="15" />
         <line x1="9" y1="9" x2="15" y2="15" />
      </>
   ),
   notInstalled: (
      <>
         <circle cx="12" cy="12" r="10" />
         <line x1="8" y1="12" x2="16" y2="12" />
      </>
   ),
};

export function App() {
   const { t } = useLocale();
   const data = store.use((s) => s.data);

   useEscape(() => send("close"));

   const d = data ?? {};
   const verdict = data ? verdictOf(d.status) : null;

   const decided = [d.digestOk, d.gameInfoOk, d.adminOk].filter((v) => v !== null && v !== undefined);
   const allOk = decided.length > 0 && decided.every((v) => v === true);
   const overall = decided.length === 0 ? null : allOk;

   const summaryAdvisory = Boolean(d.adminAdvisory) && allOk;

   const CheckRow = ({
      state,
      labelKey,
      label,
      advisory,
      note,
      summary,
   }: {
      state: boolean | null | undefined;
      labelKey: string;
      label: string;
      advisory?: boolean;
      note?: string | null;
      summary?: boolean;
   }) => {
      const isAdvisory = Boolean(advisory) && Boolean(d.adminStateText);
      const dotClass = isAdvisory
         ? css.dotWarn
         : state === true
           ? css.dotPass
           : state === false
             ? css.dotFail
             : "";
      const textClass = isAdvisory
         ? css.statusWarn
         : state === true
           ? css.statusPass
           : state === false
             ? css.statusFail
             : "";
      const text = isAdvisory
         ? d.adminStateText!
         : state === true
           ? t("verify.state.pass", "OK")
           : state === false
             ? t("verify.state.fail", "Action needed")
             : "--";

      return (
         <div className={`${css.checkRow} ${summary ? css.summaryRow : ""}`}>
            <div className={`${css.dot} ${dotClass}`} />
            <span className={css.checkLabel}>
               <T k={labelKey}>{label}</T>
            </span>
            <span className={`${css.checkStatus} ${textClass}`} title={note ?? undefined}>
               {text}
            </span>
         </div>
      );
   };

   const Row = ({
      labelKey,
      label,
      value,
      highlight,
      mismatch,
   }: {
      labelKey: string;
      label: string;
      value?: string;
      highlight?: boolean;
      mismatch?: boolean;
   }) => (
      <div className={css.kv}>
         <span className={css.kvKey}>
            <T k={labelKey}>{label}</T>
         </span>
         <span
            className={[css.kvVal, highlight ? css.highlight : "", mismatch ? css.mismatch : ""]
               .filter(Boolean)
               .join(" ")}
         >
            {value || "--"}
         </span>
      </div>
   );

   return (
      <>
         <WaveBackdrop />
         <Titlebar titleKey="statusDetails.title" title="Status Details" />

         <div className={css.content}>
            <div className={`${css.hero} ${css.fadeIn} panel`}>
               <div className={`${css.heroIco} ${verdict ? css[verdict] ?? "" : ""}`}>
                  <svg
                     viewBox="0 0 24 24"
                     fill="none"
                     stroke="currentColor"
                     strokeWidth="2"
                     strokeLinecap="round"
                     strokeLinejoin="round"
                     aria-hidden="true"
                  >
                     {ICONS[verdict ?? "notInstalled"]}
                  </svg>
               </div>
               <div className={css.heroText}>
                  <div className={css.heroStatus}>
                     {d.statusText || t("status.checking.text", "Checking")}
                  </div>
                  <div className={css.heroDesc}>
                     {d.description || t("status.checking.desc", "Detecting changes, please wait...")}
                  </div>
               </div>
            </div>

            <div className={`${css.fadeIn} ${css.d1}`}>
               <div className={css.sectionLabel}>
                  <T k="statusDetails.versionInfo">Version Info</T>
               </div>
               <div className="panel">
                  <Row labelKey="statusDetails.dota" label="Dota" value={d.dotaVersion} highlight />
                  <Row labelKey="statusDetails.build" label="Build" value={d.buildNumber} />
                  <Row
                     labelKey="statusDetails.patched"
                     label="Patched"
                     value={d.patchedVersion}
                     mismatch={d.versionMismatch}
                  />
                  <Row
                     labelKey="statusDetails.lastPatch"
                     label="Last Patch"
                     value={d.patchDate || (data ? t("statusDetails.never", "Never") : undefined)}
                  />
               </div>
            </div>

            <div className={`${css.fadeIn} ${css.d2}`}>
               <div className={css.sectionLabel}>
                  <T k="statusDetails.diagnostics">Diagnostics</T>
               </div>
               <div className="panel">
                  <CheckRow state={d.digestOk} labelKey="statusDetails.gamePatch" label="Patch Integrity" />
                  <CheckRow
                     state={d.gameInfoOk}
                     labelKey="statusDetails.modIntegration"
                     label="Search Paths"
                  />
                  <CheckRow
                     state={d.adminOk}
                     labelKey="statusDetails.runAsAdmin"
                     label="Process Elevation"
                     advisory={d.adminAdvisory}
                     note={d.adminNote}
                  />
                  <CheckRow
                     state={overall}
                     labelKey="statusDetails.overallStatus"
                     label="Overall Status"
                     advisory={summaryAdvisory}
                     note={summaryAdvisory ? d.adminNote : null}
                     summary
                  />
               </div>
            </div>

            {d.verifyDetail && (
               <div className={`${css.note} ${css.warn} ${css.fadeIn} ${css.d3}`} title={d.verifyDetail}>
                  <span className={css.noteLabel}>
                     <T k="common.warning">Warning</T>
                  </span>
                  <span className={css.noteText}>{d.verifyDetail}</span>
               </div>
            )}

            {d.errorMessage && (
               <div className={`${css.note} ${css.bad} ${css.fadeIn} ${css.d3}`} title={d.errorMessage}>
                  <span className={css.noteLabel}>
                     <T k="common.error">Error</T>
                  </span>
                  <span className={css.noteText}>{d.errorMessage}</span>
               </div>
            )}
         </div>

         <Footer layout="end" className={css.footer}>
            {d.showPatchBtn && (
               <Button variant="primary" onClick={() => send("patchNow")}>
                  {d.patchBtnText || t("statusDetails.applyPatches", "Apply Patches")}
               </Button>
            )}
            <Button variant="ghost" onClick={() => send("close")}>
               <T k="common.close">Close</T>
            </Button>
         </Footer>
      </>
   );
}
