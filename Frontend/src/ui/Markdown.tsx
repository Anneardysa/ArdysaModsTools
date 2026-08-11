import type { ReactNode } from "react";
import { send } from "../bridge/host";


type Style = Record<"h" | "p" | "ul" | "li" | "code" | "pre", string | undefined>;

const LINK = /\[([^\]]+)\]\((https?:\/\/[^\s)]+)\)/g;
const BOLD = /\*\*([^*]+)\*\*/g;
const CODE = /`([^`]+)`/g;

function safeHref(url: string): string | null {
   try {
      const parsed = new URL(url);
      return parsed.protocol === "http:" || parsed.protocol === "https:" ? parsed.href : null;
   } catch {
      return null;
   }
}

function inline(text: string, style: Style, keyBase: string): ReactNode[] {
   type Token = { start: number; end: number; node: ReactNode };
   const tokens: Token[] = [];
   let n = 0;

   const collect = (re: RegExp, make: (m: RegExpExecArray) => ReactNode | null) => {
      const rx = new RegExp(re.source, re.flags);
      let m: RegExpExecArray | null;
      while ((m = rx.exec(text)) !== null) {
         if (tokens.some((t) => m!.index < t.end && m!.index + m![0].length > t.start)) continue;
         const node = make(m);
         if (node !== null) tokens.push({ start: m.index, end: m.index + m[0].length, node });
      }
   };

   collect(CODE, (m) => (
      <code className={style.code} key={`${keyBase}-c${n++}`}>
         {m[1]}
      </code>
   ));
   collect(BOLD, (m) => <strong key={`${keyBase}-b${n++}`}>{m[1]}</strong>);
   collect(LINK, (m) => {
      const href = safeHref(m[2]!);
      if (!href) return m[1]!;
      return (
         <a
            key={`${keyBase}-a${n++}`}
            href={href}
            data-no-drag
            onClick={(e) => {
               e.preventDefault();
               send("openUrl", { url: href });
            }}
         >
            {m[1]}
         </a>
      );
   });

   tokens.sort((a, b) => a.start - b.start);

   const out: ReactNode[] = [];
   let cursor = 0;
   for (const token of tokens) {
      if (token.start > cursor) out.push(text.slice(cursor, token.start));
      out.push(token.node);
      cursor = token.end;
   }
   if (cursor < text.length) out.push(text.slice(cursor));
   return out;
}

export function Markdown({ source, style }: { source: string; style: Style }) {
   const lines = source.replace(/\r/g, "").split("\n");
   const blocks: ReactNode[] = [];
   let listItems: ReactNode[] = [];
   let codeLines: string[] | null = null;
   let key = 0;

   const flushList = () => {
      if (!listItems.length) return;
      blocks.push(
         <ul className={style.ul} key={`ul${key++}`}>
            {listItems}
         </ul>,
      );
      listItems = [];
   };
   const flushCode = () => {
      if (codeLines === null) return;
      blocks.push(
         <pre className={style.pre} key={`pre${key++}`}>
            <code>{codeLines.join("\n")}</code>
         </pre>,
      );
      codeLines = null;
   };

   for (const raw of lines) {
      if (raw.trim().startsWith("```")) {
         if (codeLines === null) {
            flushList();
            codeLines = [];
         } else {
            flushCode();
         }
         continue;
      }
      if (codeLines !== null) {
         codeLines.push(raw);
         continue;
      }

      const line = raw.trim();
      if (line === "") {
         flushList();
         continue;
      }

      const heading = line.match(/^#{1,6}\s+(.*)$/);
      if (heading) {
         flushList();
         blocks.push(
            <div className={style.h} key={`h${key++}`}>
               {inline(heading[1]!, style, `h${key}`)}
            </div>,
         );
         continue;
      }

      const item = line.match(/^[-*]\s+(.*)$/);
      if (item) {
         listItems.push(
            <li className={style.li} key={`li${key++}`}>
               {inline(item[1]!, style, `li${key}`)}
            </li>,
         );
         continue;
      }

      flushList();
      blocks.push(
         <p className={style.p} key={`p${key++}`}>
            {inline(line, style, `p${key}`)}
         </p>,
      );
   }
   flushList();
   flushCode();

   return <>{blocks}</>;
}
