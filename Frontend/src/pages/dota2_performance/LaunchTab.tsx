import { useState } from "react";
import { send } from "../../bridge/host";
import { T, translate, useLocale } from "../../bridge/i18n";
import { LAUNCH_OPTIONS } from "./data";
import { addCustomLaunch, removeCustomLaunch, showToast, store, toggleLaunchOption } from "./store";

function launchString(enabled: Record<string, boolean>, custom: string[]): string {
   const parts: string[] = [];
   for (const opt of LAUNCH_OPTIONS) if (enabled[opt.flag] ?? opt.default) parts.push(opt.flag);
   parts.push(...custom);
   return parts.join(" ") || "(none)";
}

export function LaunchTab() {
   const { t } = useLocale();
   const enabledLaunchOptions = store.use((s) => s.enabledLaunchOptions);
   const customLaunchOptions = store.use((s) => s.customLaunchOptions);
   const [customInput, setCustomInput] = useState("");

   const generated = launchString(enabledLaunchOptions, customLaunchOptions);

   const submitCustom = () => {
      addCustomLaunch(customInput);
      setCustomInput("");
   };

   const copy = () => {
      if (generated && generated !== "(none)") {
         send("copyText", { text: generated });
         showToast(translate("perf.banner.copied", "// copied to clipboard"), "success");
      }
   };

   return (
      <div className="flex-1 overflow-y-auto p-5 space-y-4">
         <div className="glass rounded-lg p-5 fade-in">
            <div className="text-base font-bold tracking-wider mb-1">
               <T k="perf.launch.title">[ LAUNCH_OPTIONS ]</T>
            </div>
            <div className="text-xs text-amt-text-dim mb-1">
               <T k="perf.launch.steamHint">// steam &gt; right-click dota 2 &gt; properties &gt; launch options</T>
            </div>
            <div className="text-xs text-amt-warning mb-4">
               <T k="perf.launch.copyHint">// copy-only — NOT written by [ APPLY ]. your selection is remembered automatically.</T>
            </div>

            <div id="launchTags" className="flex flex-wrap gap-3 mb-5">
               {LAUNCH_OPTIONS.map((opt) => {
                  const enabled = enabledLaunchOptions[opt.flag] ?? opt.default;
                  return (
                     <div
                        key={opt.flag}
                        className={`launch-tag ${enabled ? "enabled" : ""}`}
                        data-no-drag
                        role="button"
                        tabIndex={0}
                        aria-pressed={enabled}
                        onClick={() => toggleLaunchOption(opt.flag, opt.default)}
                        onKeyDown={(e) => {
                           if (e.key === "Enter" || e.key === " ") {
                              e.preventDefault();
                              toggleLaunchOption(opt.flag, opt.default);
                           }
                        }}
                     >
                        <span style={{ fontSize: 11 }}>{enabled ? "[x]" : "[ ]"}</span> <span>{opt.flag}</span>
                     </div>
                  );
               })}
               {customLaunchOptions.map((opt, i) => (
                  <div key={`${opt}-${i}`} className="launch-tag enabled">
                     <span>{opt}</span>{" "}
                     <span
                        className="tag-x"
                        data-no-drag
                        role="button"
                        tabIndex={0}
                        aria-label={`Remove ${opt}`}
                        onClick={(e) => {
                           e.stopPropagation();
                           removeCustomLaunch(i);
                        }}
                        onKeyDown={(e) => {
                           if (e.key === "Enter" || e.key === " ") {
                              e.preventDefault();
                              e.stopPropagation();
                              removeCustomLaunch(i);
                           }
                        }}
                     >
                        x
                     </span>
                  </div>
               ))}
            </div>

            <div className="text-[11px] text-amt-text-dim mb-2 tracking-wider">
               <T k="perf.launch.generated">GENERATED_STRING</T>
            </div>
            <div className="flex items-center gap-3">
               <div id="launchString" className="flex-1 bg-amt-bg border border-amt-border rounded-lg px-4 py-3 text-sm text-amt-text break-all select-text" style={{ userSelect: "text" }}>
                  {generated}
               </div>
               <button
                  type="button"
                  data-no-drag
                  onClick={copy}
                  className="action-btn px-4 py-3 rounded-lg text-xs font-bold border border-amt-border hover:border-amt-border-strong text-amt-text-dim hover:text-amt-accent transition-colors flex-shrink-0"
               >
                  <T k="perf.launch.copy">COPY</T>
               </button>
            </div>
         </div>

         <div className="glass rounded-lg p-5 fade-in">
            <div className="text-base font-bold tracking-wider mb-1">
               <T k="perf.launch.customTitle">[ CUSTOM ]</T>
            </div>
            <div className="text-xs text-amt-text-dim mb-3">
               <T k="perf.launch.customDesc">// add custom launch flags</T>
            </div>
            <div className="flex gap-3">
               <input
                  id="customLaunchInput"
                  type="text"
                  data-no-drag
                  placeholder={t("perf.launch.customPlaceholder", "-high or -threads 8")}
                  className="cvar-input flex-1 text-left"
                  style={{ width: "auto" }}
                  value={customInput}
                  onChange={(e) => setCustomInput(e.target.value)}
                  onKeyDown={(e) => {
                     if (e.key === "Enter") submitCustom();
                  }}
               />
               <button type="button" data-no-drag onClick={submitCustom} className="action-btn px-4 py-2 rounded-lg text-xs font-bold bg-amt-accent hover:bg-amt-accent-hover text-amt-on-accent transition-colors">
                  <T k="perf.launch.add">ADD</T>
               </button>
            </div>
         </div>

         <div className="glass rounded-lg p-5 fade-in">
            <div className="text-base font-bold tracking-wider mb-3">
               <T k="perf.launch.referenceTitle">[ REFERENCE ]</T>
            </div>
            <div className="overflow-x-auto">
               <table className="w-full text-sm">
                  <thead>
                     <tr className="text-amt-text-dim border-b border-amt-border">
                        <th className="text-left py-2 pr-4">
                           <T k="perf.launch.refFlag">flag</T>
                        </th>
                        <th className="text-left py-2">
                           <T k="perf.launch.refDesc">description</T>
                        </th>
                     </tr>
                  </thead>
                  <tbody id="launchRefTable">
                     {LAUNCH_OPTIONS.map((opt) => {
                        const flagId = opt.flag.substring(1).split(" ")[0];
                        return (
                           <tr key={opt.flag} className="border-b" style={{ borderColor: "var(--divider)" }}>
                              <td className="py-2 pr-4 text-amt-accent">{opt.flag}</td>
                              <td className="py-2 text-amt-text-dim">{translate("perf.launch.desc." + flagId, opt.desc)}</td>
                           </tr>
                        );
                     })}
                  </tbody>
               </table>
            </div>
         </div>
      </div>
   );
}
