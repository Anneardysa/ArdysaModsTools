export type CvarType = "toggle" | "select" | "input";

export type CvarConfig = {
   label: string;
   type: CvarType;
   default: string;
   tip: string;
   options?: Record<string, string>;
};

export type Category = {
   cvars: Record<string, CvarConfig>;
};

export type LaunchOption = {
   flag: string;
   desc: string;
   default: boolean;
};

export type CvarValues = Record<string, string>;

export type PresetName = "potato" | "low" | "medium" | "high" | "ultra" | "competitive";

export type ToastType = "success" | "error" | "warning" | "info";

export type CfgBannerState = "warning" | "success" | "ok" | "hidden" | "";

export type Tab = "cvars" | "launch";
