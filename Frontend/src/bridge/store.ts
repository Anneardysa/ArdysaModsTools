import { useSyncExternalStore } from "react";

export function createStore<S extends object>(initial: S) {
   let state = initial;
   const subscribers = new Set<() => void>();

   const subscribe = (onChange: () => void) => {
      subscribers.add(onChange);
      return () => {
         subscribers.delete(onChange);
      };
   };

   return {
      get: () => state,

      set(patch: Partial<S> | ((prev: S) => Partial<S>)) {
         const next = typeof patch === "function" ? patch(state) : patch;
         state = { ...state, ...next };
         subscribers.forEach((f) => f());
      },

      use<T>(select: (s: S) => T): T {
         return useSyncExternalStore(
            subscribe,
            () => select(state),
            () => select(initial),
         );
      },
   };
}
