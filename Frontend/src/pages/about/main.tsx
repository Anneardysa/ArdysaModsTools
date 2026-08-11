import { createRoot } from "react-dom/client";
import { expose } from "../../bridge/host";
import "../../design/base.css";
import { App } from "./App";
import { store } from "./store";

expose({
   setVersion: (v: string) => store.set({ version: v ? `v${String(v).replace(/^v/i, "")}` : "" }),
});

createRoot(document.getElementById("root")!).render(<App />);
