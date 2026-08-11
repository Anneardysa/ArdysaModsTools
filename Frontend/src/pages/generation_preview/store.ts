import { createStore } from "../../bridge/store";

export type PreviewItem = { hero: string; setName: string; thumb?: string };

export const store = createStore<{ items: PreviewItem[]; loaded: boolean }>({
   items: [],
   loaded: false,
});
