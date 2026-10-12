import { useEffect } from "react";
import { T, useLocale } from "../../bridge/i18n";
import { ActionBar } from "./ActionBar";
import { CfgBanner } from "./CfgBanner";
import { CvarsTab } from "./CvarsTab";
import { Icon, type IconName } from "./icons";
import { LaunchTab } from "./LaunchTab";
import { Sidebar } from "./Sidebar";
import { applyPreset, setSearch, store, switchTab } from "./store";
import { TitleBar } from "./TitleBar";
import { Toast } from "./Toast";
import type { Tab } from "./types";

const TABS: { id: Tab; key: string; fallback: string; icon: IconName }[] = [
   { id: "cvars", key: "perf.tab.video", fallback: "[ VIDEO ]", icon: "settings_slider" },
   { id: "launch", key: "perf.tab.launch", fallback: "[ LAUNCH ]", icon: "play" },
];

function SearchBox() {
   const { t } = useLocale();
   const search = store.use((s) => s.search);
   const label = t("perf.search.placeholder", "Search settings or cvar");

   return (
      <label className="search-box" data-no-drag>
         <Icon name="search" size={14} />
         <input type="text" value={search} placeholder={label} aria-label={label} onChange={(e) => setSearch(e.target.value)} />
         {search && (
            <button type="button" className="flex text-amt-text-dim hover:text-amt-text" aria-label={t("perf.search.clear", "Clear search")} title={t("perf.search.clear", "Clear search")} onClick={() => setSearch("")}>
               <Icon name="x_close" size={12} />
            </button>
         )}
      </label>
   );
}

export function App() {
   const activeTab = store.use((s) => s.activeTab);

   useEffect(() => {
      applyPreset("competitive");
   }, []);

   return (
      <>
         <TitleBar />

         <div className="flex relative z-10" style={{ height: "calc(100vh - 44px)" }}>
            <Sidebar />

            <div className="flex-1 min-w-0 flex flex-col overflow-hidden">
               <div className="flex items-end justify-between gap-4 border-b border-amt-border px-5 pt-1">
                  <div className="flex" role="tablist">
                     {TABS.map((tab) => (
                        <button
                           key={tab.id}
                           type="button"
                           role="tab"
                           aria-selected={activeTab === tab.id}
                           data-no-drag
                           className={`tab-btn whitespace-nowrap px-4 py-3 text-sm font-bold tracking-wider ${activeTab === tab.id ? "active" : ""}`}
                           onClick={() => switchTab(tab.id)}
                        >
                           <Icon name={tab.icon} size={15} />
                           <T k={tab.key}>{tab.fallback}</T>
                        </button>
                     ))}
                  </div>
                  {activeTab === "cvars" && (
                     <div className="pb-2">
                        <SearchBox />
                     </div>
                  )}
               </div>

               <CfgBanner />

               {activeTab === "cvars" ? <CvarsTab /> : <LaunchTab />}

               <ActionBar />
            </div>
         </div>

         <Toast />
      </>
   );
}
