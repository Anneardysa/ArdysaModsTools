import { createStore } from "../../bridge/store";

export type UpdateItem = {
   hero?: string;
   image?: string;
   date?: string;
   attribute?: string;
};

export const store = createStore<{ version: string | null; items: UpdateItem[] | null }>({
   version: null,
   items: null,
});
