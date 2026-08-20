import { useRef, useState } from "react";
import { send, useEscape } from "../../bridge/host";
import { translate } from "../../bridge/i18n";
import { AlertModal, LogModal } from "./AlertModal";
import { CachingOverlay } from "./CachingOverlay";
import { Header } from "./Header";
import { ModeModal } from "./ModeModal";
import { OptionModal } from "./OptionModal";
import { OptionsGrid } from "./OptionsGrid";
import { ProgressOverlay } from "./ProgressOverlay";
import { applyResetOption, store, type Choice } from "./store";
import { TitleBar } from "./TitleBar";
import { TributeModal } from "./TributeModal";
import css from "./misc.module.css";


export function App() {
   const options = store.use((s) => s.options);
   const selections = store.use((s) => s.selections);
   const version = store.use((s) => s.version);
   const status = store.use((s) => s.status);
   const generating = store.use((s) => s.generating);
   const cachingVisible = store.use((s) => s.cachingVisible);
   const cachingStatus = store.use((s) => s.cachingStatus);
   const flashResetId = store.use((s) => s.flashResetId);
   const progress = store.use((s) => s.progress);
   const modeModalOpen = store.use((s) => s.modeModalOpen);
   const alert = store.use((s) => s.alert);

   const [optionModalIndex, setOptionModalIndex] = useState(-1);
   const [styleOverlay, setStyleOverlay] = useState<{ optId: string; choice: Choice } | null>(null);
   const [etherealOverlay, setEtherealOverlay] = useState<{ optId: string; choiceId: string } | null>(null);
   const [etherealSelections, setEtherealSelections] = useState<string[]>([]);
   const [tribute, setTribute] = useState<{ choiceName: string | null } | null>(null);
   const [logModalOpen, setLogModalOpen] = useState(false);

   const tributeShownFor = useRef<Set<string>>(new Set());
   const shouldGenerateAfterTribute = useRef(false);

   const opts = options ?? [];

   const close = () => send("close");
   useEscape(() => {
      if (!generating) close();
   });

   const closeOptionModal = () => {
      setStyleOverlay(null);

      if (optionModalIndex >= 0) {
         const opt = opts[optionModalIndex];
         if (opt?.isSpecialVpk) {
            const currentSel = selections[opt.id];
            const defaultChoice = opt.choices?.[0]?.id ?? "default";
            if (currentSel && currentSel !== defaultChoice && !tributeShownFor.current.has(opt.id)) {
               tributeShownFor.current.add(opt.id);
               setOptionModalIndex(-1);
               setTribute({ choiceName: currentSel });
               return;
            }
         }
      }
      setOptionModalIndex(-1);
   };

   const navigateOption = (dir: number) => {
      if (opts.length === 0) return;
      setOptionModalIndex((i) => (i + dir + opts.length) % opts.length);
   };

   const enforceExclusion = (optId: string, currentChoiceId: string) => {
      const opt = opts.find((o) => o.id === optId);
      if (!opt?.excludesWith || opt.excludesWith.length === 0) return;

      const defaultChoice = opt.choices?.[0]?.id || "default";
      if (currentChoiceId === defaultChoice) return;

      for (const excludedId of opt.excludesWith) {
         const excludedOpt = opts.find((o) => o.id === excludedId);
         if (!excludedOpt) continue;
         const excludedDefault = excludedOpt.choices?.[0]?.id || "default";
         const currentExcluded = selections[excludedId];
         if (currentExcluded && currentExcluded !== excludedDefault) {
            applyResetOption(excludedId, excludedDefault);
            send("selectionChanged", { optionId: excludedId, choiceId: excludedDefault });
         }
      }
   };

   const selectBaseChoice = (optId: string, choice: Choice) => {
      let resultingChoiceId: string;
      if (choice.styles && choice.styles.length > 0) {
         const currentSelection = selections[optId];
         const hasSelectedStyle = choice.styles.some((s) => s.id === currentSelection);
         if (hasSelectedStyle) {
            resultingChoiceId = currentSelection!;
         } else {
            resultingChoiceId = choice.styles[0]!.id;
            store.set((s) => ({ selections: { ...s.selections, [optId]: resultingChoiceId } }));
            send("selectionChanged", { optionId: optId, choiceId: resultingChoiceId });
         }
         setStyleOverlay({ optId, choice });
      } else {
         resultingChoiceId = choice.id;
         store.set((s) => ({ selections: { ...s.selections, [optId]: resultingChoiceId } }));
         send("selectionChanged", { optionId: optId, choiceId: resultingChoiceId });
      }
      enforceExclusion(optId, resultingChoiceId);
   };

   const selectStyle = (styleId: string) => {
      if (!styleOverlay) return;
      const { optId } = styleOverlay;
      store.set((s) => ({ selections: { ...s.selections, [optId]: styleId } }));
      send("selectionChanged", { optionId: optId, choiceId: styleId });
   };

   const openEtherealOverlay = (optId: string, choiceId: string) => {
      const saved = selections["CourierEthereal"] || "";
      setEtherealSelections(
         saved
            .split(",")
            .map((s) => s.trim())
            .filter(Boolean),
      );
      setEtherealOverlay({ optId, choiceId });
   };

   const toggleEthereal = (effectName: string) => {
      const idx = etherealSelections.indexOf(effectName);
      const next =
         idx >= 0 ? etherealSelections.filter((n) => n !== effectName) : etherealSelections.length < 2 ? [...etherealSelections, effectName] : etherealSelections;
      if (next === etherealSelections) return;

      setEtherealSelections(next);
      const joined = next.join(",");
      store.set((s) => ({ selections: { ...s.selections, CourierEthereal: joined } }));
      send("selectionChanged", { optionId: "CourierEthereal", choiceId: joined });
   };

   const clearSelections = () => {
      if (generating) return;
      const hadSelections = Object.keys(selections).some((id) => {
         const opt = opts.find((o) => o.id === id);
         const def = opt?.choices?.[0]?.id;
         return selections[id] && selections[id] !== def;
      });
      store.set({ selections: {} });
      setEtherealSelections([]);
      send("clearSelections");
      store.set({
         status: hadSelections
            ? translate("miscForm.status.cleared", "Selections cleared")
            : translate("miscForm.status.alreadyDefault", "Already at default"),
      });
   };

   const generate = () => {
      if (generating) return;
      let specialChoiceName: string | null = null;
      const hasSpecialSelected = opts.some((opt) => {
         if (!opt.isSpecialVpk) return false;
         const sel = selections[opt.id];
         const def = opt.choices?.[0]?.id || "default";
         if (sel && sel !== def) {
            specialChoiceName = sel;
            return true;
         }
         return false;
      });

      if (hasSpecialSelected) {
         shouldGenerateAfterTribute.current = true;
         setTribute({ choiceName: specialChoiceName });
      } else {
         send("generate", { selections });
      }
   };

   const closeTribute = () => {
      setTribute(null);
      if (shouldGenerateAfterTribute.current) {
         shouldGenerateAfterTribute.current = false;
         send("generate", { selections });
      }
   };

   const closeAlert = () => {
      store.set((s) => ({ alert: { ...s.alert, visible: false } }));
      send("alertDismissed");
   };

   const playFromAlert = () => {
      store.set((s) => ({ alert: { ...s.alert, visible: false } }));
      send("alertPlay");
   };

   const chooseMode = (mode: "clean" | "add" | null) => {
      store.set({ modeModalOpen: false });
      send("modeSelected", { mode });
   };

   return (
      <>
         <div className={css.bgfx} aria-hidden="true" />
         <TitleBar />

         <Header
            options={options}
            generating={generating}
            onClear={clearSelections}
            onLoad={() => send("loadPreset")}
            onSave={() => send("savePreset", { selections })}
            onGenerate={generate}
         />

         <main className={css.mfMain}>
            <div className={css.mfScroll}>
               <OptionsGrid options={opts} selections={selections} flashResetId={flashResetId} onOpen={setOptionModalIndex} />
            </div>
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

         {optionModalIndex >= 0 && (
            <OptionModal
               options={opts}
               index={optionModalIndex}
               selections={selections}
               styleOverlay={styleOverlay}
               etherealOverlay={etherealOverlay}
               etherealSelections={etherealSelections}
               onNavigate={navigateOption}
               onClose={closeOptionModal}
               onSelectBaseChoice={selectBaseChoice}
               onCloseStyleOverlay={() => setStyleOverlay(null)}
               onSelectStyle={selectStyle}
               onOpenEtherealOverlay={openEtherealOverlay}
               onCloseEtherealOverlay={() => setEtherealOverlay(null)}
               onToggleEthereal={toggleEthereal}
            />
         )}

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

         {modeModalOpen && (
            <ModeModal onSelect={chooseMode} onDismiss={() => chooseMode(null)} />
         )}

         {alert.visible && (
            <AlertModal
               title={alert.title}
               message={alert.message}
               type={alert.type}
               hasLog={alert.hasLog}
               onClose={closeAlert}
               onShowLog={() => setLogModalOpen(true)}
               onPlay={playFromAlert}
            />
         )}

         {logModalOpen && (
            <LogModal lines={progress.lines} onClose={() => setLogModalOpen(false)} onCopy={(text) => send("copyConsole", { text })} />
         )}
      </>
   );
}
