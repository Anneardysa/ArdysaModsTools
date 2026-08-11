import { createRoot } from "react-dom/client";
import { expose } from "../../bridge/host";
import "../../design/base.css";
import { App } from "./App";
import { store, type UpdateItem } from "./store";

expose({
   setModspackVersion: (v: string) => store.set({ version: v ? `v${String(v).replace(/^v/i, "")}` : null }),
   loadUpdates: (items: UpdateItem[]) => store.set({ items: items ?? [] }),
});

createRoot(document.getElementById("root")!).render(<App />);
