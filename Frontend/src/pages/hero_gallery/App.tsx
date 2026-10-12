import { useEffect, useMemo, useRef } from "react";
import { send } from "../../bridge/host";
import { useLocale } from "../../bridge/i18n";
import { AlertModal, LogModal } from "./AlertModal";
import { CachingOverlay } from "./CachingOverlay";
import { ConfirmModal } from "./ConfirmModal";
import { closeDebug, debugStore, openDebug } from "./debug";
import { DebugModal } from "./DebugModal";
import { filterHeroes, hasAnySelection } from "./helpers";
import { Header } from "./Header";
import { HeroGrid } from "./HeroGrid";
import { LatestUpdatesCarousel } from "./LatestUpdatesCarousel";
import { ParticlePicker } from "./ParticlePanel";
import { SetModal } from "./SetModal";
import {
   activateFocusedSet,
   closeModal,
   closeParticlePicker,
   closeStylePreview,
   navigateHero,
   navigateSet,
   navigateToUpdate,
   requestClearAllSelections,
   requestGenerate,
   resolveConfirm,
   store,
   toggleFavorite,
} from "./store";
import { StylePreviewModal } from "./StylePreviewModal";
import { TitleBar } from "./TitleBar";
import type { FilterCategory } from "./types";
import css from "./gallery.module.css";

const openHero = (id: string) => store.set({ modalHeroId: id, focusedSetIndex: 0 });
const toggleUpdatesCollapsed = () => store.set((s) => ({ updatesCollapsed: !s.updatesCollapsed }));
const closeAlert = () => {
   store.set((s) => ({ alert: { ...s.alert, visible: false } }));
   send("alertDismissed");
};

export function App() {
   const { t } = useLocale();
   const heroes = store.use((s) => s.heroes);
   const heroesLoad = store.use((s) => s.heroesLoad);
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
   const alert = store.use((s) => s.alert);
   const generationLogLines = store.use((s) => s.generationLogLines);
   const cooldown = store.use((s) => s.cooldown);
   const detection = store.use((s) => s.detection);
   const logModalOpen = store.use((s) => s.logModalOpen);
   const debugEnabled = debugStore.use((s) => s.enabled);

   const searchInputRef = useRef<HTMLInputElement>(null);

   const filteredHeroes = useMemo(
      () => filterHeroes(heroes, { filter: currentFilter, search: searchQuery, onlyWithSets: showOnlyWithSets, favorites }),
      [heroes, currentFilter, searchQuery, showOnlyWithSets, favorites],
   );
   const selectionCount = useMemo(() => Object.values(selections).filter(hasAnySelection).length, [selections]);

   useEffect(() => {
      const onKeyDown = (e: KeyboardEvent) => {
         const s = store.get();
         const isSearchFocused = document.activeElement === searchInputRef.current;

         const closeTop: (() => void) | null = s.logModalOpen
            ? () => store.set({ logModalOpen: false })
            : s.alert.visible
               ? closeAlert
               : s.confirm.visible
                  ? () => resolveConfirm(false)
                  : debugStore.get().open
                     ? closeDebug
                     : s.particlePicker
                        ? closeParticlePicker
                        : s.stylePreview
                           ? closeStylePreview
                           : null;
         if (closeTop) {
            if (e.key === "Escape") {
               e.preventDefault();
               closeTop();
            }
            return;
         }

         if (s.modalHeroId != null) {
            if (e.key !== "Escape" && e.target instanceof Element && e.target.closest("input, select, textarea")) return;
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
                  if (e.target instanceof Element && e.target.closest("button")) return;
                  e.preventDefault();
                  const hero = s.heroes.find((h) => h.id === s.modalHeroId);
                  if (hero) activateFocusedSet(hero);
                  break;
               }
               case "Escape":
                  e.preventDefault();
                  closeModal();
                  break;
            }
            return;
         }

         if (isSearchFocused) {
            if (e.key === "Escape") {
               e.preventDefault();
               store.set({ searchQuery: "" });
               searchInputRef.current?.blur();
            }
            return;
         }

         if (e.key === "/" && !e.ctrlKey && !e.metaKey) {
            e.preventDefault();
            searchInputRef.current?.focus();
            return;
         }

         if (e.key === "Escape") {
            if (s.searchQuery) {
               e.preventDefault();
               store.set({ searchQuery: "" });
            }
         }
      };

      window.addEventListener("keydown", onKeyDown);
      return () => window.removeEventListener("keydown", onKeyDown);
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
            cooldown={cooldown}
            onFilterChange={(cat: FilterCategory) => store.set({ currentFilter: cat })}
            onSearchChange={(q: string) => store.set({ searchQuery: q })}
            onToggleHasSets={() => store.set((s) => ({ showOnlyWithSets: !s.showOnlyWithSets }))}
            onSave={() => send("savePreset", { selections })}
            onLoad={() => send("loadPreset")}
            onClearAll={requestClearAllSelections}
            onGenerate={requestGenerate}
            detection={detection}
            searchInputRef={searchInputRef}
            onDebug={debugEnabled ? openDebug : undefined}
         />

         <main className={css.main}>
            <LatestUpdatesCarousel
               updates={latestUpdates}
               collapsed={updatesCollapsed}
               onToggleCollapsed={toggleUpdatesCollapsed}
               onSelectUpdate={navigateToUpdate}
            />
            <HeroGrid
               heroes={filteredHeroes}
               selections={selections}
               favorites={favorites}
               onOpen={openHero}
               onToggleFavorite={toggleFavorite}
               loadState={heroes.length === 0 ? heroesLoad : "ready"}
            />
         </main>

         <SetModal />
         <StylePreviewModal />
         <ParticlePicker />

         <footer className={css.statusBar}>
            <span id="statusText" className={css.statusText}>{status || t("hero.status.ready", "Ready")}</span>
            <span id="versionText">{version}</span>
         </footer>

         {cachingVisible && <CachingOverlay />}

         <ConfirmModal />
         <DebugModal />

         {alert.visible && (
            <AlertModal
               title={alert.title}
               message={alert.message}
               type={alert.type}
               hasLog={alert.hasLog}
               onClose={closeAlert}
               onShowLog={() => {
                  store.set({ logModalOpen: true });
                  send("generationLogOpened");
               }}
               onPlay={() => {
                  store.set((s) => ({ alert: { ...s.alert, visible: false } }));
                  send("alertPlay");
               }}
            />
         )}

         {logModalOpen && <LogModal lines={generationLogLines} onClose={() => store.set({ logModalOpen: false })} />}
      </>
   );
}
