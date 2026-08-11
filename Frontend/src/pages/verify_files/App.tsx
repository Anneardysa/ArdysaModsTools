import { send, useEscape } from "../../bridge/host";
import { T, useLocale } from "../../bridge/i18n";
import { WaveBackdrop, WindowCorners } from "../../ui/Backdrop";
import { Button } from "../../ui/Button";
import { CHECK_COUNT, store, type CheckState } from "./store";
import css from "./verify.module.css";


const CHECKS = [
   { nameKey: "verifyFiles.check.package", name: "Mod Package", hintKey: "verifyFiles.check.packageHint", hint: "VPK file integrity" },
   { nameKey: "verifyFiles.check.version", name: "Dota Version", hintKey: "verifyFiles.check.versionHint", hint: "Patched version match" },
   { nameKey: "verifyFiles.check.patch", name: "Game Patch", hintKey: "verifyFiles.check.patchHint", hint: "Binary compatibility" },
   { nameKey: "verifyFiles.check.integration", name: "Mod Integration", hintKey: "verifyFiles.check.integrationHint", hint: "GameInfo + signatures" },
];

const BADGE: Record<CheckState, { key: string; fallback: string; cls?: string }> = {
   wait: { key: "verifyFiles.badge.wait", fallback: "WAIT" },
   checking: { key: "", fallback: "...", cls: css.checking },
   pass: { key: "verify.state.pass", fallback: "OK", cls: css.pass },
   fail: { key: "verifyFiles.badge.fail", fallback: "FAIL", cls: css.fail },
};

const FileIcon = () => (
   <>
      <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
      <polyline points="14 2 14 8 20 8" />
      <line x1="16" y1="13" x2="8" y2="13" />
      <line x1="16" y1="17" x2="8" y2="17" />
      <polyline points="10 9 9 9 8 9" />
   </>
);
const CheckIcon = () => <polyline points="20 6 9 17 4 12" />;
const WarnIcon = () => (
   <>
      <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
      <line x1="12" y1="9" x2="12" y2="13" />
      <line x1="12" y1="17" x2="12.01" y2="17" />
   </>
);

export function App() {
   const { t, tp } = useLocale();
   const checks = store.use((s) => s.checks);
   const visible = store.use((s) => s.visible);
   const summary = store.use((s) => s.summary);

   useEscape(() => send("close"));

   const settled = checks.filter((c) => c.state === "pass" || c.state === "fail").length;
   const allPass = summary ? summary.passed === summary.total : false;
   const outstanding = summary ? summary.total - summary.passed : 0;

   const barState = !summary
      ? ""
      : allPass
        ? css.doneOk
        : summary.passed > 0
          ? css.doneWarn
          : css.doneFail;
   const verdict = !summary ? "" : allPass ? css.ok : css.warn;

   return (
      <>
         <WaveBackdrop />
         <WindowCorners />
         <div className={css.dragArea} onMouseDown={() => send("startDrag")} />

         <div className={css.container}>
            <div className={`${css.headerIcon} ${verdict} ${css.fadeIn}`}>
               <svg
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  aria-hidden="true"
               >
                  {!summary ? <FileIcon /> : allPass ? <CheckIcon /> : <WarnIcon />}
               </svg>
            </div>

            <h1 className={`${css.title} ${verdict} ${css.fadeIn}`}>
               {!summary ? (
                  <T k="verifyFiles.title">Verifying</T>
               ) : allPass ? (
                  <T k="verifyFiles.allClear">All Clear</T>
               ) : (
                  <T k="verifyFiles.issuesFound">Issues Found</T>
               )}
            </h1>

            <div className={`${css.subtitle} ${css.fadeIn}`}>
               {!summary ? (
                  <T k="verifyFiles.subtitle">scanning mod files</T>
               ) : allPass ? (
                  <T k="verifyFiles.allVerified">all files verified successfully</T>
               ) : (
                  tp(
                     "verifyFiles.needAttention",
                     outstanding,
                     `${outstanding} issue${outstanding === 1 ? "" : "s"} require attention`,
                  )
               )}
            </div>

            <div
               className={`${css.progressTrack} ${css.fadeIn}`}
               role="progressbar"
               aria-valuemin={0}
               aria-valuemax={CHECK_COUNT}
               aria-valuenow={settled}
            >
               <div
                  className={`${css.progressBar} ${barState}`}
                  style={{ width: `${(settled / CHECK_COUNT) * 100}%` }}
               />
            </div>

            <div className={css.checks}>
               {CHECKS.map((meta, i) => {
                  const check = checks[i]!;
                  const badge = BADGE[check.state];
                  return (
                     <div
                        key={meta.nameKey}
                        className={[
                           css.row,
                           visible[i] ? css.visible : "",
                           check.state === "checking" ? css.checking : "",
                           check.state === "pass" ? css.pass : "",
                           check.state === "fail" ? css.fail : "",
                        ]
                           .filter(Boolean)
                           .join(" ")}
                     >
                        <div
                           className={[
                              css.dot,
                              check.state === "checking" ? css.spinning : "",
                              check.state === "pass" ? css.pass : "",
                              check.state === "fail" ? css.fail : "",
                           ]
                              .filter(Boolean)
                              .join(" ")}
                        />
                        <div className={css.info}>
                           <div className={css.name}>
                              <T k={meta.nameKey}>{meta.name}</T>
                           </div>
                           <div
                              className={[
                                 css.detail,
                                 check.state === "pass" ? css.pass : "",
                                 check.state === "fail" ? css.fail : "",
                              ]
                                 .filter(Boolean)
                                 .join(" ")}
                           >
                              {check.detail ?? t(meta.hintKey, meta.hint)}
                           </div>
                        </div>
                        <span className={`${css.badge} ${badge.cls ?? ""}`}>
                           {badge.key ? t(badge.key, badge.fallback) : badge.fallback}
                        </span>
                     </div>
                  );
               })}
            </div>

            <div
               className={[css.summary, summary ? (allPass ? css.allPass : css.hasIssues) : ""]
                  .filter(Boolean)
                  .join(" ")}
               aria-live="polite"
            >
               {summary
                  ? t("verifyFiles.summary", `${summary.passed}/${summary.total} checks passed`, {
                       passed: summary.passed,
                       total: summary.total,
                    })
                  : ""}
            </div>

            <div className={`${css.actions} ${css.fadeIn}`}>
               {summary?.showPatch && (
                  <Button variant="primary" onClick={() => send("patchNow")}>
                     <T k="status.action.patchUpdate">Patch Update</T>
                  </Button>
               )}
               <Button variant="ghost" onClick={() => send("close")}>
                  <T k="common.close">Close</T>
               </Button>
            </div>
         </div>
      </>
   );
}
