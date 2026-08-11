import { createStore } from "../../bridge/store";

export type StatusPayload = {
   status?: string;
   statusText?: string;
   description?: string;

   showPatchBtn?: boolean;
   patchBtnText?: string;

   dotaVersion?: string;
   buildNumber?: string;
   patchedVersion?: string;
   patchDate?: string;
   versionMismatch?: boolean;

   digestOk?: boolean | null;
   gameInfoOk?: boolean | null;
   adminOk?: boolean | null;
   adminNote?: string | null;
   adminStateText?: string | null;
   adminAdvisory?: boolean;

   verifyDetail?: string | null;
   errorMessage?: string | null;
};

export const store = createStore<{ data: StatusPayload | null }>({ data: null });
