import { useState, type ReactNode } from "react";
import { send, useEscape } from "../../bridge/host";
import { T, THtml, useLocale } from "../../bridge/i18n";
import { Titlebar } from "../../ui/Titlebar";
import { store } from "./store";
import css from "./install.module.css";


const AutoIcon = () => (
   <>
      <path d="M12 3v12" />
      <polyline points="7 11 12 16 17 11" />
      <path d="M5 20h14" />
   </>
);
const ManualIcon = () => <path d="M3 7h5l2 2h11v9a2 2 0 0 1-2 2H3z" />;
const ImportIcon = () => (
   <>
      <path d="M12 3v12" />
      <polyline points="7 8 12 3 17 8" />
      <path d="M5 21h14" />
   </>
);
const BackIcon = () => <polyline points="15 18 9 12 15 6" />;
const ChevronIcon = () => <polyline points="9 6 15 12 9 18" />;
const DropIcon = () => (
   <>
      <path d="M4 14v4a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-4" />
      <polyline points="8 8 12 4 16 8" />
      <line x1="12" y1="4" x2="12" y2="15" />
   </>
);
const DownloadIcon = () => (
   <>
      <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
      <polyline points="7 10 12 15 17 10" />
      <line x1="12" y1="15" x2="12" y2="3" />
   </>
);

function OptionCard({
   icon,
   titleKey,
   title,
   pill,
   descKey,
   desc,
   primary,
   onClick,
}: {
   icon: ReactNode;
   titleKey: string;
   title: string;
   pill?: boolean;
   descKey: string;
   desc: string;
   primary?: boolean;
   onClick: () => void;
}) {
   const { t } = useLocale();
   return (
      <button
         type="button"
         data-no-drag
         className={`${css.opt} ${primary ? css.primary : ""} notch`}
         onClick={onClick}
      >
         <span className={css.icon}>
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
               {icon}
            </svg>
         </span>
         <span className={css.info}>
            <span className={css.t}>
               <T k={titleKey}>{title}</T>
               {pill && (
                  <span className={css.pill}>
                     <T k="installMethod.recommended">Recommended</T>
                  </span>
               )}
            </span>
            <span className={css.d}>{t(descKey, desc)}</span>
         </span>
         <svg className={css.chev} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <ChevronIcon />
         </svg>
      </button>
   );
}

export function App() {
   const links = store.use((s) => s.links);
   const dragActive = store.use((s) => s.dragActive);
   const [view, setView] = useState<"choose" | "manual">("choose");

   const close = () => send("close");
   useEscape(close);

   const goManual = () => {
      setView("manual");
      send("manual");
   };
   const goChoose = () => {
      setView("choose");
      send("back");
   };
   const openMirror = (which: "mega" | "mediafire") => {
      const url = links[which];
      if (url) send("open", { url });
   };

   return (
      <>
         <Titlebar
            titleKey="shell.card.install.title"
            title="Install ModsPack"
            closeLabelKey="common.cancel"
            closeLabel="Cancel"
            onClose={close}
         />

         {view === "choose" ? (
            <main className={css.content}>
               <div className={css.lead}>
                  <T k="installMethod.lead">Choose how you want to install mods.</T>
               </div>

               <div className={css.options}>
                  <OptionCard
                     icon={<AutoIcon />}
                     titleKey="installMethod.auto.t"
                     title="[ Auto-Install ]"
                     pill
                     descKey="installMethod.auto.d"
                     desc="Download and install the latest ModsPack automatically from the server."
                     primary
                     onClick={() => send("auto")}
                  />
                  <OptionCard
                     icon={<ManualIcon />}
                     titleKey="installMethod.manual.t"
                     title="[ Manual-Install ]"
                     descKey="installMethod.manual.d"
                     desc="Browse and select a VPK file to install manually."
                     onClick={goManual}
                  />
               </div>
            </main>
         ) : (
            <main className={css.content}>
               <button type="button" data-no-drag className={css.back} onClick={goChoose}>
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <BackIcon />
                  </svg>
                  <T k="installMethod.manual.back">Back</T>
               </button>

               <div className={css.lead}>
                  <THtml k="installMethod.manual.lead">
                     Import a VPK mods file (<b>pak01_dir.vpk</b>) from your computer.
                  </THtml>
               </div>

               <div className={css.dlSection}>
                  <div className={css.dlLabel}>
                     <T k="installMethod.manual.download">Don't have the file? Download ModsPack</T>
                  </div>
                  <div className={css.dlRow}>
                     <button
                        type="button"
                        data-no-drag
                        className={css.dlBtn}
                        disabled={!links.mega}
                        onClick={() => openMirror("mega")}
                     >
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                           <DownloadIcon />
                        </svg>
                        <span>MEGA</span>
                     </button>
                     <button
                        type="button"
                        data-no-drag
                        className={css.dlBtn}
                        disabled={!links.mediafire}
                        onClick={() => openMirror("mediafire")}
                     >
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                           <DownloadIcon />
                        </svg>
                        <span>MediaFire</span>
                     </button>
                  </div>
               </div>

               <OptionCard
                  icon={<ImportIcon />}
                  titleKey="installMethod.manual.import"
                  title="[ Import VPK Mods File ]"
                  descKey="installMethod.manual.importD"
                  desc="Browse and select a VPK file from your computer."
                  onClick={() => send("browse")}
               />

               <div className={css.orDivider}>
                  <T k="common.or">or</T>
               </div>

               <div id="dropzone" className={`${css.dropzone} ${dragActive ? css.drag : ""}`}>
                  <span className={css.dzIcon}>
                     <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <DropIcon />
                     </svg>
                  </span>
                  <span className={css.dzMain}>
                     <T k="installMethod.manual.drop">Drag &amp; drop your VPK file here</T>
                  </span>
                  <span className={css.dzSub}>
                     <T k="installMethod.manual.dropSub">pak01_dir.vpk — single .vpk only</T>
                  </span>
               </div>
            </main>
         )}
      </>
   );
}
