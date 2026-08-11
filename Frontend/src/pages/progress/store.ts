import { createStore } from "../../bridge/store";

export type ServerEntry = { Name: string; Status?: string };
export type PreviewHero = { name: string; image: string };

type DownloadProgress =
   | { kind: "bytes"; downloaded: number; total: number }
   | { kind: "files"; current: number; total: number }
   | null;

export type PreviewState =
   | { status: "hidden" }
   | { status: "loading" }
   | { status: "error"; message: string }
   | { status: "ready"; heroes: PreviewHero[] };

export const store = createStore<{
   percent: number;
   status: string;
   substatus: string;
   dlSpeed: string;
   downloadProgress: DownloadProgress;
   metricsHidden: boolean;
   cancelHidden: boolean;
   serverLog: ServerEntry[];
   preview: PreviewState;
}>({
   percent: 0,
   status: "",
   substatus: "",
   dlSpeed: "-- MB/S",
   downloadProgress: null,
   metricsHidden: false,
   cancelHidden: false,
   serverLog: [],
   preview: { status: "hidden" },
});
