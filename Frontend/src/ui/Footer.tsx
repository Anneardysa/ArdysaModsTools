import type { ReactNode } from "react";
import css from "./Footer.module.css";

export function Footer({
   layout = "end",
   className,
   children,
}: {
   layout?: "end" | "between";
   className?: string;
   children: ReactNode;
}) {
   return (
      <footer className={[css.footer, css[layout], className].filter(Boolean).join(" ")}>{children}</footer>
   );
}
