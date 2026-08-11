import { createStore } from "../../bridge/store";
import { PRESETS, SETTINGS } from "./data";
import type { CfgBannerState, CvarValues, PresetName, Tab, ToastType } from "./types";

const LS_LAUNCH_ENABLED = "amt_launch_enabled";
const LS_LAUNCH_CUSTOM = "amt_launch_custom";

function defaultValues(): CvarValues {
   const values: CvarValues = {};
   for (const cat of Object.values(SETTINGS)) {
      for (const [cvar, cfg] of Object.entries(cat.cvars)) values[cvar] = cfg.default;
   }
   return values;
}

function loadLaunchEnabled(): Record<string, boolean> {
   const enabled: Record<string, boolean> = {};
   try {
      const saved = localStorage.getItem(LS_LAUNCH_ENABLED);
      if (saved) Object.assign(enabled, JSON.parse(saved));
   } catch {
   }
   return enabled;
}

function loadLaunchCustom(): string[] {
   try {
      const saved = localStorage.getItem(LS_LAUNCH_CUSTOM);
      if (saved) return JSON.parse(saved);
   } catch {
   }
   return [];
}

export const store = createStore<{
   currentValues: CvarValues;
   originalValues: CvarValues;
   activePreset: PresetName | null;

   enabledLaunchOptions: Record<string, boolean>;
   customLaunchOptions: string[];

   activeTab: Tab;
   collapsedCategories: Record<string, boolean>;

   deleteArmed: boolean;

   toast: { visible: boolean; message: string; type: ToastType };
   cfgBanner: { state: CfgBannerState; message: string };
}>({
   currentValues: defaultValues(),
   originalValues: {},
   activePreset: null,

   enabledLaunchOptions: loadLaunchEnabled(),
   customLaunchOptions: loadLaunchCustom(),

   activeTab: "cvars",
   collapsedCategories: {},

   deleteArmed: false,

   toast: { visible: false, message: "", type: "info" },
   cfgBanner: { state: "hidden", message: "" },
});

function saveLaunchOptions() {
   const s = store.get();
   try {
      localStorage.setItem(LS_LAUNCH_ENABLED, JSON.stringify(s.enabledLaunchOptions));
      localStorage.setItem(LS_LAUNCH_CUSTOM, JSON.stringify(s.customLaunchOptions));
   } catch {
   }
}

function matchPreset(values: CvarValues): PresetName | null {
   for (const [name, preset] of Object.entries(PRESETS) as [PresetName, CvarValues][]) {
      let match = true;
      for (const [k, v] of Object.entries(preset)) {
         if (values[k] !== v) {
            match = false;
            break;
         }
      }
      if (match) return name;
   }
   return null;
}

export function setCvar(cvar: string, value: string) {
   const currentValues = { ...store.get().currentValues, [cvar]: value };
   store.set({ currentValues, activePreset: matchPreset(currentValues) });
}

export function toggleCvar(cvar: string) {
   const s = store.get();
   setCvar(cvar, s.currentValues[cvar] === "0" ? "1" : "0");
}

export function applyPreset(name: PresetName) {
   const preset = PRESETS[name];
   if (!preset) return;
   store.set({ currentValues: { ...preset }, activePreset: name });
}

export function resetAll() {
   store.set({ currentValues: defaultValues(), activePreset: null });
}

export function toggleCategory(name: string) {
   store.set((s) => ({ collapsedCategories: { ...s.collapsedCategories, [name]: !s.collapsedCategories[name] } }));
}

export function switchTab(tab: Tab) {
   store.set({ activeTab: tab });
}

export function toggleLaunchOption(flag: string, defaultEnabled: boolean) {
   const s = store.get();
   const current = s.enabledLaunchOptions[flag] ?? defaultEnabled;
   store.set({ enabledLaunchOptions: { ...s.enabledLaunchOptions, [flag]: !current } });
   saveLaunchOptions();
}

export function addCustomLaunch(value: string) {
   const trimmed = value.trim();
   if (!trimmed) return;
   store.set((s) => ({ customLaunchOptions: [...s.customLaunchOptions, trimmed] }));
   saveLaunchOptions();
}

export function removeCustomLaunch(idx: number) {
   store.set((s) => ({ customLaunchOptions: s.customLaunchOptions.filter((_, i) => i !== idx) }));
   saveLaunchOptions();
}

let deleteArmTimer: number | null = null;

export function armOrConfirmDelete(onConfirm: () => void) {
   if (!store.get().deleteArmed) {
      store.set({ deleteArmed: true });
      if (deleteArmTimer != null) window.clearTimeout(deleteArmTimer);
      deleteArmTimer = window.setTimeout(() => store.set({ deleteArmed: false }), 3500);
      return;
   }
   if (deleteArmTimer != null) window.clearTimeout(deleteArmTimer);
   store.set({ deleteArmed: false });
   onConfirm();
}

let toastTimer: number | null = null;

export function showToast(message: string, type: ToastType = "info") {
   store.set({ toast: { visible: true, message, type } });
   if (toastTimer != null) window.clearTimeout(toastTimer);
   toastTimer = window.setTimeout(() => store.set((s) => ({ toast: { ...s.toast, visible: false } })), type === "error" ? 6500 : 3800);
}

export function showCfgBanner(message: string, state: CfgBannerState) {
   store.set({ cfgBanner: { state, message } });
}

export function hideCfgBanner() {
   store.set((s) => ({ cfgBanner: { ...s.cfgBanner, state: "hidden" } }));
}

export function loadSettings(json: string) {
   try {
      const data = JSON.parse(json) as CvarValues;
      const currentValues = { ...defaultValues(), ...data };
      store.set({ originalValues: { ...data }, currentValues, activePreset: matchPreset(currentValues) });
   } catch (e) {
      console.error("loadSettings error:", e);
   }
}

export function getAllSettings(): string {
   return JSON.stringify(store.get().currentValues);
}
