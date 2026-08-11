import { createStore } from "../../bridge/store";

export type Release = {
   tag?: string;
   name?: string;
   date?: string | null;
   body?: string;
   htmlUrl?: string;
};

export const store = createStore<{ releases: Release[]; loaded: boolean }>({
   releases: [],
   loaded: false,
});
