import { useEffect, type ReactNode } from "react";
import { send } from "../../bridge/host";
import { T } from "../../bridge/i18n";
import { Button } from "../../ui/Button";
import { Footer } from "../../ui/Footer";
import { Titlebar } from "../../ui/Titlebar";
import { store } from "./store";
import css from "./update.module.css";


const CdnIcon = () => (
   <path
      strokeLinecap="round"
      strokeLinejoin="round"
      strokeWidth="2"
      d="M3 15a4 4 0 004 4h9a5 5 0 10-.1-9.999 5.002 5.002 0 10-9.78 2.096A4.001 4.001 0 003 15z"
   />
);
const GitHubIcon = () => (
   <path d="M12 0c-6.626 0-12 5.373-12 12 0 5.302 3.438 9.8 8.207 11.387.599.111.793-.261.793-.577v-2.234c-3.338.726-4.033-1.416-4.033-1.416-.546-1.387-1.333-1.756-1.333-1.756-1.089-.745.083-.729.083-.729 1.205.084 1.839 1.237 1.839 1.237 1.07 1.834 2.807 1.304 3.492.997.107-.775.418-1.305.762-1.604-2.665-.305-5.467-1.334-5.467-5.931 0-1.311.469-2.381 1.236-3.221-.124-.303-.535-1.524.117-3.176 0 0 1.008-.322 3.301 1.23.957-.266 1.983-.399 3.003-.404 1.02.005 2.047.138 3.006.404 2.291-1.552 3.297-1.23 3.297-1.23.653 1.653.242 2.874.118 3.176.77.84 1.235 1.911 1.235 3.221 0 4.609-2.807 5.624-5.479 5.921.43.372.823 1.102.823 2.222v3.293c0 .319.192.694.801.576 4.765-1.589 8.199-6.086 8.199-11.386 0-6.627-5.373-12-12-12z" />
);
const ArrowIcon = () => (
   <>
      <path d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4" />
      <path d="M14 4h6m0 0v6m0-6L10 14" />
   </>
);

function VersionRow({
   labelKey,
   label,
   value,
   latest,
}: {
   labelKey: string;
   label: string;
   value?: string;
   latest?: boolean;
}) {
   return (
      <div className={css.versionRow}>
         <span className={css.versionLabel}>
            <T k={labelKey}>{label}</T>
         </span>
         <span className={`${css.versionValue} ${latest ? css.latest : ""}`}>{value || "--"}</span>
      </div>
   );
}

function DownloadCard({
   icon,
   iconFill,
   titleKey,
   title,
   desc,
   onOpen,
}: {
   icon: ReactNode;
   iconFill?: boolean;
   titleKey: string;
   title: string;
   desc: string;
   onOpen: () => void;
}) {
   return (
      <button type="button" data-no-drag className={css.downloadCard} onClick={onOpen}>
         <span className={css.cardIcon}>
            <svg viewBox="0 0 24 24" fill={iconFill ? "currentColor" : "none"} stroke={iconFill ? undefined : "currentColor"} aria-hidden="true">
               {icon}
            </svg>
         </span>
         <span className={css.cardInfo}>
            <span className={css.cardTitle}>
               <T k={titleKey}>{title}</T>
            </span>
            <span className={css.cardDesc}>{desc}</span>
         </span>
         <span className={css.cardArrow}>
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
               <ArrowIcon />
            </svg>
         </span>
      </button>
   );
}

export function App() {
   const info = store.use((s) => s.info);
   const deltaChecking = store.use((s) => s.deltaChecking);
   const autoNote = store.use((s) => s.autoNote);
   const busy = store.use((s) => s.busy);
   const progressPercent = store.use((s) => s.progressPercent);
   const progressText = store.use((s) => s.progressText);
   const controlsVisible = store.use((s) => s.controlsVisible);
   const error = store.use((s) => s.error);

   useEffect(() => {
      if (!deltaChecking) return;
      const id = window.setTimeout(() => {
         send("deltaTimeout");
         store.set({ deltaChecking: false, controlsVisible: true });
      }, 45000);
      return () => window.clearTimeout(id);
   }, [deltaChecking]);

   const notNow = () => send("notNow");
   const openLink = (url?: string) => {
      if (url) send("openLink", { url });
   };

   return (
      <>
         <Titlebar titleKey="update.available.title" title="Update Available" closeVisible={controlsVisible} onClose={notNow} />

         <div className={css.container}>
            <div className={css.badge}>
               <T k="updateAvail.badge">[ UPDATE AVAILABLE ]</T>
            </div>
            <div className={css.title}>
               <T k="updateAvail.title">A NEW VERSION IS AVAILABLE</T>
            </div>

            <div className={`${css.versionInfo} panel`}>
               <VersionRow labelKey="updateAvail.current" label="Current" value={info?.currentVersion} />
               <VersionRow labelKey="updateAvail.latest" label="Latest" value={info?.latestVersion} latest />
               <VersionRow labelKey="updateAvail.type" label="Type" value={info?.updateType} />
            </div>

            {deltaChecking && (
               <div className={`${css.deltaCheck} panel`}>
                  <div className={css.spinner} />
                  <span>
                     <T k="updateAvail.checking">Calculating update size…</T>
                  </span>
               </div>
            )}

            {autoNote && (
               <div className={`${css.autoNote} panel`}>
                  <div className={css.autoNoteTitle}>{autoNote.title}</div>
                  <div className={css.autoNoteDesc}>{autoNote.desc}</div>
               </div>
            )}

            {busy && (
               <div className={`${css.progressBox} panel`}>
                  <div className={css.progressTitle}>{busy}</div>
                  <div className={css.progressTrack}>
                     <div className={css.progressFill} style={{ width: `${progressPercent}%` }} />
                  </div>
                  <div className={css.progressText}>{progressText}</div>
               </div>
            )}

            {error && <div className={css.errorLine}>{error}</div>}

            {controlsVisible && (
               <>
                  <div className={css.sectionLabel}>
                     <T k="updateAvail.downloadSection">Download Latest Version</T>
                  </div>
                  <div className={css.downloadSection}>
                     <DownloadCard
                        titleKey="updateAvail.cdnTitle"
                        title="CDN Server"
                        desc={info?.cdnFilename || "cdn.ardysamods.my.id"}
                        icon={<CdnIcon />}
                        onOpen={() => openLink(info?.cdnUrl)}
                     />
                     <DownloadCard
                        titleKey="updateAvail.websiteTitle"
                        title="Download from Website"
                        desc={info?.githubFilename || "ardysamods.my.id"}
                        icon={<GitHubIcon />}
                        iconFill
                        onOpen={() => openLink(info?.githubUrl)}
                     />
                  </div>
               </>
            )}
         </div>

         <Footer layout="end" className={css.footer}>
            {controlsVisible && (
               <Button variant="ghost" onClick={notNow}>
                  <T k="updateAvail.notNow">Not Now</T>
               </Button>
            )}
         </Footer>
      </>
   );
}
