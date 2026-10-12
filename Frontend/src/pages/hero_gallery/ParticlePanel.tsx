import { useEffect, useMemo, useRef, useState, type KeyboardEvent } from "react";
import { useLocale } from "../../bridge/i18n";
import { trapTab } from "./a11y";
import { MAX_PARTICLES, canCarryParticles, findParticle, getHeroSelection, getSetDisplayName, isPersonaActive, particleCarriers } from "./helpers";
import { closeParticlePicker, openParticlePicker, particleSlotsKey, removeParticle, requestParticleSlots, setParticle, store } from "./store";
import type { Hero, HeroSelectionState, ParticleEntry, ParticlePick } from "./types";
import { NoticeBadge } from "./SetTile";
import css from "./gallery.module.css";

function PreviewStage({ src, name }: { src?: string | null; name: string }) {
   const { t } = useLocale();
   const [state, setState] = useState<"loading" | "ready" | "missing">(src ? "loading" : "missing");
   const [settled, setSettled] = useState(false);
   useEffect(() => {
      const timer = window.setTimeout(() => setSettled(true), 200);
      return () => window.clearTimeout(timer);
   }, []);
   const [paused, setPaused] = useState(() => window.matchMedia?.("(prefers-reduced-motion: reduce)").matches ?? false);
   const img = useRef<HTMLImageElement | null>(null);
   const still = useRef<HTMLCanvasElement | null>(null);

   useEffect(() => {
      const from = img.current, to = still.current;
      if (!paused || state !== "ready" || !from || !to) return;
      to.width = from.naturalWidth;
      to.height = from.naturalHeight;
      to.getContext("2d")?.drawImage(from, 0, 0);
   }, [paused, state]);

   return (
      <div className={css.ptStage}>
         {src && settled && state !== "missing" && (
            <img
               ref={img}
               src={src}
               alt={t("heroGallery.particles.previewAlt", `Preview of ${name}`, { name })}
               decoding="async"
               className={paused && state === "ready" ? css.ptHidden : undefined}
               onLoad={() => setState("ready")}
               onError={() => setState("missing")}
            />
         )}
         {paused && state === "ready" && <canvas ref={still} className={css.ptStill} aria-hidden="true" />}
         {state === "loading" && <span className={css.ptStageNote}>{t("heroGallery.particles.previewLoading", "Loading preview…")}</span>}
         {state === "missing" && (
            <span className={css.ptStageNote}>
               <span className={css.fxMark} aria-hidden="true" />
               {t("heroGallery.particles.previewMissing", "No preview yet")}
            </span>
         )}
         {state === "ready" && (
            <button type="button" data-no-drag className={css.ptPlay} onClick={() => setPaused((p) => !p)}>
               {paused ? t("heroGallery.particles.play", "Play") : t("heroGallery.particles.pause", "Pause")}
            </button>
         )}
      </div>
   );
}

function useOriginLabel() {
   const { t } = useLocale();
   return (p: ParticleEntry | undefined): string => {
      if (p?.source === "courier") return t("heroGallery.particles.source.courier", "Courier");
      const unusual = t("heroGallery.particles.source.unusual", "Unusual");
      return p?.pools ? `${unusual} · ${p.pools}` : unusual;
   };
}

function useFitLabel() {
   const { t } = useLocale();
   return (fit: ParticleEntry["fit"]): string | null => {
      switch (fit) {
         case "model": return t("heroGallery.particles.fit.model", "On model");
         case "ground": return t("heroGallery.particles.fit.ground", "Ground");
         case "hang": return t("heroGallery.particles.fit.hang", "Floats");
         default: return null;
      }
   };
}

function useFitReason() {
   const { t } = useLocale();
   return (fit: ParticleEntry["fit"]): string => {
      switch (fit) {
         case "ground": return t("heroGallery.particles.fitWhy.ground", "Sits at the item's origin: a ring or glow at the hero's feet.");
         case "hang": return t("heroGallery.particles.fitWhy.hang", "Tuned to the model it was made for, so it may float or not show on this item.");
         default: return t("heroGallery.particles.fitWhy.model", "Sits on the surface of the item it is added to.");
      }
   };
}

const hostLabel = (hero: Hero, host: string) => hero.particleHosts?.find((h) => h.id === host)?.label ?? host;

export function ParticlePanel({ hero, heroSel }: { hero: Hero; heroSel: HeroSelectionState }) {
   const { t } = useLocale();
   const originLabel = useOriginLabel();
   const catalog = store.use((s) => s.particles);
   const picks = heroSel.particles ?? [];
   const notice = isPersonaActive(hero, heroSel)
      ? t("heroGallery.particles.persona", "⚠ Particles are disabled while a persona is active")
      : !canCarryParticles(hero, heroSel)
         ? t("heroGallery.particles.needLayer", "⚠ Pick a set, item or arcana first. Particles ride on a worn item.")
         : null;

   return (
      <div className={css.setCategorySection}>
         <div className={css.setCategoryHeader}>
            <span className={css.categoryLabel}>{t("heroGallery.particles.title", "Particles")}</span>
            <span className={css.ptFpsNotice}>{t("heroGallery.particles.fpsNotice", "Unusual particles can lower your FPS.")}</span>
            <div className={css.categoryLine} />
            <span className={css.categoryCount}>
               {picks.length}/{MAX_PARTICLES}
            </span>
         </div>
         {notice && <div className={css.exclusionNotice}>{notice}</div>}
         <div className={css.ptList}>
            {picks.map((pick, slot) => {
               const entry = findParticle(catalog, pick.id);
               const name = entry?.name ?? pick.id;
               return (
                  <div key={`${slot}:${pick.id}`} className={css.ptRow}>
                     <span className={css.fxMark} aria-hidden="true" />
                     <div className={css.ptText}>
                        <span className={css.ptName} title={name}>{name}</span>
                        <span className={css.ptSource}>{originLabel(entry)} · {hostLabel(hero, pick.host)}</span>
                     </div>
                     <button type="button" data-no-drag className={`${css.btn} ${css.ghost} ${css.ptAction}`} onClick={() => openParticlePicker(hero, slot)}>
                        {t("heroGallery.particles.change", "Change")}
                     </button>
                     <button
                        type="button"
                        data-no-drag
                        className={`${css.btn} ${css.ghost} ${css.ptAction}`}
                        aria-label={t("heroGallery.particles.removeNamed", `Remove ${name}`, { name })}
                        onClick={() => removeParticle(hero, slot)}
                     >
                        {t("heroGallery.particles.remove", "Remove")}
                     </button>
                  </div>
               );
            })}
            {picks.length < MAX_PARTICLES && (
               <button
                  type="button"
                  data-no-drag
                  className={`${css.btn} ${css.ptAdd}`}
                  disabled={notice != null}
                  onClick={() => openParticlePicker(hero, picks.length)}
               >
                  {picks.length === 0
                     ? t("heroGallery.particles.add", "Add particle")
                     : t("heroGallery.particles.addSecond", "Add second particle")}
               </button>
            )}
         </div>
      </div>
   );
}

export function ParticlePicker() {
   const picker = store.use((s) => s.particlePicker);
   const modalHeroId = store.use((s) => s.modalHeroId);
   const heroes = store.use((s) => s.heroes);
   const selections = store.use((s) => s.selections);
   const catalog = store.use((s) => s.particles);
   const hero = picker && picker.heroId === modalHeroId ? heroes.find((h) => h.id === picker.heroId) : undefined;
   if (!picker || !hero) return null;
   const heroSel = getHeroSelection(selections, hero.id);
   return <PickerDialog key={`${hero.id}:${picker.slot}`} hero={hero} heroSel={heroSel} slot={picker.slot} picks={heroSel.particles ?? []} catalog={catalog} />;
}

const optionId = (id: string) => `ptOpt-${id.replace(/[^A-Za-z0-9_-]/g, "_")}`;

function PickerDialog({ hero, heroSel, slot, picks, catalog }: { hero: Hero; heroSel: HeroSelectionState; slot: number; picks: ParticlePick[]; catalog: ParticleEntry[] }) {
   const { t } = useLocale();
   const originLabel = useOriginLabel();
   const fitLabel = useFitLabel();
   const fitWhy = useFitReason();
   const current = picks[slot];

   const carriers = particleCarriers(hero, heroSel);
   const [asset, setAsset] = useState<number | undefined>(() => {
      const cache = store.get().particleSlots;
      const own = current && carriers.find((c) => {
         const l = cache[particleSlotsKey(hero.id, c.idx)];
         return typeof l === "object" && l.slots.some((s) => s.id === current.host);
      });
      return (own ?? carriers[0])?.idx;
   });
   const [chosen, setChosen] = useState(current?.host ?? "");
   const kind = carriers.find((c) => c.idx === asset)?.kind;
   const assetName = asset != null ? getSetDisplayName(hero.sets[asset], asset) : "";
   const loaded = store.use((s) => (asset != null ? s.particleSlots[particleSlotsKey(hero.id, asset)] : undefined));
   useEffect(() => {
      if (asset != null) requestParticleSlots(hero, asset);
   }, [hero, asset]);
   const slots = typeof loaded === "object" ? loaded.slots : [];
   const auto = kind !== "set" && typeof loaded === "object" ? loaded.preferred : null;
   const target = slots.find((s) => s.id === (auto ?? chosen)) ?? (auto == null ? slots[0] : undefined);
   const host = target?.id ?? "";
   const slotText = (s: { label: string; hasModel: boolean }) =>
      s.hasModel ? s.label : `${s.label} (${t("heroGallery.particles.noModel", "may not render")})`;

   const [query, setQuery] = useState("");
   const list = useMemo(() => {
      const q = query.trim().toLowerCase();
      return q ? catalog.filter((p) => p.name.toLowerCase().includes(q) || (p.pools ?? "").toLowerCase().includes(q)) : catalog;
   }, [catalog, query]);
   const [pickedId, setPickedId] = useState(current?.id ?? catalog[0]?.id ?? "");
   const picked = list.find((p) => p.id === pickedId) ?? list[0];
   const takenHere = new Set(picks.filter((p, i) => i !== slot && p.host === host).map((p) => p.id));
   const taken = picked != null && takenHere.has(picked.id);
   const canUse = picked != null && host !== "" && !taken;

   const listRef = useRef<HTMLUListElement | null>(null);
   useEffect(() => {
      if (picked) document.getElementById(optionId(picked.id))?.scrollIntoView({ block: "nearest" });
   }, [picked]);

   const opener = useRef<HTMLElement | null>(document.activeElement as HTMLElement | null);
   useEffect(() => () => {
      if (opener.current?.isConnected) opener.current.focus();
   }, []);

   const use = (p: ParticleEntry | undefined) => {
      if (p && host && !takenHere.has(p.id) && setParticle(hero, slot, p.id, host)) closeParticlePicker();
   };
   const move = (e: KeyboardEvent<HTMLElement>) => {
      if (list.length === 0) return;
      const at = Math.max(0, list.findIndex((p) => p.id === picked?.id));
      const inList = e.currentTarget === listRef.current;
      const next =
         e.key === "ArrowDown" ? Math.min(list.length - 1, at + 1)
         : e.key === "ArrowUp" ? Math.max(0, at - 1)
         : e.key === "PageDown" ? Math.min(list.length - 1, at + 8)
         : e.key === "PageUp" ? Math.max(0, at - 8)
         : e.key === "Home" && inList ? 0
         : e.key === "End" && inList ? list.length - 1
         : null;
      if (next != null) {
         e.preventDefault();
         setPickedId(list[next]!.id);
      } else if (e.key === "Enter") {
         e.preventDefault();
         use(picked);
      }
   };

   const title = current
      ? t("heroGallery.particles.pickerChange", `Change particle ${slot + 1}`, { n: slot + 1 })
      : t("heroGallery.particles.pickerAdd", "Add particle");
   const pickedFit = picked ? fitLabel(picked.fit) : null;
   const slotStatus =
      loaded === "loading" || loaded === undefined
         ? t("heroGallery.particles.slotsLoading", "Reading the slots…")
         : loaded === "failed"
            ? t("heroGallery.particles.slotsFailed", `Could not read the slots of ${assetName}.`, { asset: assetName })
            : slots.length === 0
               ? t("heroGallery.particles.noSlots", `${assetName} has no slot a particle can go on. Pick another one.`, { asset: assetName })
               : null;

   return (
      <div className={css.ov}>
         <div className={css.ovScrim} onClick={closeParticlePicker} />
         <div className={`${css.dialog} ${css.ptDialog}`} role="dialog" aria-modal="true" aria-labelledby="ptPickerTitle" onKeyDown={trapTab}>
            <h3 id="ptPickerTitle" className={`${css.dialogTitle} ${css.ptTitle}`}>
               {title}
               <NoticeBadge lines={[t("heroGallery.particles.fpsNotice", "Unusual particles can lower your FPS.")]} className={css.noticeBadgeInline} />
            </h3>

            <div className={css.ptTarget}>
               <label className={css.ptField}>
                  <span className={css.ptFieldLabel}>{t("heroGallery.particles.on", "On")}</span>
                  <select className={css.ptSelect} data-no-drag value={asset ?? ""} onChange={(e) => setAsset(Number(e.target.value))}>
                     {carriers.map((c) => (
                        <option key={c.idx} value={c.idx}>
                           {getSetDisplayName(hero.sets[c.idx], c.idx)}
                        </option>
                     ))}
                  </select>
               </label>
               {slotStatus == null && (
                  auto != null && target ? (
                     <span className={css.ptField}>
                        <span className={css.ptFieldLabel}>{t("heroGallery.particles.slot", "Slot")}</span>
                        <span className={css.ptFixed}>{slotText(target)}</span>
                     </span>
                  ) : (
                     <label className={css.ptField}>
                        <span className={css.ptFieldLabel}>{t("heroGallery.particles.slot", "Slot")}</span>
                        <select className={css.ptSelect} data-no-drag value={host} onChange={(e) => setChosen(e.target.value)}>
                           {slots.map((s) => (
                              <option key={s.id} value={s.id}>{slotText(s)}</option>
                           ))}
                        </select>
                     </label>
                  )
               )}
               <span className={css.ptStatus} role="status">
                  {slotStatus}
                  {loaded === "failed" && asset != null && (
                     <button type="button" data-no-drag className={`${css.btn} ${css.ghost} ${css.ptAction}`} onClick={() => requestParticleSlots(hero, asset)}>
                        {t("heroGallery.particles.retry", "Try again")}
                     </button>
                  )}
               </span>
            </div>

            <div className={css.ptBody}>
               <div className={css.ptBrowse}>
                  <div className={css.ptSearchRow}>
                     <input
                        type="text"
                        role="combobox"
                        aria-expanded="true"
                        aria-controls="ptListbox"
                        aria-activedescendant={picked ? optionId(picked.id) : undefined}
                        aria-autocomplete="list"
                        className={css.ptSearch}
                        data-no-drag
                        autoFocus
                        value={query}
                        placeholder={t("heroGallery.particles.search", "Search effects")}
                        aria-label={t("heroGallery.particles.search", "Search effects")}
                        onChange={(e) => setQuery(e.target.value)}
                        onKeyDown={move}
                     />
                     <span className={css.ptCount} aria-live="polite">
                        {query.trim()
                           ? t("heroGallery.particles.shown", `${list.length} of ${catalog.length}`, { shown: list.length, total: catalog.length })
                           : t("heroGallery.particles.total", `${catalog.length} effects`, { total: catalog.length })}
                     </span>
                  </div>

                  {catalog.length === 0 ? (
                     <p className={css.ptEmpty}>
                        {t("heroGallery.particles.noCatalog", "The particle list did not load. Close and reopen the Skin Selector.")}
                     </p>
                  ) : list.length === 0 ? (
                     <div className={css.ptEmpty}>
                        <p>{t("heroGallery.particles.noMatch", `No particle matches "${query}"`, { query })}</p>
                        <button type="button" data-no-drag className={`${css.btn} ${css.ghost} ${css.ptAction}`} onClick={() => setQuery("")}>
                           {t("heroGallery.particles.clearSearch", "Clear search")}
                        </button>
                     </div>
                  ) : (
                     <ul
                        ref={listRef}
                        id="ptListbox"
                        role="listbox"
                        tabIndex={0}
                        aria-label={t("heroGallery.particles.title", "Particles")}
                        aria-activedescendant={picked ? optionId(picked.id) : undefined}
                        className={css.ptListbox}
                        onKeyDown={move}
                     >
                        {list.map((p) => {
                           const isPicked = p.id === picked?.id;
                           const notes = [
                              current?.id === p.id && current.host === host ? t("heroGallery.particles.current", "Current") : null,
                              takenHere.has(p.id) ? t("heroGallery.particles.taken", "Already on this slot") : null,
                           ].filter(Boolean);
                           return (
                              <li
                                 key={p.id}
                                 id={optionId(p.id)}
                                 role="option"
                                 aria-selected={isPicked}
                                 aria-disabled={takenHere.has(p.id) || undefined}
                                 className={`${css.ptOpt} ${isPicked ? css.selected : ""}`}
                                 onClick={() => setPickedId(p.id)}
                                 onDoubleClick={() => use(p)}
                              >
                                 <span className={css.ptOptName}>{p.name}</span>
                                 <span className={css.ptOptMeta}>
                                    {[...notes, p.thumbnail ? t("heroGallery.particles.preview", "Preview") : null, originLabel(p)].filter(Boolean).join(" · ")}
                                 </span>
                              </li>
                           );
                        })}
                     </ul>
                  )}
               </div>

               <section className={css.ptPreview} aria-label={t("heroGallery.particles.preview", "Preview")}>
                  {picked && (
                     <>
                        <PreviewStage key={picked.id} src={picked.thumbnail} name={picked.name} />
                        <h4 className={css.ptPreviewName}>{picked.name}</h4>
                        <p className={css.ptPreviewMeta}>{originLabel(picked)}</p>
                        <dl className={css.ptFacts}>
                           {pickedFit && (
                              <div>
                                 <dt>{pickedFit}</dt>
                                 <dd>{fitWhy(picked.fit)}</dd>
                              </div>
                           )}
                           {picked.courier && (
                              <div>
                                 <dt>{t("heroGallery.particles.courierBadge", "Courier")}</dt>
                                 <dd>{t("heroGallery.particles.courierNote", "Courier effects were made for couriers and may sit off-center on a hero.")}</dd>
                              </div>
                           )}
                           {target && !target.hasModel && (
                              <div>
                                 <dt>{target.label}</dt>
                                 <dd>{t("heroGallery.particles.noModelWhy", `The ${target.label} has no model, so the effect may not render.`, { slot: target.label })}</dd>
                              </div>
                           )}
                        </dl>
                     </>
                  )}
               </section>
            </div>

            <div className={`${css.dialogActions} ${css.ptActions}`}>
               <span className={`${css.actionsLead} ${css.ptHint}`}>{t("heroGallery.particles.hint", "Enter or double-click also uses the selected effect.")}</span>
               <button type="button" data-no-drag className={`${css.btn} ${css.ghost}`} onClick={closeParticlePicker}>
                  {t("common.cancel", "Cancel")}
               </button>
               <button type="button" data-no-drag className={`${css.btn} ${css.primary}`} disabled={!canUse} onClick={() => use(picked)}>
                  {taken
                     ? t("heroGallery.particles.taken", "Already on this slot")
                     : target
                        ? t("heroGallery.particles.useOn", `Use on ${target.label}`, { slot: target.label })
                        : t("heroGallery.particles.pickSlot", "Pick a slot first")}
               </button>
            </div>
         </div>
      </div>
   );
}
