import { useState } from "react";
import { useEscape } from "../../bridge/host";
import { T, translate, useLocale } from "../../bridge/i18n";
import { store, type PreviewHero } from "./store";
import css from "./progress.module.css";


const CIRCUMFERENCE = 628.32;

function sendCancel(): void {
   window.chrome?.webview?.postMessage("cancel");
}

function ServerLog() {
   const servers = store.use((s) => s.serverLog);

   return (
      <div className={css.serverLog} style={servers.length === 0 ? { display: "none" } : undefined}>
         {servers.map((s, i) => {
            const status = (s.Status || "standby").toLowerCase();
            const cls = status === "active" ? css.active : status === "success" ? css.success : status === "failed" ? css.failed : css.standby;
            return (
               <div className={css.serverEntry} key={`${s.Name}-${i}`}>
                  <span className={`${css.serverDot} ${cls}`} />
                  <span className={`${css.serverName} ${cls}`}>{s.Name}</span>
               </div>
            );
         })}
      </div>
   );
}

function DownloadProgressLine() {
   const dp = store.use((s) => s.downloadProgress);

   const [value, total, unit] = !dp
      ? ["0", "0", " MB"]
      : dp.kind === "bytes"
        ? [dp.downloaded.toFixed(1), dp.total.toFixed(1), " MB"]
        : [String(dp.current), String(dp.total), " files"];

   return (
      <div className={css.downloadProgress} style={!dp ? { display: "none" } : undefined}>
         <span className={css.downloaded}>{value}</span> / <span className={css.total}>{total}</span>
         <span>{unit}</span>
      </div>
   );
}

function HeroCard({ hero, onOpen }: { hero: PreviewHero; onOpen: () => void }) {
   const [broken, setBroken] = useState(false);
   return (
      <button type="button" className={css.heroCard} onClick={onOpen}>
         <span className={css.heroImgWrapper}>
            {!broken && (
               <img
                  className={css.heroImg}
                  src={hero.image}
                  alt={hero.name}
                  loading="lazy"
                  onError={() => setBroken(true)}
               />
            )}
            <span className={css.heroGradient} />
            <span className={css.heroName}>{hero.name}</span>
         </span>
      </button>
   );
}

function PreviewPanel({ onOpenLightbox }: { onOpenLightbox: (hero: PreviewHero) => void }) {
   const { t } = useLocale();
   const preview = store.use((s) => s.preview);
   const [search, setSearch] = useState("");

   if (preview.status === "hidden") return null;

   const heroes = preview.status === "ready" ? preview.heroes : [];
   const q = search.trim().toLowerCase();
   const filtered = q ? heroes.filter((h) => h.name.toLowerCase().includes(q)) : heroes;

   return (
      <div className={`${css.previewSide} ${css.visible}`}>
         <div className={css.previewHeader}>
            <div className={css.previewTitle}>
               <span className={css.previewTitleText}>
                  <T k="progress.preview.title">ModsPack Preview</T>
               </span>
               <span className={css.previewCount}>
                  {preview.status === "ready" ? `${heroes.length} skins` : "..."}
               </span>
            </div>
         </div>

         <div className={css.previewSearch}>
            <div className={css.previewSearchWrapper}>
               <svg className={css.previewSearchIcon} fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
               </svg>
               <input
                  type="text"
                  className={css.previewSearchInput}
                  placeholder={t("progress.preview.search", "Search heroes...")}
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
               />
            </div>
         </div>

         <div className={css.previewGridWrapper}>
            {preview.status === "loading" && (
               <div className={css.previewLoading}>
                  <div className={css.previewLoadingDots}>
                     <div className={css.previewLoadingDot} />
                     <div className={css.previewLoadingDot} />
                     <div className={css.previewLoadingDot} />
                  </div>
                  <div className={css.previewStateText}>
                     <T k="progress.preview.loading">Loading preview</T>
                  </div>
                  <div className={css.previewStateSub}>
                     <T k="progress.preview.loadingSub">Fetching hero skins from server...</T>
                  </div>
               </div>
            )}

            {preview.status === "error" && (
               <div className={css.previewError}>
                  <svg className={css.previewErrorIcon} fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                     <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth="1.5"
                        d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
                     />
                  </svg>
                  <div className={css.previewStateText}>
                     <T k="progress.preview.errorTitle">Preview unavailable</T>
                  </div>
                  <div className={css.previewStateSub}>{preview.message}</div>
               </div>
            )}

            {preview.status === "ready" &&
               (filtered.length === 0 ? (
                  <div className={css.previewEmpty}>
                     <div className={css.previewStateText}>
                        <T k="progress.preview.empty">No heroes found</T>
                     </div>
                     <div className={css.previewStateSub}>
                        <T k="progress.preview.emptySub">Try a different search term</T>
                     </div>
                  </div>
               ) : (
                  <div className={css.previewGrid}>
                     {filtered.map((hero) => (
                        <HeroCard key={hero.image} hero={hero} onOpen={() => onOpenLightbox(hero)} />
                     ))}
                  </div>
               ))}
         </div>
      </div>
   );
}

export function App() {
   const percent = store.use((s) => s.percent);
   const status = store.use((s) => s.status);
   const substatus = store.use((s) => s.substatus);
   const dlSpeed = store.use((s) => s.dlSpeed);
   const metricsHidden = store.use((s) => s.metricsHidden);
   const cancelHidden = store.use((s) => s.cancelHidden);

   const [lightboxHero, setLightboxHero] = useState<PreviewHero | null>(null);
   const closeLightbox = () => setLightboxHero(null);
   useEscape(closeLightbox);

   const offset = CIRCUMFERENCE - (percent / 100) * CIRCUMFERENCE;
   const dlSpeedActive = dlSpeed !== "-- MB/S" && dlSpeed !== "0.0 MB/S";
   const statusText = status || translate("progress.preparing", "Preparing");

   return (
      <>
         <div className={css.animatedBg} aria-hidden="true" />

         <div className={css.mainLayout}>
            <div className={css.progressSide}>
               <span className={`${css.corner} ${css.tl}`} aria-hidden="true" />
               <span className={`${css.corner} ${css.tr}`} aria-hidden="true" />
               <span className={`${css.corner} ${css.bl}`} aria-hidden="true" />
               <span className={`${css.corner} ${css.br}`} aria-hidden="true" />

               <div className={css.progressRingContainer}>
                  <svg className={css.progressRing} width="220" height="220">
                     <circle className={css.progressRingBg} cx="110" cy="110" r="100" />
                     <circle
                        className={css.progressRingFill}
                        cx="110"
                        cy="110"
                        r="100"
                        style={{ strokeDasharray: CIRCUMFERENCE, strokeDashoffset: offset }}
                     />
                  </svg>
                  <div className={css.percentText}>{Math.round(percent)}%</div>
               </div>

               <div className={`${css.status} ${css.working}`}>{statusText}</div>
               <div className={css.substatus}>{substatus}</div>

               <ServerLog />
               <DownloadProgressLine />

               <div className={`${css.metricsContainer} metrics-container`} style={metricsHidden ? { display: "none" } : undefined}>
                  <div className={css.metricItem}>
                     <span className={css.metricLabel}>
                        <T k="progress.dlSpeed">DL Speed:</T>
                     </span>
                     <span className={`${css.metricValue} ${dlSpeedActive ? css.active : ""}`}>{dlSpeed}</span>
                  </div>
               </div>

               <div className={css.credits}>
                  <T k="progress.madeWith">made with ❤️</T>
               </div>

               {!cancelHidden && (
                  <div className={css.cancelContainer}>
                     <button type="button" className={css.cancelBtn} onClick={sendCancel}>
                        <T k="progress.cancel">[ CANCEL ]</T>
                     </button>
                  </div>
               )}
            </div>

            <PreviewPanel onOpenLightbox={setLightboxHero} />
         </div>

         {lightboxHero && (
            <div className={`${css.lightbox} ${css.active}`} onClick={closeLightbox}>
               <div className={css.lightboxContent} onClick={(e) => e.stopPropagation()}>
                  <img className={css.lightboxImg} src={lightboxHero.image} alt={lightboxHero.name} />
                  <div className={css.lightboxName}>{lightboxHero.name}</div>
               </div>
            </div>
         )}
      </>
   );
}
