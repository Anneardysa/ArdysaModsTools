import { send, useEscape } from "../../bridge/host";
import { T } from "../../bridge/i18n";
import { BrandGlyph, BrandSprite } from "../../ui/BrandMark";
import { ExternalLink } from "../../ui/ExternalLink";
import { Footer } from "../../ui/Footer";
import { GITHUB_REPO_URL } from "../../ui/StarOnGitHub";
import { Titlebar } from "../../ui/Titlebar";
import { store } from "./store";
import css from "./about.module.css";

const CDN = "https://cdn.ardysamods.my.id/image";

const TECH = [
   { name: "C#", src: `${CDN}/c-sharp.svg` },
   { name: "Python", src: `${CDN}/python.svg` },
   { name: "HTML5", src: `${CDN}/html-5.svg` },
   { name: "JavaScript", src: `${CDN}/javascript.svg` },
];

const LIBS = ["WebView2", "ValveKeyValue", "ImageSharp", "SharpCompress", "HLLib / HLExtract"];

const THANKS = [
   { label: "Dota 2 Changer", href: "https://dota2changer.com/" },
   { label: "Darkness", href: "https://t.me/s/Darkness_Logovo" },
   { label: "Kisilev", href: "https://vk.com/id363951132" },
   { label: "Source2Viewer", href: "https://github.com/ValveResourceFormat/ValveResourceFormat" },
];

export function App() {
   const version = store.use((s) => s.version);
   useEscape(() => send("close"));

   return (
      <>
         <BrandSprite />
         <Titlebar titleKey="shell.titlebar.about" title="About" />

         <div className={css.body}>
            <div className={css.hero}>
               <span className={css.heroMark}>
                  <BrandGlyph height={26} />
               </span>
               <div className={css.heroText}>
                  <span className={css.heroName}>ArdysaModsTools</span>
                  <span className={css.heroTag}>
                     <T k="about.heroTag">Dota 2 Cosmetic Mod Manager</T>
                  </span>
                  <span className={css.heroVer}>{version}</span>
               </div>
            </div>

            <p className={css.desc}>
               <T k="about.desc">
                  Install, customize, and disable client-side cosmetic mods for Dota&nbsp;2 — hero skins,
                  weather, terrain, HUD, couriers, wards and more. Mods are applied safely and reversibly,
                  with automatic re-patching after each game update.
               </T>
            </p>

            <div className={css.sectionLabel}>
               <T k="about.credits">Credits &amp; Acknowledgments</T>
            </div>

            <div className={css.credit}>
               <span className={css.creditKey}>
                  <T k="about.author">Author</T>
               </span>
               <span className={css.creditVal}>
                  Ardysa <ExternalLink href="https://github.com/Anneardysa">(@Anneardysa)</ExternalLink>
               </span>
            </div>

            <div className={css.credit}>
               <span className={css.creditKey}>
                  <T k="about.thanksTo">Thanks To</T>
               </span>
               <span className={`${css.creditVal} ${css.muted}`}>
                  {THANKS.map((entry) => (
                     <span key={entry.href}>
                        <ExternalLink href={entry.href}>{entry.label}</ExternalLink>
                        {", "}
                     </span>
                  ))}
                  modders &amp; content creators, and Valve Corporation for Dota&nbsp;2 and the
                  Source&nbsp;2 engine.
               </span>
            </div>

            <div className={css.credit}>
               <span className={css.creditKey}>
                  <T k="about.builtWith">Built With</T>
               </span>
               <div className={css.techStack}>
                  {TECH.map((tech) => (
                     <span className={css.tech} key={tech.name} title={tech.name}>
                        <img
                           src={tech.src}
                           alt={tech.name}
                           onError={(e) => {
                              (e.currentTarget.parentElement as HTMLElement).style.display = "none";
                           }}
                        />
                     </span>
                  ))}
               </div>
               <div className={css.libs}>
                  {LIBS.map((lib) => (
                     <span className={css.lib} key={lib}>
                        {lib}
                     </span>
                  ))}
               </div>
            </div>
         </div>

         <Footer layout="between">
            <span className={css.footerNote}>© Ardysa</span>
            <ExternalLink href={GITHUB_REPO_URL} className={css.footerLink}>
               <T k="about.viewGithub">View on GitHub</T>
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
