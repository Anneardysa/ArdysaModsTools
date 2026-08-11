import { createStore } from "../../bridge/store";

export type UpdateInfoPayload = {
   currentVersion?: string;
   latestVersion?: string;
   updateType?: string;
   cdnUrl?: string;
   githubUrl?: string;
   cdnFilename?: string;
   githubFilename?: string;
   deltaPending?: boolean;
};

export const store = createStore<{
   info: UpdateInfoPayload | null;
   deltaChecking: boolean;
   autoNote: { title: string; desc: string } | null;
   busy: string | null;
   progressPercent: number;
   progressText: string;
   controlsVisible: boolean;
   error: string | null;
}>({
   info: null,
   deltaChecking: false,
   autoNote: null,
   busy: null,
   progressPercent: 0,
   progressText: " ",
   controlsVisible: true,
   error: null,
});
