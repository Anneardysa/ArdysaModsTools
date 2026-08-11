import { useEffect, type MouseEvent } from "react";


declare global {
   interface Window {
      chrome?: { webview?: { postMessage(message: unknown): void } };
   }
}

export function send(type: string, payload?: Record<string, unknown>): void {
   window.chrome?.webview?.postMessage({ type, ...payload });
}

export function expose(handlers: Record<string, (...args: never[]) => unknown>): void {
   Object.assign(window, handlers);
}

export function startDragUnlessInteractive(e: MouseEvent): void {
   if (e.button !== 0) return;
   if ((e.target as Element).closest?.("[data-no-drag]")) return;
   send("startDrag");
}

export function useEscape(onEscape: () => void): void {
   useEffect(() => {
      const onKeyDown = (e: KeyboardEvent) => {
         if (e.key === "Escape") onEscape();
      };
      document.addEventListener("keydown", onKeyDown);
      return () => document.removeEventListener("keydown", onKeyDown);
   }, [onEscape]);
}
