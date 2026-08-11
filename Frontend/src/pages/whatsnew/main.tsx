import { createRoot } from "react-dom/client";
import { expose } from "../../bridge/host";
import "../../design/base.css";
import { App } from "./App";
import { store, type Release } from "./store";

expose({
   loadReleases: (payload: Release[] | string) => {
      let releases: Release[] = [];
      try {
         const parsed = typeof payload === "string" ? JSON.parse(payload) : payload;
         if (Array.isArray(parsed)) releases = parsed;
      } catch {
      }
      store.set({ releases, loaded: true });
   },
});

createRoot(document.getElementById("root")!).render(<App />);
