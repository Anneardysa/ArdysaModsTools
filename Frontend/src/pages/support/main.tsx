import { createRoot } from "react-dom/client";
import { expose } from "../../bridge/host";
import "../../design/base.css";
import { App } from "./App";
import { store } from "./store";

expose({
   startCountdown: (seconds: number) => {
      const n = Number.parseInt(String(seconds), 10);
      store.set({ promptMode: true, countdown: Number.isFinite(n) && n > 0 ? n : 5 });
   },
});

createRoot(document.getElementById("root")!).render(<App />);
