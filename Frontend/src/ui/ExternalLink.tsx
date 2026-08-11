import type { ReactNode } from "react";
import { send } from "../bridge/host";

export function ExternalLink({
   href,
   className,
   children,
}: {
   href: string;
   className?: string;
   children: ReactNode;
}) {
   return (
      <a
         href={href}
         data-no-drag
         className={className}
         onClick={(e) => {
            e.preventDefault();
            send("openUrl", { url: href });
         }}
      >
         {children}
      </a>
   );
}
