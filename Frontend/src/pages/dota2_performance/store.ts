import { createStore } from "../../bridge/store";
import { MAP_CLEANUP_DEFAULTS, PRESETS, SETTINGS } from "./data";
import type { CfgBannerState, CvarValues, PresetName, Tab, ToastType } from "./types";

const LS_LAUNCH_ENABLED = "amt_launch_enabled";
const LS_LAUNCH_CUSTOM = "amt_launch_custom";

function defaultValues(): CvarValues {
   const values: CvarValues = { ...MAP_CLEANUP_DEFAULTS };
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

   search: string;

   toast: { visible: boolean; message: string; type: ToastType };
   cfgBanner: { state: CfgBannerState; message: string };

   mapCleanup: { mounted: boolean };
   busy: boolean;
}>({
   currentValues: defaultValues(),
   originalValues: {},
   activePreset: null,

   enabledLaunchOptions: loadLaunchEnabled(),
   customLaunchOptions: loadLaunchCustom(),

   activeTab: "cvars",
   collapsedCategories: {},

   deleteArmed: false,

   search: "",

   toast: { visible: false, message: "", type: "info" },
   cfgBanner: { state: "hidden", message: "" },

   mapCleanup: { mounted: true },
   busy: false,
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

export function setSearch(search: string) {
   store.set({ search });
}

export function changedKeys(current: CvarValues, original: CvarValues): string[] {
   return Object.keys(current).filter((k) => original[k] !== undefined && original[k] !== current[k]);
}

export function hasWrittenCfg(original: CvarValues): boolean {
   return Object.keys(original).some((k) => !k.startsWith("amt_"));
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

export function setMapCleanupStatus(json: string) {
   try {
      const data = JSON.parse(json) as { foliage?: boolean; props?: boolean; mounted: boolean; sync?: boolean };
      if (data.foliage === undefined || data.props === undefined) {
         store.set({ mapCleanup: { mounted: data.mounted }, busy: false });
         return;
      }
      const installed: CvarValues = { amt_remove_foliage: data.foliage ? "1" : "0", amt_remove_props: data.props ? "1" : "0" };
      const s = store.get();
      const currentValues = data.sync ? { ...s.currentValues, ...installed } : s.currentValues;
      store.set({
         mapCleanup: { mounted: data.mounted },
         busy: false,
         originalValues: { ...s.originalValues, ...installed },
         currentValues,
         activePreset: matchPreset(currentValues),
      });
   } catch (e) {
      console.error("setMapCleanupStatus error:", e);
      store.set({ busy: false });
   }
}

export function setBusy() {
   store.set({ busy: true });
}

export function getAllSettings(): string {
   return JSON.stringify(store.get().currentValues);
}
