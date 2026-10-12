import { send } from "../../bridge/host";
import { translate } from "../../bridge/i18n";
import { createStore } from "../../bridge/store";
import {
   MAX_ETHEREAL_SLOTS,
   activeOptionOf,
   baseChoiceOf,
   defaultChoiceId,
   etherealKey,
   isChanged,
   isCustomized,
   parseEthereal,
   stepStyle,
} from "./helpers";

export type Choice = {
   id: string;
   name: string;
   thumbnailId?: string | null;
   styles?: Choice[] | null;
};

export type MiscOption = {
   id: string;
   name: string;
   category?: string;
   thumbnailPattern: string;
   excludesWith?: string[] | null;
   isSpecialVpk?: boolean | null;
   choices: Choice[];
};

export type Selections = Record<string, string>;
export type AlertType = "success" | "warning" | "info" | "error";

export const store = createStore<{
   options: MiscOption[] | null;
   loadError: string | null;
   selections: Selections;
   activeOptionId: string | null;
   search: string;
   etherealOpen: boolean;
   version: string;
   defaultThumb: string;
   status: string;
   generating: boolean;
   cachingVisible: boolean;
   cachingStatus: { current: number; total: number };
   flashResetId: string | null;
   progress: {
      visible: boolean;
      title: string;
      percent: number;
      status: string;
      lines: string[];
      flash: "success" | "error" | null;
   };
   modeModalOpen: boolean;
   alert: { visible: boolean; title: string; message: string; type: AlertType; hasLog: boolean };
   logModalOpen: boolean;
   tribute: { choiceName: string | null } | null;
   tributeShownFor: string[];
   generateAfterTribute: boolean;
}>({
   options: null,
   loadError: null,
   selections: {},
   activeOptionId: null,
   search: "",
   etherealOpen: false,
   version: "",
   defaultThumb: "",
   status: "",
   generating: false,
   cachingVisible: true,
   cachingStatus: { current: 0, total: 0 },
   flashResetId: null,
   progress: { visible: false, title: "", percent: 0, status: "", lines: [], flash: null },
   modeModalOpen: false,
   alert: { visible: false, title: "", message: "", type: "info", hasLog: false },
   logModalOpen: false,
   tribute: null,
   tributeShownFor: [],
   generateAfterTribute: false,
});

const optionById = (id: string) => store.get().options?.find((o) => o.id === id);

export function activeOption(): MiscOption | undefined {
   const { options, activeOptionId } = store.get();
   return options ? activeOptionOf(options, activeOptionId) : undefined;
}

export function optionsLoaded(options: MiscOption[]) {
   store.set({ options, loadError: null, cachingVisible: false, status: translate("hero.status.ready", "Ready") });
}

export function setActiveOption(id: string) {
   store.set({ activeOptionId: id, search: "", etherealOpen: false });
}

export function applyResetOption(optionId: string, defaultChoiceId: string) {
   store.set((s) => ({ selections: { ...s.selections, [optionId]: defaultChoiceId }, flashResetId: optionId }));
   window.setTimeout(() => {
      if (store.get().flashResetId === optionId) store.set({ flashResetId: null });
   }, 600);
}

export function selectChoice(optId: string, choiceId: string) {
   const opt = optionById(optId);
   if (!opt || store.get().selections[optId] === choiceId) return;

   store.set((s) => ({ selections: { ...s.selections, [optId]: choiceId } }));
   send("selectionChanged", { optionId: optId, choiceId });

   if (choiceId === defaultChoiceId(opt)) return;

   for (const excludedId of opt.excludesWith ?? []) {
      const excluded = optionById(excludedId);
      if (!excluded || !isChanged(excluded, store.get().selections)) continue;
      const def = defaultChoiceId(excluded);
      applyResetOption(excludedId, def);
      send("selectionChanged", { optionId: excludedId, choiceId: def });
   }

   if (opt.isSpecialVpk && !store.get().tributeShownFor.includes(optId)) {
      store.set((s) => ({ tribute: { choiceName: choiceId }, tributeShownFor: [...s.tributeShownFor, optId] }));
   }
}

export function resetOption(optId: string) {
   const opt = optionById(optId);
   if (opt) selectChoice(optId, defaultChoiceId(opt));
}

export function cycleStyle(optId: string, choice: Choice, shownId: string, dir: 1 | -1): string {
   const next = stepStyle(choice, shownId, dir);
   const opt = optionById(optId);
   const current = store.get().selections[optId];
   if (opt && current && baseChoiceOf(opt, current)?.id === choice.id) selectChoice(optId, next);
   return next;
}

export function toggleEthereal(opt: MiscOption, effectName: string) {
   const key = etherealKey(opt);
   const current = parseEthereal(store.get().selections[key]);
   const next = current.includes(effectName)
      ? current.filter((n) => n !== effectName)
      : current.length < MAX_ETHEREAL_SLOTS
        ? [...current, effectName]
        : null;
   if (!next) return;

   const joined = next.join(",");
   store.set((s) => ({ selections: { ...s.selections, [key]: joined } }));
   send("selectionChanged", { optionId: key, choiceId: joined });
}

export function clearSelections() {
   const { generating, options, selections } = store.get();
   if (generating) return;
   const hadSelections = (options ?? []).some((o) => isCustomized(o, selections));
   store.set({
      selections: {},
      status: hadSelections
         ? translate("miscForm.status.cleared", "Selections cleared")
         : translate("miscForm.status.alreadyDefault", "Already at default"),
   });
   send("clearSelections");
}

export function generate() {
   const { generating, options, selections } = store.get();
   if (generating) return;
   const special = (options ?? []).find((o) => o.isSpecialVpk && isChanged(o, selections));
   if (special) {
      store.set({ tribute: { choiceName: selections[special.id] ?? null }, generateAfterTribute: true });
   } else {
      send("generate", { selections });
   }
}

export function closeTribute() {
   const { generateAfterTribute, selections } = store.get();
   store.set({ tribute: null, generateAfterTribute: false });
   if (generateAfterTribute) send("generate", { selections });
}

export function chooseMode(mode: "clean" | "add" | null) {
   store.set({ modeModalOpen: false });
   send("modeSelected", { mode });
}

export function closeAlert() {
   store.set((s) => ({ alert: { ...s.alert, visible: false } }));
   send("alertDismissed");
}

export function playFromAlert() {
   store.set((s) => ({ alert: { ...s.alert, visible: false } }));
   send("alertPlay");
}

export function closeTopLayer() {
   const s = store.get();
   if (s.logModalOpen) return store.set({ logModalOpen: false });
   if (s.tribute) return;
   if (s.alert.visible) return closeAlert();
   if (s.modeModalOpen) return chooseMode(null);
   if (s.progress.visible) return;
   if (s.etherealOpen) return store.set({ etherealOpen: false });
   if (s.search) return store.set({ search: "" });
   if (!s.generating) send("close");
}
