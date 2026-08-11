import { useEffect } from "react";
import { T } from "../../bridge/i18n";
import { ActionBar } from "./ActionBar";
import { CfgBanner } from "./CfgBanner";
import { CvarsTab } from "./CvarsTab";
import { LaunchTab } from "./LaunchTab";
import { Sidebar } from "./Sidebar";
import { applyPreset, store, switchTab } from "./store";
import { TitleBar } from "./TitleBar";
import { Toast } from "./Toast";
import type { Tab } from "./types";

const TABS: { id: Tab; key: string; fallback: string }[] = [
   { id: "cvars", key: "perf.tab.video", fallback: "[ VIDEO ]" },
   { id: "launch", key: "perf.tab.launch", fallback: "[ LAUNCH ]" },
];

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

            <div className="flex-1 flex flex-col overflow-hidden">
               <CfgBanner />

               <div className="flex border-b border-amt-border px-5 pt-1">
                  {TABS.map((tab) => (
                     <button
                        key={tab.id}
                        type="button"
                        data-no-drag
                        className={`tab-btn px-5 py-3 text-base font-bold tracking-wider ${activeTab === tab.id ? "active" : ""}`}
                        onClick={() => switchTab(tab.id)}
                     >
                        <T k={tab.key}>{tab.fallback}</T>
                     </button>
                  ))}
               </div>

               {activeTab === "cvars" ? <CvarsTab /> : <LaunchTab />}

               <ActionBar />
            </div>
         </div>

         <Toast />
      </>
   );
}
