import type { Choice, MiscOption } from "./store";

export function isDefaultChoice(name: string | undefined): boolean {
   const n = (name || "").replace(/^\s+/, "").toLowerCase();
   return n.startsWith("default") || n.startsWith("disable");
}

function findThumbnailId(opt: MiscOption, choiceId: string): string | null {
   for (const c of opt.choices ?? []) {
      if (c.id === choiceId) return c.thumbnailId ?? null;
      for (const s of c.styles ?? []) {
         if (s.id === choiceId) return s.thumbnailId ?? null;
      }
   }
   return null;
}

export function getThumbUrl(opt: MiscOption, choiceId: string): string | null {
   if (!opt.thumbnailPattern) return null;
   if (isDefaultChoice(choiceId)) return null;
   const override = findThumbnailId(opt, choiceId);
   const token =
      override ||
      choiceId
         .replace(/[^\w\s-]/g, "")
         .trim()
         .toLowerCase()
         .replace(/ /g, "_");
   return opt.thumbnailPattern.replace("{choice}", token);
}

export function findChoice(opt: MiscOption, id: string): Choice | undefined {
   for (const c of opt.choices ?? []) {
      if (c.id === id) return c;
      const style = c.styles?.find((s) => s.id === id);
      if (style) return style;
   }
   return undefined;
}
