import { useEffect, useRef, useState } from "react";
import { send } from "../../bridge/host";
import { useLocale } from "../../bridge/i18n";
import { AlertModal, LogModal } from "./AlertModal";
import { CachingOverlay } from "./CachingOverlay";
import { ConfirmModal } from "./ConfirmModal";
import { filterHeroes, hasAnySelection } from "./helpers";
import { Header } from "./Header";
import { HeroGrid } from "./HeroGrid";
import { LatestUpdatesCarousel } from "./LatestUpdatesCarousel";
import { SetModal } from "./SetModal";
import {
   closeModal,
   closeStylePreview,
   navigateHero,
   navigateSet,
   navigateToUpdate,
   requestClearAllSelections,
   requestGenerate,
   selectSet,
   store,
   toggleFavorite,
} from "./store";
import { StylePreviewModal } from "./StylePreviewModal";
import { TitleBar } from "./TitleBar";
import type { FilterCategory } from "./types";
import css from "./gallery.module.css";

export function App() {
   const { t } = useLocale();
   const heroes = store.use((s) => s.heroes);
   const selections = store.use((s) => s.selections);
   const favorites = store.use((s) => s.favorites);
   const version = store.use((s) => s.version);
   const status = store.use((s) => s.status);
   const currentFilter = store.use((s) => s.currentFilter);
   const searchQuery = store.use((s) => s.searchQuery);
   const showOnlyWithSets = store.use((s) => s.showOnlyWithSets);
   const latestUpdates = store.use((s) => s.latestUpdates);
   const updatesCollapsed = store.use((s) => s.updatesCollapsed);
   const cachingVisible = store.use((s) => s.cachingVisible);
   const cachingStatus = store.use((s) => s.cachingStatus);
   const alert = store.use((s) => s.alert);
   const generationLogLines = store.use((s) => s.generationLogLines);

   const [logModalOpen, setLogModalOpen] = useState(false);
   const searchInputRef = useRef<HTMLInputElement>(null);

   const filteredHeroes = filterHeroes(heroes, { filter: currentFilter, search: searchQuery, onlyWithSets: showOnlyWithSets, favorites });
   const selectionCount = Object.values(selections).filter(hasAnySelection).length;

   useEffect(() => {
      const onKeyDown = (e: KeyboardEvent) => {
         const s = store.get();
         const isSearchFocused = document.activeElement === searchInputRef.current;

         if (s.modalHeroId != null) {
            if (s.stylePreview) {
               if (e.key === "Escape") {
                  e.preventDefault();
                  closeStylePreview();
               }
               return;
            }
            switch (e.key) {
               case "ArrowLeft":
                  e.preventDefault();
                  navigateHero(-1);
                  break;
               case "ArrowRight":
                  e.preventDefault();
                  navigateHero(1);
                  break;
               case "ArrowUp":
                  e.preventDefault();
                  navigateSet(-1);
                  break;
               case "ArrowDown":
                  e.preventDefault();
                  navigateSet(1);
                  break;
               case "Enter": {
                  e.preventDefault();
                  const hero = s.heroes.find((h) => h.id === s.modalHeroId);
                  if (hero) selectSet(hero, s.focusedSetIndex);
                  closeModal();
                  break;
               }
               case "Escape":
                  e.preventDefault();
                  closeModal();
                  break;
            }
            return;
         }

         if (!isSearchFocused && e.key.length === 1 && !e.ctrlKey && !e.altKey && !e.metaKey) {
            searchInputRef.current?.focus();
         } else if (e.key === "Escape" && isSearchFocused) {
            searchInputRef.current?.blur();
            store.set({ searchQuery: "" });
         }
      };
      document.addEventListener("keydown", onKeyDown);
      return () => document.removeEventListener("keydown", onKeyDown);
   }, []);

   return (
      <>
         <div className={css.bgfx} aria-hidden="true" />
         <TitleBar />

         <Header
            filter={currentFilter}
            search={searchQuery}
            onlyWithSets={showOnlyWithSets}
            selectionCount={selectionCount}
            onFilterChange={(cat: FilterCategory) => store.set({ currentFilter: cat })}
            onSearchChange={(q: string) => store.set({ searchQuery: q })}
            onToggleHasSets={() => store.set((s) => ({ showOnlyWithSets: !s.showOnlyWithSets }))}
            onSave={() => send("savePreset", { selections })}
            onLoad={() => send("loadPreset")}
            onClearAll={requestClearAllSelections}
            onGenerate={requestGenerate}
            searchInputRef={searchInputRef}
         />

         <main className={css.main}>
            <LatestUpdatesCarousel
               updates={latestUpdates}
               collapsed={updatesCollapsed}
               onToggleCollapsed={() => store.set((s) => ({ updatesCollapsed: !s.updatesCollapsed }))}
               onSelectUpdate={navigateToUpdate}
            />
            <HeroGrid heroes={filteredHeroes} selections={selections} favorites={favorites} onOpen={(id) => store.set({ modalHeroId: id, focusedSetIndex: 0 })} onToggleFavorite={toggleFavorite} />
         </main>

         <SetModal />
         <StylePreviewModal />

         <footer className={css.statusBar}>
            <span id="statusText" className={css.statusText}>{status || t("hero.status.ready", "Ready")}</span>
            <span id="versionText">{version}</span>
         </footer>

         {cachingVisible && <CachingOverlay current={cachingStatus.current} total={cachingStatus.total} />}

         <ConfirmModal />

         {alert.visible && (
            <AlertModal
               title={alert.title}
               message={alert.message}
               type={alert.type}
               hasLog={alert.hasLog}
               onClose={() => {
                  store.set((s) => ({ alert: { ...s.alert, visible: false } }));
                  send("alertDismissed");
               }}
               onShowLog={() => {
                  setLogModalOpen(true);
                  send("generationLogOpened");
               }}
               onPlay={() => {
                  store.set((s) => ({ alert: { ...s.alert, visible: false } }));
                  send("alertPlay");
               }}
            />
         )}

         {logModalOpen && <LogModal lines={generationLogLines} onClose={() => setLogModalOpen(false)} />}
      </>
   );
}
