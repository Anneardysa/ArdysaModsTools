import { useEffect } from "react";
import { send } from "../../bridge/host";
import { T, useLocale } from "../../bridge/i18n";
import { store, type ServerConnectionResult } from "./store";
import css from "./settings.module.css";

const SignalIcon = () => (
   <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M2 20h.01" />
      <path d="M7 20v-4" />
      <path d="M12 20v-8" />
      <path d="M17 20V4" />
   </svg>
);

const GaugeIcon = () => (
   <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M12 14v4" />
      <path d="M3.34 19a10 10 0 1 1 17.32 0" />
   </svg>
);

const CheckIcon = () => (
   <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <polyline points="20 6 9 17 4 12" />
   </svg>
);

const AlertIcon = () => (
   <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <circle cx="12" cy="12" r="10" />
      <line x1="12" y1="8" x2="12" y2="12" />
      <line x1="12" y1="16" x2="12.01" y2="16" />
   </svg>
);

const ZapIcon = () => (
   <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2" />
   </svg>
);

const RetestIcon = () => (
   <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M21.5 2v6h-6M21.34 15.57a10 10 0 1 1-.57-8.38l5.67-5.67" />
   </svg>
);

function getStatusBadgeKind(status: string) {
   switch (status.toLowerCase()) {
      case "optimal":
      case "good":
         return "success";
      case "fair":
      case "slow":
         return "warning";
      case "unreachable":
         return "danger";
      default:
         return "info";
   }
}

function getLatencyColorClass(latencyMs: number, reachable: boolean) {
   if (!reachable) return css.metricDanger;
   if (latencyMs <= 45) return css.metricOptimal;
   if (latencyMs <= 120) return css.metricGood;
   if (latencyMs <= 250) return css.metricFair;
   return css.metricSlow;
}

function ServerCard({
   server,
   isSelected,
   onSelect,
}: {
   server: ServerConnectionResult;
   isSelected: boolean;
   onSelect: () => void;
}) {
   const { t } = useLocale();

   const speedDisplay = server.isReachable
      ? server.downloadSpeedMBps > 0
         ? `${server.downloadSpeedMBps.toFixed(1)} MB/s`
         : `${server.downloadSpeedKBps} KB/s`
      : "—";

   const latencyDisplay = server.isReachable
      ? server.jitterMs && server.jitterMs > 0
         ? `${server.latencyMs} ms (±${server.jitterMs}ms)`
         : `${server.latencyMs} ms`
      : t("settings.testConnection.status.unreachable", "Unreachable");
   const badgeKind = getStatusBadgeKind(server.status);

   return (
      <div
         className={`${css.testServerCard} panel ${server.isRecommended ? css.recommendedCard : ""} ${isSelected ? css.selectedCard : ""}`}
      >
         <div className={css.serverCardHeader}>
            <div className={css.serverNameGroup}>
               <div className={css.serverTitleRow}>
                  <span className={`${css.serverLed} ${css[`led_${badgeKind}`]}`} />
                  <span className={css.serverCardTitle}>{server.serverName}</span>
                  {server.isRecommended && (
                     <span className={css.recommendedBadge}>
                        ★ <T k="settings.testConnection.recommended">RECOMMENDED</T>
                     </span>
                  )}
               </div>
               <span className={css.serverCardUrl}>{server.baseUrl}</span>
            </div>
            <span className={css.statusBadge} data-kind={badgeKind}>
               {t(`settings.testConnection.status.${server.status.toLowerCase()}`, server.status.toUpperCase())}
            </span>
         </div>

         <div className={css.metricsGrid}>
            <div className={css.metricBox}>
               <div className={css.metricLabel}>
                  <SignalIcon />
                  <span><T k="settings.testConnection.ping">Ping Latency</T></span>
               </div>
               <span className={`${css.metricValue} ${getLatencyColorClass(server.latencyMs, server.isReachable)}`}>
                  {latencyDisplay}
               </span>
            </div>

            <div className={css.metricBox}>
               <div className={css.metricLabel}>
                  <GaugeIcon />
                  <span><T k="settings.testConnection.sustainedSpeed">Sustained Speed</T></span>
               </div>
               <span className={`${css.metricValue} ${server.isReachable ? css.metricOptimal : css.metricDanger}`}>
                  {speedDisplay}
               </span>
            </div>
         </div>

         {server.isReachable && (server.stabilityPercent !== undefined || server.peakSpeedMBps !== undefined || server.dataSampledMB !== undefined) && (
            <div className={css.sampleSection}>
               <div className={css.sampleSectionHeader}>
                  <span><T k="settings.testConnection.telemetryHeader">STREAMING TELEMETRY</T></span>
               </div>
               <div className={css.sampleTierGrid}>
                  <div className={css.sampleTierItem}>
                     <span className={css.sampleTierLabel}><T k="settings.testConnection.stability">STABILITY</T></span>
                     <span className={`${css.sampleTierVal} ${server.stabilityPercent && server.stabilityPercent >= 85 ? css.metricOptimal : css.metricFair}`}>
                        {server.stabilityPercent ? `${server.stabilityPercent}%` : "—"}
                     </span>
                  </div>
                  <div className={css.sampleTierItem}>
                     <span className={css.sampleTierLabel}><T k="settings.testConnection.peakSpeed">PEAK RATE</T></span>
                     <span className={css.sampleTierVal}>{server.peakSpeedMBps ? `${server.peakSpeedMBps} MB/s` : "—"}</span>
                  </div>
                  <div className={css.sampleTierItem}>
                     <span className={css.sampleTierLabel}><T k="settings.testConnection.dataSampled">DATA SAMPLED</T></span>
                     <span className={css.sampleTierVal}>{server.dataSampledMB ? `${server.dataSampledMB} MB` : "—"}</span>
                  </div>
               </div>
            </div>
         )}

         {server.errorDetail && (
            <div className={css.serverErrorNote}>
               <AlertIcon />
               <span>{server.errorDetail}</span>
            </div>
         )}

         <div className={css.serverCardFooter}>
            <div className={css.qualityScore}>
               <span className={css.scoreLabel}>SCORE:</span>
               <span className={css.scoreValue}>{server.qualityScore} / 100</span>
            </div>
            <button
               type="button"
               data-no-drag
               className={`${css.applyServerBtn} ${isSelected ? css.applied : ""}`}
               disabled={!server.isReachable || isSelected}
               onClick={onSelect}
            >
               {isSelected ? (
                  <>
                     <CheckIcon />
                     <span><T k="settings.testConnection.applied">IN USE</T></span>
                  </>
               ) : (
                  <>
                     <ZapIcon />
                     <span><T k="settings.testConnection.useServer">USE SERVER</T></span>
                  </>
               )}
            </button>
         </div>
      </div>
   );
}

export function ConnectionTestModal() {
   const isOpen = store.use((s) => s.isConnectionModalOpen);
   const busy = store.use((s) => s.connectionTestBusy);
   const progress = store.use((s) => s.connectionTestProgress);
   const report = store.use((s) => s.connectionTestReport);
   const currentCdnServer = store.use((s) => s.settings.cdnServer);

   const close = () => {
      if (busy) {
         send("cancelConnectionTest");
      }
      store.set({ isConnectionModalOpen: false });
   };

   const startTest = () => {
      store.set({ connectionTestBusy: true, connectionTestProgress: null });
      send("testConnection");
   };

   const applyServer = (serverKey: string) => {
      store.set((s) => ({ settings: { ...s.settings, cdnServer: serverKey } }));
      send("applyRecommendedServer", { value: serverKey });
   };

   useEffect(() => {
      const handleKeyDown = (e: KeyboardEvent) => {
         if (e.key === "Escape" && isOpen) {
            close();
         }
      };
      window.addEventListener("keydown", handleKeyDown);
      return () => window.removeEventListener("keydown", handleKeyDown);
   }, [isOpen, busy]);

   if (!isOpen) return null;

   const progressPercent = progress?.percent ?? 0;
   const progressMessage = progress?.message ?? "Initializing network benchmark...";

   return (
      <div
         className={`${css.modalOverlay} ${css.show}`}
         onClick={(e) => {
            if (e.target === e.currentTarget) close();
         }}
      >
         <div
            className={`${css.modal} ${css.connectionModal}`}
            role="dialog"
            aria-modal="true"
            aria-labelledby="test-conn-title"
         >
            <div className={css.modalHead}>
               <div className={css.modalTitle}>
                  <span className={`${css.led} ${busy ? css.ledBusy : ""}`} />
                  <span id="test-conn-title">
                     [ BENCHMARK // <T k="settings.testConnection.title">SERVER CONNECTION TEST</T> ]
                  </span>
               </div>
               <button
                  type="button"
                  data-no-drag
                  className={css.modalX}
                  onClick={close}
                  aria-label="Close"
               >
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
                     <line x1="6" y1="6" x2="18" y2="18" />
                     <line x1="18" y1="6" x2="6" y2="18" />
                  </svg>
               </button>
            </div>

            <div className={css.modalBody}>
               {busy ? (
                  <div className={css.testingView}>
                     <div className={css.launchSpinner} />

                     <div className={css.testingStatus}>
                        <span className={css.testingStageTitle}>{progressMessage}</span>
                        <div className={css.progressBarTrack}>
                           <div className={css.progressBarFill} style={{ width: `${progressPercent}%` }} />
                        </div>
                        <span className={css.progressPercentText}>{progressPercent}% COMPLETE</span>
                     </div>
                  </div>
               ) : report ? (
                  <div className={css.resultsView}>
                     <div className={`${css.confirmNote} ${css[`diag_${report.diagnosticSeverity}`]}`}>
                        <div className={css.diagHeader}>
                           <div className={css.diagTitleGroup}>
                              <span className={css.diagLed} />
                              <span className={css.diagEyebrow}>! DIAGNOSTIC // NETWORK HEALTH</span>
                           </div>
                           {report.recommendedServerKey && report.recommendedServerKey !== currentCdnServer && (
                              <button
                                 type="button"
                                 data-no-drag
                                 className={css.quickApplyBtn}
                                 onClick={() => applyServer(report.recommendedServerKey)}
                              >
                                 <ZapIcon />
                                 <span><T k="settings.testConnection.applyRecommended">Apply Recommended Server</T></span>
                              </button>
                           )}
                        </div>
                        <div className={css.diagMessage}>{report.diagnosticMessage}</div>
                     </div>

                     <div className={css.serverGrid}>
                        {report.servers.map((server) => (
                           <ServerCard
                              key={server.serverKey}
                              server={server}
                              isSelected={currentCdnServer === server.serverKey}
                              onSelect={() => applyServer(server.serverKey)}
                           />
                        ))}
                     </div>
                  </div>
               ) : (
                  <div className={css.emptyStateView}>
                     <div className={css.emptyIcon}>
                        <GaugeIcon />
                     </div>
                     <span className={css.emptyTitle}>
                        <T k="settings.testConnection.subtitle">
                           Benchmark latency and download throughput with 10MB, 25MB, and 50MB samples to find your optimal server
                        </T>
                     </span>
                     <button
                        type="button"
                        data-no-drag
                        className={`${css.obBtn} ${css.primary}`}
                        onClick={startTest}
                     >
                        <T k="settings.testConnection.btn">Start Connection Test</T>
                     </button>
                  </div>
               )}
            </div>

            <div className={css.confirmActions}>
               <div className={css.footerMeta}>
                  {report?.testedAt && (
                     <span className={css.lastTestedText}>
                        TIMESTAMP: {new Date(report.testedAt).toLocaleTimeString()}
                     </span>
                  )}
               </div>
               <div className={css.footerButtons}>
                  {report && !busy && (
                     <button
                        type="button"
                        data-no-drag
                        className={`${css.obBtn} ${css.ghost}`}
                        onClick={startTest}
                     >
                        <RetestIcon />
                        <span><T k="settings.testConnection.retest">Retest</T></span>
                     </button>
                  )}
                  <button
                     type="button"
                     data-no-drag
                     className={`${css.obBtn} ${css.ghost}`}
                     onClick={close}
                  >
                     <T k="settings.close">Close</T>
                  </button>
               </div>
            </div>
         </div>
      </div>
   );
}
