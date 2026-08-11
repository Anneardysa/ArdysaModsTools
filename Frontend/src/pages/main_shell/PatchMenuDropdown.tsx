import { useEffect, useRef } from "react";
import { send } from "../../bridge/host";
import { T } from "../../bridge/i18n";
import { closePatchMenu, store } from "./store";
import css from "./shell.module.css";

const ITEMS = [
   {
      type: "patchApply" as const,
      labelKey: "shell.patchMenu.update",
      fallback: "Patch Update",
      icon: (
         <>
            <path d="M21 12a9 9 0 1 1-3-6.7L21 8" />
            <path d="M21 3v5h-5" />
         </>
      ),
      sepAfter: true,
   },
   {
      type: "patchVerify" as const,
      labelKey: "shell.patchMenu.verify",
      fallback: "Verify Mod Files",
      icon: (
         <>
            <path d="M9 11l3 3L22 4" />
            <path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11" />
         </>
      ),
   },
   {
      type: "patchViewStatus" as const,
      labelKey: "shell.patchMenu.viewStatus",
      fallback: "View Status Details",
      icon: (
         <>
            <circle cx="12" cy="12" r="10" />
            <line x1="12" y1="16" x2="12" y2="12" />
            <line x1="12" y1="8" x2="12.01" y2="8" />
         </>
      ),
   },
];

export function PatchMenuDropdown() {
   const menu = store.use((s) => s.patchMenu);
   const ref = useRef<HTMLDivElement>(null);

   useEffect(() => {
      if (!menu.visible) return;
      const onDocClick = (e: MouseEvent) => {
         const target = e.target as Node;
         const patchBtn = document.getElementById("btn-patch");
         if (ref.current?.contains(target)) return;
         if (patchBtn?.contains(target)) return;
         closePatchMenu();
      };
      document.addEventListener("click", onDocClick, true);
      return () => document.removeEventListener("click", onDocClick, true);
   }, [menu.visible]);

   if (!menu.visible) return null;

   const action = (type: (typeof ITEMS)[number]["type"]) => {
      closePatchMenu();
      send(type);
   };

   return (
      <div
         id="patch-menu"
         ref={ref}
         className={`${css.menu} ${css.show}`}
         style={{ left: menu.left, top: menu.top, minWidth: menu.minWidth }}
      >
         {ITEMS.map((item) => (
            <div key={item.type}>
               <button type="button" data-no-drag className={css.menuItem} onClick={() => action(item.type)}>
                  <svg className={css.ico} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     {item.icon}
                  </svg>
                  <span>
                     <T k={item.labelKey}>{item.fallback}</T>
                  </span>
               </button>
               {item.sepAfter && <div className={css.menuSep} />}
            </div>
         ))}
      </div>
   );
}
