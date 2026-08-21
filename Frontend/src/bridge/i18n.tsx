import { useSyncExternalStore, type ReactNode } from "react";


type Vars = Record<string, string | number>;

declare global {
   interface Window {
      t?: (key: string, vars?: Vars | null) => string;
      tp?: (key: string, count: number, vars?: Vars | null) => string;
      renderLogSegments?: (segments: unknown) => string;
   }
}

let epoch = 0;
if (typeof window !== "undefined") {
   window.addEventListener("amt:locale", () => {
      epoch++;
   });
}

const subscribe = (onChange: () => void) => {
   window.addEventListener("amt:locale", onChange);
   return () => window.removeEventListener("amt:locale", onChange);
};

export function useLocale(): { t: typeof translate; tp: typeof translatePlural } {
   useSyncExternalStore(subscribe, () => epoch, () => epoch);
   return { t: translate, tp: translatePlural };
}

export function translate(key: string, fallback?: string, vars?: Vars): string {
   const value = window.t?.(key, vars ?? null);
   if (value == null || value === key) return fallback ?? key;
   return value;
}

export function translatePlural(key: string, count: number, fallback?: string, vars?: Vars): string {
   const value = window.tp?.(key, count, vars ?? null);
   if (value == null || value.startsWith(key)) return fallback ?? key;
   return value;
}

export function T({ k, vars, values, children }: { k: string; vars?: Vars; values?: Vars; children?: ReactNode }): ReactNode {
   const { t } = useLocale();
   const value = t(k, undefined, vars ?? values);
   return value === k ? (children ?? k) : value;
}

const INLINE_MARKUP = /<br\s*\/?>|<b>([\s\S]*?)<\/b>/gi;

function renderInlineMarkup(text: string): ReactNode[] {
   const out: ReactNode[] = [];
   const re = new RegExp(INLINE_MARKUP.source, "gi");
   let last = 0;
   let key = 0;
   let m: RegExpExecArray | null;

   while ((m = re.exec(text)) !== null) {
      if (m.index > last) out.push(text.slice(last, m.index));
      out.push(
         m[0].toLowerCase().startsWith("<br") ? <br key={key++} /> : <b key={key++}>{m[1]}</b>,
      );
      last = m.index + m[0].length;
   }
   if (last < text.length) out.push(text.slice(last));
   return out;
}

export function THtml({ k, vars, children }: { k: string; vars?: Vars; children?: ReactNode }): ReactNode {
   const { t } = useLocale();
   const value = t(k, undefined, vars);
   return value === k ? (children ?? k) : renderInlineMarkup(value);
}
