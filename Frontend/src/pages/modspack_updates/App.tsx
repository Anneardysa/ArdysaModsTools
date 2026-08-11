import { useEffect, useState } from "react";
import { send } from "../../bridge/host";
import { T, translate, useLocale } from "../../bridge/i18n";
import { Footer } from "../../ui/Footer";
import { Titlebar } from "../../ui/Titlebar";
import { store, type UpdateItem } from "./store";
import css from "./updates.module.css";


const FILTERS: { id: string; titleKey: string; title: string; label: string; labelKey?: string }[] = [
   { id: "all", titleKey: "filter.all", title: "All", label: "ALL", labelKey: "filter.allShort" },
   { id: "Strength", titleKey: "filter.strength", title: "Strength", label: "STR" },
   { id: "Agility", titleKey: "filter.agility", title: "Agility", label: "AGI" },
   { id: "Intelligence", titleKey: "filter.intelligence", title: "Intelligence", label: "INT" },
   { id: "Universal", titleKey: "filter.universal", title: "Universal", label: "UNI" },
];

const CHANGELOG_URL = "https://ardysamods.my.id/updates.html";

const StarIcon = () => (
   <svg viewBox="0 0 24 24" fill="currentColor" style={{ width: 14, height: 14 }} aria-hidden="true">
      <path d="M12 2l2.4 5 5.6.8-4 4 1 5.5L12 19.8 6.9 22.3l1-5.5-4-4 5.6-.8z" />
   </svg>
);

const BrokenImageIcon = () => (
   <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round">
      <rect x="3" y="3" width="18" height="18" rx="2" />
      <circle cx="8.5" cy="8.5" r="1.5" />
      <path d="m21 15-5-5L5 21" />
   </svg>
);

function Card({ item, onOpen }: { item: UpdateItem; onOpen: () => void }) {
   const [broken, setBroken] = useState(false);
   const hint = translate("modspackUpdates.previewHint", "Click to preview");

   return (
      <div
         className={css.card}
         title={hint}
         role="button"
         tabIndex={0}
         aria-label={item.hero ? `${hint}: ${item.hero}` : hint}
         onClick={onOpen}
         onKeyDown={(e) => {
            if (e.key === "Enter" || e.key === " ") {
               e.preventDefault();
               onOpen();
            }
         }}
      >
         {item.image && !broken ? (
            <img
               className={css.cardImg}
               loading="lazy"
               alt={item.hero ?? ""}
               src={item.image}
               onError={() => setBroken(true)}
            />
         ) : (
            <div className={css.ph}>
               <BrokenImageIcon />
            </div>
         )}
         <div className={css.veil} />
         <div className={css.meta}>
            {item.attribute && <span className={css.attr}>{item.attribute}</span>}
            <span className={css.hero}>{item.hero ?? ""}</span>
         </div>
      </div>
   );
}

function LightboxImage({ src, alt }: { src?: string; alt: string }) {
   const [state, setState] = useState<"loading" | "ok" | "error">(src ? "loading" : "error");
   return (
      <>
         {state === "loading" && <div className={css.lbSpin} />}
         {src && state !== "error" && (
            <img
               className={css.lbImg}
               style={{ display: state === "ok" ? "block" : "none" }}
               alt={alt}
               src={src}
               onLoad={() => setState("ok")}
               onError={() => setState("error")}
            />
         )}
      </>
   );
}

export function App() {
   const { t } = useLocale();
   const version = store.use((s) => s.version);
   const items = store.use((s) => s.items);

   const [search, setSearch] = useState("");
   const [filter, setFilter] = useState("all");
   const [lbIndex, setLbIndex] = useState(-1);

   const all = items ?? [];
   const q = search.trim().toLowerCase();
   const filtered = all.filter((u) => {
      const okFilter = filter === "all" || u.attribute === filter;
      const okSearch = !q || (u.hero ?? "").toLowerCase().includes(q);
      return okFilter && okSearch;
   });

   const lbItem = lbIndex >= 0 && lbIndex < filtered.length ? filtered[lbIndex] : undefined;

   useEffect(() => {
      const onKeyDown = (e: KeyboardEvent) => {
         if (lbItem) {
            if (e.key === "Escape") {
               setLbIndex(-1);
               e.preventDefault();
            } else if (e.key === "ArrowLeft" && lbIndex > 0) {
               setLbIndex(lbIndex - 1);
               e.preventDefault();
            } else if (e.key === "ArrowRight" && lbIndex < filtered.length - 1) {
               setLbIndex(lbIndex + 1);
               e.preventDefault();
            }
            return;
         }
         if (e.key === "Escape") send("close");
      };
      document.addEventListener("keydown", onKeyDown);
      return () => document.removeEventListener("keydown", onKeyDown);
   }, [lbItem, lbIndex, filtered.length]);

   const loading = items === null;
   const offline = items !== null && items.length === 0;

   return (
      <>
         <Titlebar
            titleKey="shell.news.modspack.title"
            title="ModsPack"
            icon={<StarIcon />}
            badge={version}
         />

         {loading || offline ? (
            <div className={css.state}>
               {loading && <div className={css.spinner} />}
               <div>
                  {offline ? (
                     <T k="modspackUpdates.error">Couldn't load ModsPack updates — check your connection.</T>
                  ) : (
                     <T k="modspackUpdates.loading">Loading ModsPack updates…</T>
                  )}
               </div>
               {offline && (
                  <button
                     type="button"
                     data-no-drag
                     className={css.stateAction}
                     onClick={() => send("openUrl", { url: CHANGELOG_URL })}
                  >
                     <T k="whatsnew.openWeb">Open on the web</T>
                  </button>
               )}
            </div>
         ) : (
            <>
               <div className={css.controls}>
                  <div className={css.searchWrap}>
                     <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <circle cx="11" cy="11" r="7" />
                        <line x1="21" y1="21" x2="16.65" y2="16.65" />
                     </svg>
                     <input
                        className={css.searchInput}
                        type="text"
                        data-no-drag
                        placeholder={t("modspackUpdates.search", "Search hero…")}
                        autoComplete="off"
                        value={search}
                        onChange={(e) => setSearch(e.target.value)}
                     />
                  </div>
                  <div className={css.filters}>
                     {FILTERS.map((f) => (
                        <button
                           key={f.id}
                           type="button"
                           data-no-drag
                           className={`${css.chip} ${filter === f.id ? css.chipActive : ""}`}
                           title={t(f.titleKey, f.title)}
                           onClick={() => setFilter(f.id)}
                        >
                           {f.labelKey ? t(f.labelKey, f.label) : f.label}
                        </button>
                     ))}
                  </div>
               </div>

               <div className={css.grid}>
                  {filtered.length === 0 ? (
                     <div className={css.empty}>
                        <T k="modspackUpdates.noMatch">No heroes match your search.</T>
                     </div>
                  ) : (
                     filtered.map((item, i) => (
                        <Card key={`${item.hero}-${i}`} item={item} onOpen={() => setLbIndex(i)} />
                     ))
                  )}
               </div>
            </>
         )}

         {lbItem && (
            <div
               className={css.lightbox}
               onClick={(e) => {
                  if (e.target === e.currentTarget) setLbIndex(-1);
               }}
            >
               <button
                  type="button"
                  data-no-drag
                  className={css.lbClose}
                  title={t("common.close", "Close")}
                  aria-label={t("common.close", "Close")}
                  onClick={() => setLbIndex(-1)}
               >
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
                     <line x1="6" y1="6" x2="18" y2="18" />
                     <line x1="18" y1="6" x2="6" y2="18" />
                  </svg>
               </button>
               <button
                  type="button"
                  data-no-drag
                  className={css.lbNav}
                  title={t("shell.carousel.prev", "Previous")}
                  aria-label={t("shell.carousel.prev", "Previous")}
                  disabled={lbIndex <= 0}
                  onClick={() => setLbIndex(lbIndex - 1)}
               >
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                     <polyline points="15 18 9 12 15 6" />
                  </svg>
               </button>
               <button
                  type="button"
                  data-no-drag
                  className={`${css.lbNav} ${css.lbNext}`}
                  title={t("shell.carousel.next", "Next")}
                  aria-label={t("shell.carousel.next", "Next")}
                  disabled={lbIndex >= filtered.length - 1}
                  onClick={() => setLbIndex(lbIndex + 1)}
               >
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                     <polyline points="9 18 15 12 9 6" />
                  </svg>
               </button>
               <div className={css.lbStage}>
                  <div className={css.lbFrame}>
                     <LightboxImage key={lbIndex} src={lbItem.image} alt={lbItem.hero ?? ""} />
                  </div>
                  <div className={css.lbMeta}>
                     {lbItem.attribute && <span className={css.lbAttr}>{lbItem.attribute}</span>}
                     <span className={css.lbHero}>{lbItem.hero ?? ""}</span>
                     <span className={css.lbCounter}>
                        {lbIndex + 1} / {filtered.length}
                     </span>
                  </div>
               </div>
            </div>
         )}

         <Footer layout="between" className={css.footer}>
            <span className={css.count}>
               {!loading && !offline &&
                  t("modspackUpdates.count", `${filtered.length} / ${all.length} heroes`, {
                     shown: filtered.length,
                     total: all.length,
                  })}
            </span>
            <button
               type="button"
               data-no-drag
               className={css.footerLink}
               onClick={() => send("openUrl", { url: CHANGELOG_URL })}
            >
               <T k="modspackUpdates.viewAll">View all updates</T>
               <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M7 17 17 7" />
                  <polyline points="9 7 17 7 17 15" />
               </svg>
            </button>
         </Footer>
      </>
   );
}
