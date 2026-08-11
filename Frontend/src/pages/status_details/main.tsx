import { createRoot } from "react-dom/client";
import { expose } from "../../bridge/host";
import "../../design/base.css";
import { App } from "./App";
import { store, type StatusPayload } from "./store";

expose({
   populate: (data: StatusPayload) => store.set({ data: data ?? {} }),
});

createRoot(document.getElementById("root")!).render(<App />);
