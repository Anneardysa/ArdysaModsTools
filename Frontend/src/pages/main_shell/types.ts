export type BadgeKind = "neutral" | "info" | "success" | "warning" | "danger" | "loading";

export type ButtonKey = "autoDetect" | "manualDetect" | "install" | "disable" | "patch" | "misc" | "hero" | "tweak";

export type ButtonStates = Partial<Record<ButtonKey, boolean>> & { detectHighlight?: boolean };

export type StatusPayload = { kind: BadgeKind; text: string; tooltip?: string };

export type SetupCheckState = "pass" | "fail" | "advisory";

export type SetupCheck = {
   id: string;
   state: SetupCheckState;
   label: string;
   stateText: string;
   detail: string;
   canFix: boolean;
   hasOwnDialog: boolean;
   detected: boolean;
};

export type SetupChecksPayload = {
   title: string;
   checks: SetupCheck[];
   fixLabel: string;
   canFix: boolean;
};

export type SyncItemDetail = {
   id: string;
   name: string;
   category: string;
   status: "new" | "modified" | "error";
   description?: string;
};

export type SyncDetailsPayload = {
   isStale: boolean;
   addedCount: number;
   modifiedCount: number;
   errorCount: number;
   summary: string;
   items: SyncItemDetail[];
};

export type PlayStatePayload = { enabled: boolean; label: string; reason: string };

export type LaunchPanelPayload = {
   heading: string;
   detail: string;
   percent: number | null;
   canCancel: boolean;
   isError: boolean;
   cancelLabel: string;
   confirmLabel: string | null;
} | null;

export type CarouselSlide = { image: string; link: string; title: string };

export type UpdateCard = {
   heroId?: string;
   heroName: string;
   setName?: string;
   setThumbnail: string;
   daysAgo: number;
};

export type ConfirmPayload = {
   id: number;
   eyebrow: string;
   heading: string;
   body: string;
   note: string;
   confirmText: string;
   cancelText: string;
   countdown: number;
   accent: "" | "warn";
};

export type InstallLogLine = { t: string; c: string };

export type InstallLogPayload = {
   title: string;
   body: string;
   variant: "error" | "success";
   lines: InstallLogLine[] | null;
};

export type ToastVariant = "success" | "error" | "info";

export type ToastPayload = { title: string; message: string; variant: ToastVariant; timeout: number };

export type OnboardStep = { target: string; title: string; desc: string; pad?: number };

export type LogSegment = string | { k: string; v?: Record<string, unknown> };

export type LogCategory = "default" | "success" | "error" | "warning" | "progress" | "notice";

export type ConsoleLine = {
   id: number;
   key?: string;
   category: LogCategory;
   text?: string;
   segments?: LogSegment[];
};
