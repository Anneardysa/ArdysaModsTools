import { createStore } from "../../bridge/store";

export type CheckState = "wait" | "checking" | "pass" | "fail";

export type Check = { state: CheckState; detail: string | null };

export type Summary = { passed: number; total: number; showPatch: boolean };

export const CHECK_COUNT = 4;

export const store = createStore<{
   checks: Check[];
   visible: boolean[];
   summary: Summary | null;
}>({
   checks: Array.from({ length: CHECK_COUNT }, () => ({ state: "wait", detail: null })),
   visible: Array.from({ length: CHECK_COUNT }, () => false),
   summary: null,
});

function patchIndex<T>(list: T[], index: number, value: T): T[] {
   const next = list.slice();
   next[index] = value;
   return next;
}

export function beginCheck(index: number) {
   if (index < 0 || index >= CHECK_COUNT) return;
   store.set((s) => ({
      visible: patchIndex(s.visible, index, true),
      checks: patchIndex(s.checks, index, { state: "checking", detail: s.checks[index]?.detail ?? null }),
   }));
}

export function finishCheck(index: number, passed: boolean, detail?: string) {
   if (index < 0 || index >= CHECK_COUNT) return;
   store.set((s) => ({
      visible: patchIndex(s.visible, index, true),
      checks: patchIndex(s.checks, index, {
         state: passed ? "pass" : "fail",
         detail: detail || s.checks[index]?.detail || null,
      }),
   }));
}
