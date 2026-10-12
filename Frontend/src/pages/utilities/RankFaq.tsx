import { useEffect } from "react";
import { T } from "../../bridge/i18n";
import { Button } from "../../ui/Button";
import css from "./utilities.module.css";

const ENTRIES: { k: string; q: string; a: string }[] = [
   {
      k: "safe",
      q: "Can I be banned for this?",
      a: "It is the same kind of file swap a cosmetic mod does — artwork is replaced inside your own install and nothing is sent anywhere. Your account, your rating and your match history are never touched.",
   },
   {
      k: "who",
      q: "Who sees the medal I pick?",
      a: "Only you. It shows on your screen — profile, scoreboard, the pre-pick connecting screen and the rank line under your hero while picking — and everyone else still sees your real medal.",
   },
   {
      k: "immortal",
      q: "Can I show Immortal without calibrating?",
      a: "Yes. The medal is drawn the moment you apply, no ranked games needed. Your real rating and leaderboard place stay exactly where they were.",
   },
   {
      k: "leaderboard",
      q: "What about the Top 1000 / 100 / 10 medals?",
      a: "Dota keeps separate artwork for each leaderboard bracket, and all four are in the list. Immortal medals carry no stars — the game draws a leaderboard number there instead.",
   },
   {
      k: "ladder",
      q: "How much MMR is each medal worth?",
      a: "Every tier spans 770 MMR — Herald 0-769, Guardian 770-1539, Crusader 1540-2309, Archon 2310-3079, Legend 3080-3849, Ancient 3850-4619 — Divine runs 4620-5499, and Immortal opens at 5620. The five stars sit about 150 MMR apart inside a tier.",
   },
   {
      k: "mmr",
      q: "Does the MMR slider change my MMR?",
      a: "Yes, locally on your screen! The app bakes your custom Profile MMR, Rank Confidence progress bar, and Leaderboard Rank directly into compiled Source 2 textures and Panorama stylesheet hooks inside your mod package. Dota matchmaking and Valve servers are unaffected.",
   },
   {
      k: "stars",
      q: "Can I choose the stars?",
      a: "One to five, or none at all. Uncalibrated and Immortal never draw stars, so the control greys out there.",
   },
   {
      k: "restart",
      q: "I applied it and nothing changed.",
      a: "Dota loads its packages once, while it starts. Close the client completely and open it again — if it was already running when you pressed Apply, it is still drawing the old artwork from memory.",
   },
   {
      k: "gone",
      q: "My medal disappeared from my profile.",
      a: "Usually not the mod. Dota hides the medal and rating when your rank confidence decays; a few ranked games bring it back on its own.",
   },
   {
      k: "update",
      q: "Will a Dota update undo it?",
      a: "A large patch rewrites Valve's own files, so run the install again afterwards and re-apply.",
   },
   {
      k: "revert",
      q: "How do I get my real medal back?",
      a: "Reset. It rebuilds the mod package without any of the medal files, so the client falls back to Dota's own artwork — then restart it.",
   },
];

export function RankFaq({ onClose }: { onClose: () => void }) {
   useEffect(() => {
      const onKeyDown = (e: KeyboardEvent) => {
         if (e.key !== "Escape") return;
         e.stopPropagation();
         onClose();
      };
      window.addEventListener("keydown", onKeyDown, true);
      return () => window.removeEventListener("keydown", onKeyDown, true);
   }, [onClose]);

   return (
      <div className={css.modalScrim} onClick={onClose}>
         <div
            className={`${css.modal} notch`}
            role="dialog"
            aria-modal="true"
            aria-label="Rank Changer"
            onClick={(e) => e.stopPropagation()}
         >
            <header className={css.modalHead}>
               <h2 className={css.modalTitle}>
                  <T k="rank.faq.title">Rank medal — what it does</T>
               </h2>
               <Button onClick={onClose}>
                  <T k="common.close">Close</T>
               </Button>
            </header>

            <div className={css.modalBody}>
               {ENTRIES.map((entry) => (
                  <details key={entry.k} className={css.faqItem}>
                     <summary className={css.faqQ}>
                        <T k={`rank.faq.${entry.k}.q`}>{entry.q}</T>
                     </summary>
                     <p className={css.faqA}>
                        <T k={`rank.faq.${entry.k}.a`}>{entry.a}</T>
                     </p>
                  </details>
               ))}
            </div>
         </div>
      </div>
   );
}
