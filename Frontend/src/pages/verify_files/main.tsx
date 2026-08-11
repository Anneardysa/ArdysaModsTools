import { createRoot } from "react-dom/client";
import { expose } from "../../bridge/host";
import "../../design/base.css";
import { App } from "./App";
import { beginCheck, finishCheck, store } from "./store";

expose({
   startCheck: (index: number) => beginCheck(index),
   completeCheck: (index: number, passed: boolean, detail?: string) => finishCheck(index, passed, detail),
   allDone: (passed: number, total: number, showPatch: boolean) =>
      store.set({ summary: { passed, total, showPatch } }),
});

createRoot(document.getElementById("root")!).render(<App />);
