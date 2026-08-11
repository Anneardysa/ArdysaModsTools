import { useEffect } from "react";
import { send, useEscape } from "../../bridge/host";
import { T, useLocale } from "../../bridge/i18n";
import { WaveBackdrop, WindowCorners } from "../../ui/Backdrop";
import { Button } from "../../ui/Button";
import { store } from "./store";
import css from "./preview.module.css";


const PLACEHOLDER =
   "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 56 56'%3E%3Crect fill='%23222' width='56' height='56'/%3E%3C/svg%3E";

export function App() {
   const { tp } = useLocale();
   const items = store.use((s) => s.items);
   const loaded = store.use((s) => s.loaded);

   const confirm = () => send("confirm");
   const cancel = () => send("cancel");

   useEscape(cancel);

   useEffect(() => {
      const onKeyDown = (e: KeyboardEvent) => {
         if (e.key !== "Enter") return;
         if ((e.target as Element)?.closest?.("button")) return;
         confirm();
      };
      document.addEventListener("keydown", onKeyDown);
      return () => document.removeEventListener("keydown", onKeyDown);
   }, []);

   const count = items.length;

   return (
      <>
         <WaveBackdrop />
         <WindowCorners />
         <div className={css.dragArea} onMouseDown={() => send("startDrag")} />

         <div className={css.container}>
            <div className={`${css.header} ${css.fadeIn}`}>
               <div className={css.headerIcon}>
                  <svg
                     viewBox="0 0 24 24"
                     fill="none"
                     stroke="currentColor"
                     strokeWidth="2"
                     strokeLinecap="round"
                     strokeLinejoin="round"
                     aria-hidden="true"
                  >
                     <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
                     <line x1="12" y1="9" x2="12" y2="13" />
                     <line x1="12" y1="17" x2="12.01" y2="17" />
                  </svg>
               </div>
               <h1 className={css.title}>
                  <T k="genPreview.title">Confirm Generation</T>
               </h1>
               <div className={css.subtitle}>
                  {loaded
                     ? tp(
                          "genPreview.subtitle",
                          count,
                          `${count} hero set${count === 1 ? "" : "s"} will be generated`,
                       )
                     : ""}
               </div>
            </div>

            <div className={`${css.list} ${css.fadeIn}`}>
               {items.map((item, i) => (
                  <div className={css.row} key={`${item.hero}-${item.setName}-${i}`}>
                     <img
                        className={css.thumb}
                        loading="lazy"
                        alt=""
                        src={item.thumb || PLACEHOLDER}
                        onError={(e) => {
                           if (e.currentTarget.src !== PLACEHOLDER) e.currentTarget.src = PLACEHOLDER;
                        }}
                     />
                     <div className={css.rowInfo}>
                        <div className={css.hero}>{item.hero}</div>
                        <div className={css.set}>{item.setName}</div>
                     </div>
                  </div>
               ))}
               {loaded && count === 0 && (
                  <div className={css.empty}>
                     <T k="hero.noSelections.title">No Selections</T>
                  </div>
               )}
            </div>

            <div className={`${css.actions} ${css.fadeIn}`}>
               <Button variant="ghost" onClick={cancel}>
                  <T k="common.cancel">Cancel</T>
               </Button>
               <Button variant="primary" onClick={confirm} disabled={loaded && count === 0}>
                  <T k="common.generate">Generate</T>
               </Button>
            </div>
         </div>
      </>
   );
}
