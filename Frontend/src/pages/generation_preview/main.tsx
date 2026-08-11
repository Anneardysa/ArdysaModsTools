import { createRoot } from "react-dom/client";
import { expose } from "../../bridge/host";
import "../../design/base.css";
import { App } from "./App";
import { store, type PreviewItem } from "./store";

expose({
   loadItems: (items: PreviewItem[]) =>
      store.set({ items: Array.isArray(items) ? items : [], loaded: true }),
});

createRoot(document.getElementById("root")!).render(<App />);
