import { send } from "../bridge/host";
import { useLocale } from "../bridge/i18n";
import css from "./StarOnGitHub.module.css";

export const GITHUB_REPO_URL = "https://github.com/Anneardysa/ArdysaModsTools";

export function StarOnGitHub({ variant = "chip" }: { variant?: "chip" | "cta" }) {
   const { t } = useLocale();
   const label = t("common.starOnGithub", "Star on GitHub");

   return (
      <button
         type="button"
         data-no-drag
         className={`${css.star} ${variant === "cta" ? css.cta : css.chip}`}
         title={label}
         aria-label={label}
         onClick={() => send("openUrl", { url: GITHUB_REPO_URL })}
      >
         <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
            <path d="M12 2.6l2.9 5.88 6.49.95-4.7 4.58 1.11 6.46L12 17.42l-5.8 3.05 1.1-6.46-4.69-4.58 6.49-.95L12 2.6z" />
         </svg>
         <span className={css.label}>{label}</span>
      </button>
   );
}
