import type { Choice, MiscOption, Selections } from "./store";

export const MAX_ETHEREAL_SLOTS = 2;

export const hasEthereal = (opt: MiscOption) => /^(courier|ward)$/i.test(opt.id);
export const etherealKey = (opt: MiscOption) => `${opt.id}Ethereal`;

export function parseEthereal(value: string | undefined): string[] {
   return (value ?? "")
      .split(",")
      .map((s) => s.trim())
      .filter(Boolean);
}

export const defaultChoiceId = (opt: MiscOption) => opt.choices?.[0]?.id ?? "default";
export const selectedChoiceId = (opt: MiscOption, selections: Selections) => selections[opt.id] || defaultChoiceId(opt);
export const isChanged = (opt: MiscOption, selections: Selections) => selectedChoiceId(opt, selections) !== defaultChoiceId(opt);

export const isCustomized = (opt: MiscOption, selections: Selections) =>
   isChanged(opt, selections) || (hasEthereal(opt) && parseEthereal(selections[etherealKey(opt)]).length > 0);

export const isChoiceSelected = (choice: Choice, selectedId: string) =>
   choice.id === selectedId || !!choice.styles?.some((s) => s.id === selectedId);

export function baseChoiceOf(opt: MiscOption, id: string): Choice | undefined {
   return opt.choices?.find((c) => isChoiceSelected(c, id));
}

const PINNED = ["courier", "ward"];

export type NavGroup = { category: string; options: MiscOption[] };

export function navGroups(options: MiscOption[]): NavGroup[] {
   const rank = (o: MiscOption) => {
      const i = PINNED.indexOf(o.id.toLowerCase());
      return i < 0 ? PINNED.length : i;
   };
   const groups = new Map<string, MiscOption[]>();
   for (const o of [...options].sort((a, b) => rank(a) - rank(b))) {
      const category = o.category || "Other";
      groups.set(category, [...(groups.get(category) ?? []), o]);
   }
   return [...groups].map(([category, opts]) => ({ category, options: opts }));
}

export function activeOptionOf(options: MiscOption[], activeId: string | null): MiscOption | undefined {
   return options.find((o) => o.id === activeId) ?? navGroups(options)[0]?.options[0];
}

export function filterChoices(choices: Choice[], query: string): Choice[] {
   const q = query.trim().toLowerCase();
   if (!q) return choices;
   const hit = (c: Choice) => c.name.toLowerCase().includes(q);
   return choices.filter((c) => hit(c) || !!c.styles?.some(hit));
}

export function shownStyleId(choice: Choice, selectedId: string): string | undefined {
   return choice.styles?.find((s) => s.id === selectedId)?.id ?? choice.styles?.[0]?.id;
}

export function stepStyle(choice: Choice, currentStyleId: string, dir: 1 | -1): string {
   const styles = choice.styles ?? [];
   if (styles.length === 0) return choice.id;
   const i = Math.max(0, styles.findIndex((s) => s.id === currentStyleId));
   return styles[(i + dir + styles.length) % styles.length]!.id;
}
