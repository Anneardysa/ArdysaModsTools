import type { ButtonHTMLAttributes, ReactNode } from "react";
import css from "./Button.module.css";

type Variant = "ghost" | "primary" | "danger";

export function Button({
   variant = "ghost",
   className,
   children,
   ...rest
}: { variant?: Variant; children: ReactNode } & ButtonHTMLAttributes<HTMLButtonElement>) {
   return (
      <button
         type="button"
         data-no-drag
         className={[css.btn, css[variant], className].filter(Boolean).join(" ")}
         {...rest}
      >
         {children}
      </button>
   );
}
