import { createStore } from "../../bridge/store";

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
   selections: Selections;
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
}>({
   options: null,
   selections: {},
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
});

export function applyResetOption(optionId: string, defaultChoiceId: string) {
   store.set((s) => ({ selections: { ...s.selections, [optionId]: defaultChoiceId }, flashResetId: optionId }));
   window.setTimeout(() => {
      if (store.get().flashResetId === optionId) store.set({ flashResetId: null });
   }, 600);
}
