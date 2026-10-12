import type { KeyboardEvent } from "react";

const FOCUSABLE = "button:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex='-1'])";

export function trapTab(e: KeyboardEvent<HTMLElement>) {
   if (e.key !== "Tab") return;
   const items = [...e.currentTarget.querySelectorAll<HTMLElement>(FOCUSABLE)];
   if (items.length === 0) return;
   const first = items[0]!;
   const last = items[items.length - 1]!;
   if (e.shiftKey && document.activeElement === first) {
      e.preventDefault();
      last.focus();
   } else if (!e.shiftKey && document.activeElement === last) {
      e.preventDefault();
      first.focus();
   }
}
