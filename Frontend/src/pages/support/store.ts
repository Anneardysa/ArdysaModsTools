import { createStore } from "../../bridge/store";

export const store = createStore<{ promptMode: boolean; countdown: number; snooze: boolean }>({
   promptMode: false,
   countdown: 0,
   snooze: false,
});
