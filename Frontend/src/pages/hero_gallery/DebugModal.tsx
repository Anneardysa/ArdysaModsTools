import { buildDebug, clearZips, closeDebug, debugStore, generateModsPack, pickHeroesJson, pickZips, removeZip, type DebugRow } from "./debug";
import css from "./gallery.module.css";

const BUG_PATH =
   "M8 2l1.88 1.88M14.12 3.88L16 2M9 7.13v-1a3 3 0 116 0v1M12 20c-3.3 0-6-2.7-6-6v-3a4 4 0 014-4h4a4 4 0 014 4v3c0 3.3-2.7 6-6 6zM12 20v-9M6.53 9C4.6 8.8 3 7.1 3 5M6 13H2M3 21c0-2.1 1.7-3.9 3.8-4M20.97 5c0 2.1-1.6 3.8-3.5 4M22 13h-4M17.2 17c2.1.1 3.8 1.9 3.8 4";

type Entry = { path: string; row: DebugRow | null; error: string | null };
type Group = { key: string; title: string; mode: string; entries: Entry[] };

const UNRESOLVED = "__unresolved";

const LAYER_LABEL: Record<string, string> = {
   BaseHero: "base",
   Item: "item",
   CustomSet: "mix",
   Persona: "persona",
   Prismatic: "prismatic",
   AbilityEffect: "effect",
   LegacySet: "set",
};

const MODE_HINT: Record<string, string> = {
   "base wins": "Base goes first, everything else stacks over it",
   "sets win": "Base goes after sets and items and overrides them",
   mixed: "Per-set methods in heroes.json put the Base in different places",
};

function fileOf(path: string) {
   return path.split(/[\\/]/).pop() ?? path;
}

const pad = (n: number) => String(n).padStart(2, "0");

export function DebugModal() {
   const open = debugStore.use((s) => s.open);
   const zips = debugStore.use((s) => s.zips);
   const rows = debugStore.use((s) => s.rows);
   const errors = debugStore.use((s) => s.errors);
   const errorsByPath = debugStore.use((s) => s.errorsByPath);
   const warnings = debugStore.use((s) => s.warnings);
   const planning = debugStore.use((s) => s.planning);
   const heroesJson = debugStore.use((s) => s.heroesJson);
   if (!open) return null;

   const byPath = new Map(rows.map((r) => [r.path.toLowerCase(), r]));
   const errorOf = new Map(Object.entries(errorsByPath).map(([p, m]) => [p.toLowerCase(), m]));
   const groups: Group[] = [];
   for (const path of zips) {
      const row = byPath.get(path.toLowerCase()) ?? null;
      const key = row ? row.heroId : UNRESOLVED;
      let g = groups.find((x) => x.key === key);
      if (!g) {
         g = { key, title: row ? row.heroName : planning ? "Checking" : "Not added", mode: row?.mode ?? "", entries: [] };
         groups.push(g);
      }
      g.entries.push({ path, row, error: errorOf.get(path.toLowerCase()) ?? null });
   }
   for (const g of groups) g.entries.sort((a, b) => (a.row?.order ?? 0) - (b.row?.order ?? 0));
   groups.sort((a, b) => Number(a.key === UNRESOLVED) - Number(b.key === UNRESOLVED));

   const inline = new Set(Object.entries(errorsByPath).map(([p, m]) => `${fileOf(p)}: ${m}`));
   const planErrors = errors.filter((e) => !inline.has(e));
   const heroCount = groups.filter((g) => g.key !== UNRESOLVED).length;
   const canBuild = zips.length > 0 && errors.length === 0 && !planning;
   const blockedBy =
      zips.length === 0
         ? "Add at least one zip"
         : planning
           ? "Still checking the zips"
           : errors.length > 0
             ? "Fix or remove the zips marked in red"
             : "Build every zip and install into game/_ArdysaMods";

   return (
      <div className={css.ov} role="dialog" aria-modal="true" aria-labelledby="debugTitle">
         <div className={css.ovScrim} onClick={closeDebug} />
         <div className={`${css.dialog} ${css.debugDialog}`}>
            <div className={css.dialogBody}>
               <div className={`${css.dialogIco} ${css.debugIco}`}>
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <path d={BUG_PATH} />
                  </svg>
               </div>
               <div className={css.dialogMain}>
                  <h3 id="debugTitle" className={css.dialogTitle}>
                     Skin Tester <span className={css.debugTag}>Debug</span>
                  </h3>
                  <div className={css.dialogMsg}>
                     Local zips through the real Skin Selector pipeline into <strong>game/_ArdysaMods</strong>. Nothing is published, no quota is used.
                  </div>

                  <div className={css.debugField}>
                     <span className={css.debugLabel}>heroes.json</span>
                     <span className={css.debugPath} title={heroesJson}>{heroesJson || "(none)"}</span>
                     <button type="button" data-no-drag className={`${css.btn} ${css.ghost} ${css.debugSmall}`} onClick={pickHeroesJson}>
                        Change
                     </button>
                  </div>

                  {zips.length === 0 ? (
                     <button type="button" data-no-drag className={`${css.debugDrop} ${css.debugDropEmpty}`} onClick={pickZips}>
                        <span className={css.debugDropTitle}>+ Add .zip files</span>
                        <span className={css.debugDropHint}>Each zip lands on the hero its index.txt names. The file prefix sets the layer:</span>
                        <span className={css.debugDropPrefixes}>base_ item_ mix_ persona_ prismatic_ effect_</span>
                     </button>
                  ) : (
                     <>
                        <div className={css.debugListHead}>
                           <span>Build order per hero. The last row wins files and item ids it shares with rows above it.</span>
                           <button type="button" data-no-drag className={`${css.btn} ${css.ghost} ${css.debugSmall}`} onClick={pickZips}>
                              + Add zips
                           </button>
                        </div>
                        <div className={css.debugList} aria-busy={planning}>
                           {groups.map((g) => (
                              <section key={g.key} className={css.debugGroup} aria-label={g.title}>
                                 <div className={css.debugGroupHead}>
                                    <span className={css.debugGroupName}>{g.title}</span>
                                    {g.mode && (
                                       <span className={css.debugMode} title={MODE_HINT[g.mode] ?? g.mode}>
                                          {g.mode}
                                       </span>
                                    )}
                                    <span className={css.debugCount}>{g.entries.length}</span>
                                 </div>
                                 <ol className={css.debugStack}>
                                    {g.entries.map(({ path, row, error }) => (
                                       <li key={path} className={`${css.debugRow} ${row?.top ? css.debugRowTop : ""} ${error ? css.debugRowBad : ""}`}>
                                          <span className={css.debugOrd} aria-label={row ? `applied ${row.order} of ${g.entries.length}` : undefined}>
                                             {row ? pad(row.order) : "--"}
                                          </span>
                                          <span className={`${css.debugChip} ${row ? "" : error ? css.debugChipBad : css.debugChipWait}`}>
                                             {row ? LAYER_LABEL[row.layer] ?? row.layer : error ? "error" : "checking"}
                                          </span>
                                          <div className={css.debugRowMain}>
                                             <div className={css.debugFile}>
                                                {row?.file ?? fileOf(path)}
                                                {row?.top && g.entries.length > 1 && <span className={css.debugTop}>Top</span>}
                                             </div>
                                             {error ? (
                                                <div className={css.debugRowError}>{error}</div>
                                             ) : (
                                                <div className={css.debugRowPath} title={path}>
                                                   {path}
                                                </div>
                                             )}
                                          </div>
                                          <button
                                             type="button"
                                             data-no-drag
                                             className={css.debugRemove}
                                             onClick={() => removeZip(path)}
                                             aria-label={`Remove ${fileOf(path)}`}
                                             title="Remove"
                                          >
                                             ×
                                          </button>
                                       </li>
                                    ))}
                                 </ol>
                              </section>
                           ))}
                        </div>
                     </>
                  )}

                  {warnings.length > 0 && (
                     <div className={css.debugWarnings} role="status">
                        {warnings.map((w, i) => (
                           <div key={i}>{w}</div>
                        ))}
                     </div>
                  )}

                  {planErrors.length > 0 && (
                     <div className={css.debugErrors} role="alert">
                        {planErrors.map((e, i) => (
                           <div key={i}>{e}</div>
                        ))}
                     </div>
                  )}
               </div>
            </div>
            <div className={css.dialogActions}>
               <button type="button" data-no-drag className={`${css.btn} ${css.ghost} ${css.actionsLead}`} onClick={clearZips} disabled={zips.length === 0}>
                  Clear
               </button>
               <span className={css.debugSummary}>
                  {zips.length} zip{zips.length === 1 ? "" : "s"} · {heroCount} hero{heroCount === 1 ? "" : "es"}
               </span>
               <button type="button" data-no-drag className={`${css.btn} ${css.ghost}`} onClick={closeDebug}>
                  Close
               </button>
               <span title="The gallery's current selections as a ModsPack release: a new folder in F:\Projects\ModsPack\output\modspack with the slim pak01_dir.vpk, ModsPack.zip, ModsPack.hash and modspack_index.txt, plus the full package installed in game/_ArdysaMods for testing (removes the game/mod package). The Skin Selector preset rides along in version/_ardysamods. Every selected set must build; encrypted sets ship decrypted (noted in the run log).">
                  <button type="button" data-no-drag className={`${css.btn} ${css.ghost}`} onClick={generateModsPack}>
                     Generate ModsPack
                  </button>
               </span>
               <span title={blockedBy}>
                  <button type="button" data-no-drag className={`${css.btn} ${css.primary}`} onClick={buildDebug} disabled={!canBuild}>
                     {planning ? "Checking" : "Build + install"}
                  </button>
               </span>
            </div>
         </div>
      </div>
   );
}
