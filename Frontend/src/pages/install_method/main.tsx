import { createRoot } from "react-dom/client";
import { expose } from "../../bridge/host";
import "../../design/base.css";
import { store, type DownloadLinks } from "./store";
import { App } from "./App";

expose({
   setDownloadLinks: (links: DownloadLinks) => store.set({ links: links ?? {} }),
   setDrag: (on: boolean) => store.set({ dragActive: Boolean(on) }),
});

createRoot(document.getElementById("root")!).render(<App />);
