import { send, useEscape } from "../../bridge/host";
import { T, translate } from "../../bridge/i18n";
import { AlertModal, LogModal } from "./AlertModal";
import { CachingOverlay } from "./CachingOverlay";
import { EtherealDialog } from "./EtherealDialog";
import { Header } from "./Header";
import { activeOptionOf, hasEthereal } from "./helpers";
import { ModeModal } from "./ModeModal";
import { OptionPane } from "./OptionPane";
import { ProgressOverlay } from "./ProgressOverlay";
import { Sidebar } from "./Sidebar";
import {
   chooseMode,
   clearSelections,
   closeAlert,
   closeTopLayer,
   closeTribute,
   generate,
   playFromAlert,
   store,
} from "./store";
import { TitleBar } from "./TitleBar";
import { TributeModal } from "./TributeModal";
import css from "./misc.module.css";

export function App() {
   const options = store.use((s) => s.options);
   const loadError = store.use((s) => s.loadError);
   const selections = store.use((s) => s.selections);
   const search = store.use((s) => s.search);
   const etherealOpen = store.use((s) => s.etherealOpen);
   const version = store.use((s) => s.version);
   const status = store.use((s) => s.status);
   const generating = store.use((s) => s.generating);
   const cachingVisible = store.use((s) => s.cachingVisible);
   const cachingStatus = store.use((s) => s.cachingStatus);
   const flashResetId = store.use((s) => s.flashResetId);
   const progress = store.use((s) => s.progress);
   const modeModalOpen = store.use((s) => s.modeModalOpen);
   const alert = store.use((s) => s.alert);
   const logModalOpen = store.use((s) => s.logModalOpen);
   const tribute = store.use((s) => s.tribute);
   const activeOptionId = store.use((s) => s.activeOptionId);

   useEscape(closeTopLayer);

   const opt = options ? activeOptionOf(options, activeOptionId) : undefined;

   return (
      <>
         <div className={css.bgfx} aria-hidden="true" />
         <TitleBar />

         <Header
            options={loadError !== null ? [] : options}
            selections={selections}
            generating={generating}
            onClear={clearSelections}
            onLoad={() => send("loadPreset")}
            onSave={() => send("savePreset", { selections })}
            onGenerate={generate}
         />

         <main className={css.mfMain}>
            {loadError !== null ? (
               <div className={css.mainState} role="alert">
                  <h2 className={css.mainStateTitle}>
                     <T k="miscForm.loadError">Couldn't load the option list</T>
                  </h2>
                  {loadError && <p>{loadError}</p>}
                  <p>
                     <T k="miscForm.loadErrorHint">Close and reopen Miscellaneous. If it keeps failing, check your connection.</T>
                  </p>
               </div>
            ) : options && options.length === 0 ? (
               <div className={css.mainState}>
                  <p>
                     <T k="miscForm.noOptions">No options available. Check your connection and reopen this window.</T>
                  </p>
               </div>
            ) : options && opt ? (
               <>
                  <Sidebar options={options} selections={selections} activeId={opt.id} flashResetId={flashResetId} />
                  <OptionPane opt={opt} selections={selections} search={search} />
               </>
            ) : null}
         </main>

         <footer className={css.statusBar}>
            <span className={css.statusText}>{status || translate("hero.status.ready", "Ready")}</span>
            <span>{version}</span>
         </footer>

         {cachingVisible && (
            <CachingOverlay
               current={cachingStatus.current}
               total={cachingStatus.total}
               onSkip={() => store.set({ cachingVisible: false })}
            />
         )}

         {etherealOpen && opt && hasEthereal(opt) && <EtherealDialog opt={opt} selections={selections} />}

         {tribute && <TributeModal choiceName={tribute.choiceName} onClose={closeTribute} />}

         {progress.visible && (
            <ProgressOverlay
               title={progress.title}
               percent={progress.percent}
               status={progress.status}
               lines={progress.lines}
               flash={progress.flash}
               onCancel={() => send("cancelGeneration")}
            />
         )}

         {modeModalOpen && <ModeModal onSelect={chooseMode} onDismiss={() => chooseMode(null)} />}

         {alert.visible && (
            <AlertModal
               title={alert.title}
               message={alert.message}
               type={alert.type}
               hasLog={alert.hasLog}
               onClose={closeAlert}
               onShowLog={() => store.set({ logModalOpen: true })}
               onPlay={playFromAlert}
            />
         )}

         {logModalOpen && (
            <LogModal lines={progress.lines} onClose={() => store.set({ logModalOpen: false })} onCopy={(text) => send("copyConsole", { text })} />
         )}
      </>
   );
}
