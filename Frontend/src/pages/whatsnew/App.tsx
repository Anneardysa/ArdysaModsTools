import { send, useEscape } from "../../bridge/host";
import { T, useLocale } from "../../bridge/i18n";
import { Button } from "../../ui/Button";
import { ExternalLink } from "../../ui/ExternalLink";
import { Footer } from "../../ui/Footer";
import { Markdown } from "../../ui/Markdown";
import { Titlebar } from "../../ui/Titlebar";
import { store } from "./store";
import css from "./whatsnew.module.css";


const CHANGELOG_URL = "https://ardysamods.my.id/whatsnew.html";

const MD_STYLE = {
   h: css.mdH,
   p: css.mdP,
   ul: css.mdUl,
   li: css.mdLi,
   code: css.mdCode,
   pre: css.mdPre,
};

const BoltIcon = () => (
   <svg viewBox="0 0 24 24" fill="currentColor" style={{ width: 14, height: 14 }} aria-hidden="true">
      <path d="M13 2 4 14h6l-1 8 9-12h-6z" />
   </svg>
);

function formatDate(iso: string | null | undefined): string {
   if (!iso) return "";
   const date = new Date(iso);
   if (Number.isNaN(date.getTime())) return "";
   return date.toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
}

export function App() {
   const { t } = useLocale();
   const releases = store.use((s) => s.releases);
   const loaded = store.use((s) => s.loaded);

   useEscape(() => send("close"));

   const failed = loaded && releases.length === 0;

   return (
      <>
         <Titlebar titleKey="shell.card.news.title" title="What's New" icon={<BoltIcon />} />

         {releases.length === 0 ? (
            <div className={css.state} aria-live="polite">
               {!loaded && <div className={css.spinner} />}
               <div>
                  {failed ? (
                     <T k="whatsnew.error">Couldn't load What's New — check your connection.</T>
                  ) : (
                     <T k="whatsnew.loading">Loading the latest releases…</T>
                  )}
               </div>
               {failed && (
                  <Button variant="ghost" onClick={() => send("openUrl", { url: CHANGELOG_URL })}>
                     <T k="whatsnew.openWeb">Open on the web</T>
                  </Button>
               )}
            </div>
         ) : (
            <div className={css.list}>
               {releases.map((release, i) => (
                  <div
                     className={`${css.rel} ${i === 0 ? css.latest : ""}`}
                     key={`${release.tag ?? release.name ?? i}`}
                  >
                     <div className={css.head}>
                        <span className={css.tag}>{release.tag || release.name || ""}</span>
                        {i === 0 && (
                           <span className={css.badge}>
                              <T k="whatsnew.latest">Latest</T>
                           </span>
                        )}
                        <span className={css.date}>{formatDate(release.date)}</span>
                     </div>

                     {release.name && release.name !== release.tag && (
                        <div className={css.name}>{release.name}</div>
                     )}

                     <div className={css.body}>
                        <Markdown source={release.body ?? ""} style={MD_STYLE} />
                     </div>
                  </div>
               ))}
            </div>
         )}

         <Footer layout="end" className={css.footer}>
            <ExternalLink href={CHANGELOG_URL} className={css.footerLink}>
               {t("whatsnew.viewFull", "View full changelog")}
               <svg
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                  strokeLinecap="round"
                  strokeLinejoin="round"
               >
                  <path d="M7 17 17 7" />
                  <polyline points="9 7 17 7 17 15" />
               </svg>
            </ExternalLink>
         </Footer>
      </>
   );
}
