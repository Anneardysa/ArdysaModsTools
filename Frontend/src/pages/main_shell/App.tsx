import { useEffect } from "react";
import { Carousel } from "./Carousel";
import { ConfirmModal, resolveShellConfirm } from "./ConfirmModal";
import { Console } from "./Console";
import { ElevationModal } from "./ElevationModal";
import { FailureModal } from "./FailureModal";
import { LaunchModal } from "./LaunchModal";
import { NewsModal } from "./NewsModal";
import { Onboarding } from "./Onboarding";
import { PatchMenuDropdown } from "./PatchMenuDropdown";
import { PathFoundBanner } from "./PathFoundBanner";
import { PromoCards } from "./PromoCards";
import { Sidebar } from "./Sidebar";
import { closeInstallLogModal, closeNewsModal, closePatchMenu, finishOnboarding, onboardNext, onboardPrev, store } from "./store";
import { SyncModal } from "./SyncModal";
import { TitleBar } from "./TitleBar";
import { ToastHost } from "./ToastHost";
import { UpdatesStrip } from "./UpdatesStrip";
import { Warning } from "./Warning";
import css from "./shell.module.css";

export function App() {
   useEffect(() => {
      const onKeyDown = (e: KeyboardEvent) => {
         const s = store.get();

         if (s.confirmModal) {
            if (e.key === "Escape") resolveShellConfirm(false);
            else if (e.key === "Enter") {
               const f = document.activeElement;
               if (f instanceof HTMLButtonElement && f.closest("#confirm-modal")) return;
               e.preventDefault();
               resolveShellConfirm(true);
            }
            return;
         }

         if (s.installLogModal) {
            if (e.key === "Escape") closeInstallLogModal();
            return;
         }

         if (s.onboarding) {
            if (e.key === "Escape") finishOnboarding();
            else if (e.key === "ArrowRight" || e.key === "Enter" || e.key === " ") {
               e.preventDefault();
               onboardNext();
            } else if (e.key === "ArrowLeft") onboardPrev();
            return;
         }

         if (e.key === "Escape") {
            if (s.newsModalOpen) {
               closeNewsModal();
               return;
            }
            closePatchMenu();
         }
      };
      document.addEventListener("keydown", onKeyDown);
      return () => document.removeEventListener("keydown", onKeyDown);
   }, []);

   return (
      <>
         <div className={css.bgfx} aria-hidden="true" />
         <TitleBar />

         <div className={css.shell}>
            <Sidebar />
            <main className={css.content}>
               <Carousel />
               <UpdatesStrip />
               <PromoCards />
               <Warning />
               <PathFoundBanner />
               <Console />
            </main>
         </div>

         <PatchMenuDropdown />
         <NewsModal />
         <ConfirmModal />
         <SyncModal />
         <ElevationModal />
         <LaunchModal />
         <FailureModal />
         <ToastHost />
         <Onboarding />
      </>
   );
}
