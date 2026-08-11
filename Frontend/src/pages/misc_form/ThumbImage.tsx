import { useEffect, useState } from "react";

const FORMATS = [".webp", ".png", ".jpg", ".jpeg"];
const EXT_RE = /\.(png|webp|jpg|jpeg)$/i;

function nextFormatUrl(url: string, tried: ReadonlySet<string>): string | null {
   const currentExt = (url.match(EXT_RE)?.[0] ?? "").toLowerCase();
   const base = url.replace(EXT_RE, "");
   for (const fmt of FORMATS) {
      if (fmt === currentExt || tried.has(fmt)) continue;
      return base + fmt;
   }
   return null;
}

export function ThumbImage({
   src,
   alt,
   letter,
   imgClassName,
   placeholderClassName,
}: {
   src: string | null | undefined;
   alt: string;
   letter: string;
   imgClassName: string | undefined;
   placeholderClassName: string | undefined;
}) {
   const [current, setCurrent] = useState(src ?? null);
   const [tried, setTried] = useState<Set<string>>(() => new Set());
   const [exhausted, setExhausted] = useState(!src);

   useEffect(() => {
      setCurrent(src ?? null);
      setTried(new Set());
      setExhausted(!src);
   }, [src]);

   if (!current || exhausted) {
      return <div className={placeholderClassName}>{letter}</div>;
   }

   return (
      <img
         className={imgClassName}
         src={current}
         alt={alt}
         loading="lazy"
         onError={() => {
            const ext = (current.match(EXT_RE)?.[0] ?? "").toLowerCase();
            const nextTried = new Set(tried);
            nextTried.add(ext);
            const next = nextFormatUrl(current, nextTried);
            setTried(nextTried);
            if (next) setCurrent(next);
            else setExhausted(true);
         }}
      />
   );
}
