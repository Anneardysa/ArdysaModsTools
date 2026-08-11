import { createStore } from "../../bridge/store";

export type DownloadLinks = { mega?: string | null; mediafire?: string | null };

export const store = createStore<{ links: DownloadLinks; dragActive: boolean }>({
   links: {},
   dragActive: false,
});
