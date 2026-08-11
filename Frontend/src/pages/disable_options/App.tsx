import { useEffect, useRef, useState, type KeyboardEvent as ReactKeyboardEvent } from "react";
import { send, useEscape } from "../../bridge/host";
import { T, useLocale } from "../../bridge/i18n";
import { BrandSprite } from "../../ui/BrandMark";
import { Button } from "../../ui/Button";
import { Footer } from "../../ui/Footer";
import { Titlebar } from "../../ui/Titlebar";
import css from "./disable.module.css";


type Choice = "disable" | "delete";

const OPTIONS = [
   {
      id: "disable" as Choice,
      titleKey: "disableOpt.disable.t",
      title: "[ Disable Mods ]",
      descKey: "disableOpt.disable.d",
      desc: "Temporarily disable mods. You can re-enable them later.",
      danger: false,
      icon: (
         <>
            <circle cx="12" cy="12" r="9" />
            <line x1="6" y1="6" x2="18" y2="18" />
         </>
      ),
   },
   {
      id: "delete" as Choice,
      titleKey: "disableOpt.delete.t",
      title: "[ Delete Permanently ]",
      descKey: "disableOpt.delete.d",
      desc: "Remove all mod files from your game folder. This cannot be undone.",
      danger: true,
      icon: (
         <>
            <polyline points="3 6 5 6 21 6" />
            <path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6" />
            <path d="M10 11v6" />
            <path d="M14 11v6" />
            <path d="M9 6V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2" />
         </>
      ),
   },
];

export function App() {
   const { t } = useLocale();
   const [choice, setChoice] = useState<Choice>("disable");
   const optionRefs = useRef<(HTMLButtonElement | null)[]>([]);

   const confirm = () => send("confirm", { option: choice });

   useEscape(() => send("cancel"));

   useEffect(() => {
      const onKeyDown = (e: KeyboardEvent) => {
         if (e.key !== "Enter") return;
         if ((e.target as Element)?.closest?.("button")) return;
         confirm();
      };
      document.addEventListener("keydown", onKeyDown);
      return () => document.removeEventListener("keydown", onKeyDown);
   }, [choice]);

   const onGroupKeyDown = (e: ReactKeyboardEvent) => {
      const keys = ["ArrowDown", "ArrowRight", "ArrowUp", "ArrowLeft"];
      if (!keys.includes(e.key)) return;
      e.preventDefault();
      const forward = e.key === "ArrowDown" || e.key === "ArrowRight";
      const index = OPTIONS.findIndex((o) => o.id === choice);
      const next = OPTIONS[(index + (forward ? 1 : OPTIONS.length - 1)) % OPTIONS.length]!;
      setChoice(next.id);
      optionRefs.current[OPTIONS.indexOf(next)]?.focus();
   };

   return (
      <>
         <BrandSprite />
         <Titlebar titleKey="disableOpt.header" title="Disable Options" />

         <main className={css.content}>
            <div className={css.lead}>
               <T k="disableOpt.lead">Choose what to do with your installed mods.</T>
            </div>

            <div
               className={css.options}
               role="radiogroup"
               aria-label={t("disableOpt.header", "Disable Options")}
               onKeyDown={onGroupKeyDown}
            >
               {OPTIONS.map((option, i) => (
                  <button
                     key={option.id}
                     ref={(el) => {
                        optionRefs.current[i] = el;
                     }}
                     type="button"
                     data-no-drag
                     role="radio"
                     aria-checked={choice === option.id}
                     // Only the selected radio is in the tab order; arrows move within the group.
                     tabIndex={choice === option.id ? 0 : -1}
                     className={`${css.opt} ${option.danger ? css.danger : ""} notch`}
                     onClick={() => setChoice(option.id)}
                  >
                     <span className={css.icon}>
                        <svg
                           viewBox="0 0 24 24"
                           fill="none"
                           stroke="currentColor"
                           strokeWidth="2"
                           strokeLinecap="round"
                           strokeLinejoin="round"
                        >
                           {option.icon}
                        </svg>
                     </span>
                     <span className={css.info}>
                        <span className={css.title}>
                           <T k={option.titleKey}>{option.title}</T>
                        </span>
                        <span className={css.desc}>
                           <T k={option.descKey}>{option.desc}</T>
                        </span>
                     </span>
                     <span className={css.radio} />
                  </button>
               ))}
            </div>
         </main>

         <Footer layout="end">
            <Button variant="ghost" onClick={() => send("cancel")}>
               <T k="common.cancel">Cancel</T>
            </Button>
            <Button variant={choice === "delete" ? "danger" : "primary"} onClick={confirm}>
               <T k="common.confirm">Confirm</T>
            </Button>
         </Footer>
      </>
   );
}
