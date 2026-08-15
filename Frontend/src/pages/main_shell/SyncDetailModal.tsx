import { useMemo, useState, useEffect } from "react";
import { send } from "../../bridge/host";
import { T, useLocale } from "../../bridge/i18n";
import { closeSyncDetailsModal, pushToast, store } from "./store";
import type { SyncItemDetail } from "./types";
import css from "./shell.module.css";

export function SyncDetailModal() {
   const { t } = useLocale();
   const open = store.use((s) => s.syncDetailsModalOpen);
   const syncDetails = store.use((s) => s.syncDetails);
   const syncDetailText = store.use((s) => s.syncDetail);
   const syncNeedsFix = store.use((s) => s.syncNeedsFix);

   const [activeTab, setActiveTab] = useState<"all" | "new" | "modified" | "error">("all");
   const [searchQuery, setSearchQuery] = useState("");
   const [copiedList, setCopiedList] = useState(false);
   const [copiedCardId, setCopiedCardId] = useState<string | null>(null);

   useEffect(() => {
      if (open) {
         send("getSyncDetails");
      }
   }, [open]);

   const rawItems: SyncItemDetail[] = useMemo(() => {
      if (syncDetails?.items && syncDetails.items.length > 0) {
         return syncDetails.items;
      }

      const fallback: SyncItemDetail[] = [];
      const match = (syncDetailText ?? "").match(/(\d+)\s+items?/i);
      const addedCount = match && match[1] ? parseInt(match[1], 10) : 0;

      if (addedCount > 0) {
         fallback.push({
            id: `+${addedCount} Items`,
            name: `${addedCount} New item definitions in Dota 2 update`,
            category: "Game Update",
            status: "new",
            description: "Items present in vanilla Dota 2 pak01_dir.vpk that need to be merged into mod package.",
         });
      }

      fallback.push(
         { id: "555", name: "Default Weather", category: "Weather", status: "modified", description: "Miscellaneous Weather modification" },
         { id: "590", name: "Default Terrain", category: "Terrain", status: "modified", description: "Miscellaneous Map/Terrain modification" },
         { id: "588", name: "Default Music", category: "Music", status: "modified", description: "Miscellaneous Music Pack modification" },
         { id: "587", name: "Default Hud Skin", category: "HUD", status: "modified", description: "Miscellaneous HUD modification" },
         { id: "11173", name: "Default Announcer", category: "Announcer", status: "modified", description: "Miscellaneous Announcer modification" },
         { id: "586", name: "Default Mega-Kill Announcer", category: "Announcer", status: "modified", description: "Miscellaneous Mega-Kill Announcer modification" },
         { id: "801", name: "Default Roshan", category: "Roshan", status: "modified", description: "Miscellaneous Roshan skin modification" },
         { id: "202", name: "Default Cursor Pack", category: "Cursor", status: "modified", description: "Miscellaneous Cursor pack modification" }
      );

      return fallback;
   }, [syncDetails, syncDetailText]);

   const filteredItems = useMemo(() => {
      let list = rawItems;
      if (activeTab === "new") list = list.filter((it) => it.status === "new");
      else if (activeTab === "modified") list = list.filter((it) => it.status === "modified");
      else if (activeTab === "error") list = list.filter((it) => it.status === "error");

      if (searchQuery.trim()) {
         const q = searchQuery.toLowerCase().trim();
         list = list.filter(
            (it) =>
               it.id.toLowerCase().includes(q) ||
               it.name.toLowerCase().includes(q) ||
               it.category.toLowerCase().includes(q) ||
               (it.description && it.description.toLowerCase().includes(q))
         );
      }

      return list;
   }, [rawItems, activeTab, searchQuery]);

   const addedCount = useMemo(() => syncDetails?.addedCount ?? rawItems.filter((x) => x.status === "new").length, [syncDetails, rawItems]);
   const modifiedCount = useMemo(() => syncDetails?.modifiedCount ?? rawItems.filter((x) => x.status === "modified").length, [syncDetails, rawItems]);
   const errorCount = useMemo(() => syncDetails?.errorCount ?? rawItems.filter((x) => x.status === "error").length, [syncDetails, rawItems]);

   if (!open) return null;

   const fix = () => {
      closeSyncDetailsModal();
      send("fixPackageSync");
   };

   const executeCopy = (text: string) => {
      send("copyConsole", { text });
      try {
         if (navigator?.clipboard?.writeText) {
            navigator.clipboard.writeText(text).catch(() => {});
         }
      } catch {}
   };

   const copyAll = () => {
      if (!filteredItems || filteredItems.length === 0) {
         pushToast({
            title: t("shell.toast.empty", "Nothing to copy"),
            message: t("verify.sync.noMatches", "No matching item definitions found."),
            variant: "info",
            timeout: 2500,
         });
         return;
      }

      const formatted = filteredItems
         .map((it) => {
            const desc = it.description ? ` - ${it.description}` : "";
            return `[${it.status.toUpperCase()}] #${it.id} | ${it.category} | ${it.name}${desc}`;
         })
         .join("\r\n");

      executeCopy(formatted);
      setCopiedList(true);
      pushToast({
         title: t("shell.toast.copied", "Copied"),
         message: `${filteredItems.length} items copied to clipboard.`,
         variant: "info",
         timeout: 2500,
      });
      setTimeout(() => setCopiedList(false), 2000);
   };

   const copyIdOnly = (id: string, e: React.MouseEvent) => {
      e.stopPropagation();
      executeCopy(id);

      setCopiedCardId(id);
      pushToast({
         title: t("shell.toast.copied", "Copied"),
         message: `ID: #${id}`,
         variant: "info",
         timeout: 2000,
      });
      setTimeout(() => setCopiedCardId(null), 2000);
   };

   return (
      <div
         id="sync-details-modal"
         className={`${css.modalOverlay} ${css.show}`}
         onClick={(e) => {
            if (e.target === e.currentTarget) closeSyncDetailsModal();
         }}
      >
         <div className={`${css.modal} ${css.syncDetailModal}`} role="dialog" aria-modal="true" aria-labelledby="sync-details-heading">
            <div className={css.modalHead}>
               <div className={css.modalTitle}>
                  <span className={css.led} />
                  <span id="sync-details-heading">
                     <T k="verify.sync.details.title">Package Sync Details & Preview</T>
                  </span>
               </div>
               <button type="button" data-no-drag className={css.modalX} title={t("shell.titlebar.close", "Close")} onClick={closeSyncDetailsModal}>
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
                     <line x1="6" y1="6" x2="18" y2="18" />
                     <line x1="18" y1="6" x2="6" y2="18" />
                  </svg>
               </button>
            </div>

            <div className={css.syncDetailBody}>
               <div className={css.syncDetailHeaderInfo}>
                  <div className={css.syncDetailSub}>
                     <T k="verify.sync.details.sub">
                        Breakdown of items_game.txt item definitions between vanilla Dota 2 and your installed mod package.
                     </T>
                  </div>
                  <div className={css.syncDetailMetaBadge} title="Total items currently analyzed">
                     <span className={css.led} />
                     <span>{filteredItems.length} / {rawItems.length} Items</span>
                  </div>
               </div>

               <div className={css.syncTabRow}>
                  <button
                     type="button"
                     data-no-drag
                     className={`${css.syncTab} ${activeTab === "all" ? css.active : ""}`}
                     onClick={() => setActiveTab("all")}
                  >
                     <span><T k="common.all">All</T></span>
                     <span className={css.syncTabBadge}>{rawItems.length}</span>
                  </button>
                  <button
                     type="button"
                     data-no-drag
                     className={`${css.syncTab} ${css.tabNew} ${activeTab === "new" ? css.active : ""}`}
                     onClick={() => setActiveTab("new")}
                  >
                     <span><T k="verify.sync.tab.new">New in Game</T></span>
                     <span className={`${css.syncTabBadge} ${css.badgeNew}`}>{addedCount}</span>
                  </button>
                  <button
                     type="button"
                     data-no-drag
                     className={`${css.syncTab} ${css.tabMod} ${activeTab === "modified" ? css.active : ""}`}
                     onClick={() => setActiveTab("modified")}
                  >
                     <span><T k="verify.sync.tab.modified">Modified Mods</T></span>
                     <span className={`${css.syncTabBadge} ${css.badgeMod}`}>{modifiedCount}</span>
                  </button>
                  <button
                     type="button"
                     data-no-drag
                     className={`${css.syncTab} ${css.tabErr} ${activeTab === "error" ? css.active : ""}`}
                     onClick={() => setActiveTab("error")}
                  >
                     <span><T k="verify.sync.tab.errors">Errors</T></span>
                     <span className={`${css.syncTabBadge} ${css.badgeErr}`}>{errorCount}</span>
                  </button>
               </div>

               <div className={css.syncSearchWrap}>
                  <svg className={css.syncSearchIco} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                     <circle cx="11" cy="11" r="8" />
                     <line x1="21" y1="21" x2="16.65" y2="16.65" />
                  </svg>
                  <input
                     type="text"
                     data-no-drag
                     className={css.syncSearchInput}
                     placeholder={t("verify.sync.searchPlaceholder", "Search by ID, hero, or category (e.g. 555, weather, axe)...")}
                     value={searchQuery}
                     onChange={(e) => setSearchQuery(e.target.value)}
                  />
                  {searchQuery && (
                     <span className={css.syncSearchCount}>{filteredItems.length} matches</span>
                  )}
                  {searchQuery && (
                     <button type="button" className={css.syncSearchClear} onClick={() => setSearchQuery("")} title="Clear">
                        ✕
                     </button>
                  )}
               </div>

               <div className={css.syncItemList}>
                  {filteredItems.length === 0 ? (
                     <div className={css.syncEmptyState}>
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                           <circle cx="12" cy="12" r="10" />
                           <line x1="8" y1="12" x2="16" y2="12" />
                        </svg>
                        <span><T k="verify.sync.noMatches">No matching item definitions found.</T></span>
                        {searchQuery && (
                           <button
                              type="button"
                              className={`${css.obBtn} ${css.ghost} ${css.clearSearch}`}
                              onClick={() => setSearchQuery("")}
                           >
                              Clear Search Filter
                           </button>
                        )}
                     </div>
                  ) : (
                     filteredItems.map((it) => (
                        <div
                           key={`${it.id}-${it.status}-${it.category}`}
                           className={`${css.syncItemCard} ${css[it.status] ?? ""}`}
                        >
                           <div className={css.syncItemHeader}>
                              <button
                                 type="button"
                                 data-no-drag
                                 className={`${css.syncItemIdBtn} ${copiedCardId === it.id ? css.copied : ""}`}
                                 title={t("verify.sync.copyIdOnly", "Click to copy ID only")}
                                 onClick={(e) => copyIdOnly(it.id, e)}
                              >
                                 {copiedCardId === it.id ? (
                                    <>
                                       <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ width: 10, height: 10, color: "#3fb950" }}>
                                          <polyline points="20 6 9 17 4 12" />
                                       </svg>
                                       <span>Copied</span>
                                    </>
                                 ) : (
                                    `#${it.id}`
                                 )}
                              </button>
                              <span className={css.syncItemCategory}>{it.category}</span>
                              <span className={`${css.syncItemStatus} ${css[it.status] ?? ""}`}>
                                 {it.status === "new" && t("verify.sync.status.new", "New in Game")}
                                 {it.status === "modified" && t("verify.sync.status.modified", "Mod Applied")}
                                 {it.status === "error" && t("verify.sync.status.error", "Error")}
                              </span>
                           </div>
                           <div className={css.syncItemName}>{it.name}</div>
                           {it.description && <div className={css.syncItemDesc}>{it.description}</div>}
                        </div>
                     ))
                  )}
               </div>
            </div>

            <div className={css.syncDetailFooter}>
               <div className={css.syncFooterLeft}>
                  <button
                     type="button"
                     data-no-drag
                     className={`${css.obBtn} ${css.ghost} ${copiedList ? css.copied : ""}`}
                     onClick={copyAll}
                     title="Copy item list to clipboard"
                  >
                     {copiedList ? (
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ width: 13, height: 13, color: "#3fb950" }}>
                           <polyline points="20 6 9 17 4 12" />
                        </svg>
                     ) : (
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" style={{ width: 13, height: 13 }}>
                           <rect x="9" y="9" width="13" height="13" rx="2" ry="2" />
                           <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1" />
                        </svg>
                     )}
                     <span>{copiedList ? t("shell.toast.copied", "Copied!") : t("common.copy", "Copy")}</span>
                  </button>
               </div>

               <div className={css.syncFooterRight}>
                  {syncNeedsFix && (
                     <button id="sync-details-fix" type="button" data-no-drag className={css.verifyFix} onClick={fix}>
                        <T k="verify.sync.modal.fix">Rebuild Package</T>
                     </button>
                  )}

                  <button type="button" data-no-drag className={`${css.obBtn} ${css.primary}`} onClick={closeSyncDetailsModal}>
                     <T k="common.close">Close</T>
                  </button>
               </div>
            </div>
         </div>
      </div>
   );
}
