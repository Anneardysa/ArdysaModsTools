import { useEffect, useState, type ReactNode } from "react";
import { send, startDragUnlessInteractive } from "../../bridge/host";
import { T, translate, useLocale } from "../../bridge/i18n";
import { store, type Settings } from "./store";
import css from "./settings.module.css";


const CloseIcon = () => (
   <>
      <line x1="18" y1="6" x2="6" y2="18" />
      <line x1="6" y1="6" x2="18" y2="18" />
   </>
);
const PathIcon = () => <path d="M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />;
const ChangeIcon = () => (
   <>
      <polyline points="17 1 21 5 17 9" />
      <path d="M3 11V9a4 4 0 0 1 4-4h14" />
      <polyline points="7 23 3 19 7 15" />
      <path d="M21 13v2a4 4 0 0 1-4 4H3" />
   </>
);
const CheckUpdatesIcon = () => (
   <>
      <path d="M21 12a9 9 0 1 1-9-9c2.52 0 4.83 1.04 6.5 2.72" />
      <polyline points="21 3 21 9 15 9" />
   </>
);
const ClearCacheIcon = () => (
   <>
      <polyline points="3 6 5 6 21 6" />
      <path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6" />
      <path d="M10 11v6" />
      <path d="M14 11v6" />
      <path d="M9 6V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2" />
   </>
);
const ShowGuideIcon = () => (
   <>
      <circle cx="12" cy="12" r="10" />
      <path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3" />
      <line x1="12" y1="17" x2="12.01" y2="17" />
   </>
);

function Toggle({ active, label, onToggle }: { active: boolean; label: string; onToggle: () => void }) {
   return (
      <button
         type="button"
         data-no-drag
         role="switch"
         aria-checked={active}
         aria-label={label}
         className={`${css.toggleSwitch} ${active ? css.active : ""}`}
         onClick={onToggle}
      />
   );
}

function ToggleRow({
   labelKey,
   label,
   descKey,
   desc,
   active,
   onToggle,
}: {
   labelKey: string;
   label: string;
   descKey: string;
   desc: string;
   active: boolean;
   onToggle: () => void;
}) {
   return (
      <div className={css.toggleRow}>
         <div className={css.toggleInfo}>
            <span className={css.toggleLabel}>
               <T k={labelKey}>{label}</T>
            </span>
            <span className={css.toggleDesc}>
               <T k={descKey}>{desc}</T>
            </span>
         </div>
         <Toggle active={active} label={translate(labelKey, label)} onToggle={onToggle} />
      </div>
   );
}

function SelectRow({
   labelKey,
   label,
   descKey,
   desc,
   children,
}: {
   labelKey: string;
   label: string;
   descKey: string;
   desc: string;
   children: ReactNode;
}) {
   return (
      <div className={css.toggleRow}>
         <div className={css.toggleInfo}>
            <span className={css.toggleLabel}>
               <T k={labelKey}>{label}</T>
            </span>
            <span className={css.toggleDesc}>
               <T k={descKey}>{desc}</T>
            </span>
         </div>
         {children}
      </div>
   );
}

function ActionButton({
   busy,
   icon,
   labelKey,
   label,
   onClick,
}: {
   busy: boolean;
   icon: ReactNode;
   labelKey: string;
   label: string;
   onClick: () => void;
}) {
   return (
      <button type="button" data-no-drag className={css.actionBtn} disabled={busy} onClick={onClick}>
         {busy ? (
            <span className={css.spinner} aria-hidden="true" />
         ) : (
            <>
               <span className={css.actionBtnIcon}>
                  <svg viewBox="0 0 24 24" fill="none" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     {icon}
                  </svg>
               </span>
               <span className={css.actionBtnText}>
                  <T k={labelKey}>{label}</T>
               </span>
            </>
         )}
      </button>
   );
}

function Toast() {
   const toast = store.use((s) => s.toast);
   const [visible, setVisible] = useState(false);

   useEffect(() => {
      if (!toast) return;
      setVisible(true);
      const id = window.setTimeout(() => setVisible(false), 3000);
      return () => window.clearTimeout(id);
   }, [toast]);

   const variant = toast?.type === "error" ? css.error : css.success;
   return (
      <div className={`${css.toast} ${variant} ${visible ? css.show : ""}`} role="status" aria-live="polite">
         <span className={css.toastIcon} aria-hidden="true">
            {toast?.type === "error" ? "✕" : "✓"}
         </span>
         <span className={css.toastText}>{toast?.message ?? ""}</span>
      </div>
   );
}

function patchSetting<K extends keyof Settings>(key: K, value: Settings[K]) {
   store.set((s) => ({ settings: { ...s.settings, [key]: value } }));
}

export function App() {
   const { t } = useLocale();
   const settings = store.use((s) => s.settings);
   const version = store.use((s) => s.version);
   const cacheSize = store.use((s) => s.cacheSize);
   const checkUpdatesBusy = store.use((s) => s.checkUpdatesBusy);
   const clearCacheBusy = store.use((s) => s.clearCacheBusy);

   const close = () => send("close");

   const toggleSetting = (key: "startup" | "tray" | "notifications" | "preloadAssets" | "autoDetectPath") => {
      const value = !settings[key];
      patchSetting(key, value);
      send("settingChanged", { key, value });
   };

   const changeLanguage = (code: string) => {
      patchSetting("language", code);
      send("languageChanged", { value: code });
   };
   const changeUiSize = (v: string) => {
      const scale = parseFloat(v);
      patchSetting("uiScale", scale);
      send("uiScaleChanged", { value: scale });
   };
   const changeTheme = (v: string) => {
      patchSetting("theme", v);
      send("themeChanged", { value: v });
   };

   const checkUpdates = () => {
      store.set({ checkUpdatesBusy: true });
      send("checkUpdates");
   };
   const clearCache = () => {
      store.set({ clearCacheBusy: true });
      send("clearCache");
   };

   return (
      <>
         <div className={css.animatedBg} aria-hidden="true" />

         <header className={css.header} onMouseDown={startDragUnlessInteractive}>
            <div className={css.headerLeft}>
               <span className={css.headerTitle}>
                  <T k="settings.header.title">Settings</T>
               </span>
            </div>
            <button
               type="button"
               data-no-drag
               className={css.headerClose}
               onClick={close}
               title={t("common.close", "Close")}
               aria-label={t("common.close", "Close")}
            >
               <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
                  <CloseIcon />
               </svg>
            </button>
         </header>

         <div className={css.mainContent}>
            <div className={css.settingsGrid}>
               <div className={css.settingsCol}>
                  <div className={css.section}>
                     <div className={css.sectionHeader}>
                        <span className={css.sectionTitle}>
                           <T k="settings.dota.section">Dota 2</T>
                        </span>
                        <div className={css.sectionLine} />
                     </div>
                     <div className={`${css.settingsCard} panel`}>
                        <div className={css.pathField}>
                           <div className={`${css.pathBox} ${!settings.dotaPath ? css.empty : ""}`}>
                              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" aria-hidden="true">
                                 <PathIcon />
                              </svg>
                              <span className={css.pathBoxText} title={settings.dotaPath || undefined}>
                                 {settings.dotaPath || <T k="settings.autoDetect.notSet">Not set</T>}
                              </span>
                           </div>
                           <button type="button" data-no-drag className={css.pathChangeBtn} onClick={() => send("changeDotaPath")}>
                              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" aria-hidden="true">
                                 <ChangeIcon />
                              </svg>
                              <span>
                                 <T k="settings.dota.change">Change</T>
                              </span>
                           </button>
                        </div>
                        <div className={css.pathFieldNote}>
                           <T k="settings.dota.pathNote">Point this at your "dota 2 beta" folder if it isn't found automatically.</T>
                        </div>
                        <ToggleRow
                           labelKey="settings.autoDetect.label"
                           label="Auto-detect Dota 2 Path"
                           descKey="settings.autoDetect.desc"
                           desc="Find your Dota 2 installation automatically at startup when no path is set"
                           active={settings.autoDetectPath}
                           onToggle={() => toggleSetting("autoDetectPath")}
                        />
                     </div>
                  </div>

                  <div className={css.section}>
                     <div className={css.sectionHeader}>
                        <span className={css.sectionTitle}>
                           <T k="settings.section.appearance">Appearance</T>
                        </span>
                        <div className={css.sectionLine} />
                     </div>
                     <div className={`${css.settingsCard} panel`}>
                        <SelectRow labelKey="settings.language.label" label="Language" descKey="settings.language.desc" desc="Select your preferred language">
                           <select
                              className={css.langSelect}
                              data-no-drag
                              value={settings.language}
                              onChange={(e) => changeLanguage(e.target.value)}
                           >
                              <option value="en">English</option>
                              <option value="ru">Русский</option>
                              <option value="es">Español</option>
                              <option value="de">Deutsch</option>
                              <option value="fr">Français</option>
                              <option value="pt">Português</option>
                              <option value="zh-Hans">简体中文</option>
                              <option value="zh-Hant">繁體中文</option>
                           </select>
                        </SelectRow>
                        <SelectRow labelKey="settings.uisize.label" label="UI Size" descKey="settings.uisize.desc" desc="Scale the whole interface uniformly">
                           <select
                              className={css.langSelect}
                              data-no-drag
                              value={String(settings.uiScale)}
                              onChange={(e) => changeUiSize(e.target.value)}
                           >
                              <option value="1">{t("settings.uisize.normal", "Normal")}</option>
                              <option value="1.15">{t("settings.uisize.large", "Large")}</option>
                              <option value="1.3">{t("settings.uisize.xlarge", "Extra Large")}</option>
                           </select>
                        </SelectRow>
                        <SelectRow labelKey="settings.theme.label" label="Theme" descKey="settings.theme.desc" desc="Switch between the dark and light interface">
                           <select
                              className={css.langSelect}
                              data-no-drag
                              value={settings.theme}
                              onChange={(e) => changeTheme(e.target.value)}
                           >
                              <option value="dark">{t("settings.theme.dark", "Dark")}</option>
                              <option value="light">{t("settings.theme.light", "Light")}</option>
                           </select>
                        </SelectRow>
                     </div>
                  </div>
               </div>

               <div className={css.settingsCol}>
                  <div className={css.section}>
                     <div className={css.sectionHeader}>
                        <span className={css.sectionTitle}>
                           <T k="settings.section.startup">Startup &amp; Behavior</T>
                        </span>
                        <div className={css.sectionLine} />
                     </div>
                     <div className={`${css.settingsCard} panel`}>
                        <ToggleRow
                           labelKey="settings.startup.label"
                           label="Run on Windows Start"
                           descKey="settings.startup.desc"
                           desc="Launch automatically when Windows starts"
                           active={settings.startup}
                           onToggle={() => toggleSetting("startup")}
                        />
                        <ToggleRow
                           labelKey="settings.tray.label"
                           label="Minimize to System Tray"
                           descKey="settings.tray.desc"
                           desc="Keep running in background when minimized"
                           active={settings.tray}
                           onToggle={() => toggleSetting("tray")}
                        />
                        <ToggleRow
                           labelKey="settings.notifications.label"
                           label="Show Notifications"
                           descKey="settings.notifications.desc"
                           desc="Display update and status notifications"
                           active={settings.notifications}
                           onToggle={() => toggleSetting("notifications")}
                        />
                        <ToggleRow
                           labelKey="settings.preload.label"
                           label="Preload Assets on Launch"
                           descKey="settings.preload.desc"
                           desc="Download all thumbnails at startup so panels open instantly"
                           active={settings.preloadAssets}
                           onToggle={() => toggleSetting("preloadAssets")}
                        />
                     </div>
                  </div>

                  <div className={css.section}>
                     <div className={css.sectionHeader}>
                        <span className={css.sectionTitle}>
                           <T k="settings.section.actions">Actions</T>
                        </span>
                        <div className={css.sectionLine} />
                     </div>
                     <div className={css.actionGrid}>
                        <ActionButton
                           busy={checkUpdatesBusy}
                           icon={<CheckUpdatesIcon />}
                           labelKey="settings.action.checkUpdates"
                           label="Check Updates"
                           onClick={checkUpdates}
                        />
                        <ActionButton
                           busy={clearCacheBusy}
                           icon={<ClearCacheIcon />}
                           labelKey="settings.action.clearCache"
                           label="Clear Cache"
                           onClick={clearCache}
                        />
                        <ActionButton
                           busy={false}
                           icon={<ShowGuideIcon />}
                           labelKey="settings.action.showGuide"
                           label="Show Guide"
                           onClick={() => send("showGuide")}
                        />
                     </div>
                     <div className={css.cacheInfo}>
                        <span className={css.cacheText}>
                           <T k="settings.cache.label">Cache Size:</T>{" "}
                           <span className={css.cacheValue}>
                              {cacheSize || <T k="settings.cache.calculating">Calculating...</T>}
                           </span>
                        </span>
                     </div>
                  </div>
               </div>
            </div>
         </div>

         <div className={css.footer}>
            <div className={css.versionInfo}>
               <span className={css.versionBadge}>{version}</span>
            </div>
            <button type="button" data-no-drag className={css.closeBtn} onClick={close}>
               <T k="settings.close">Close</T>
            </button>
         </div>

         <Toast />
      </>
   );
}
