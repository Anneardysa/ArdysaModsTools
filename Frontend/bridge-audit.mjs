import { readdirSync, readFileSync, existsSync, statSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = dirname(fileURLToPath(import.meta.url));
const repo = join(root, "..");
const pagesDir = join(root, "src", "pages");

const HOSTS = {
   about: ["UI/Forms/AboutDialogWebView.cs"],
   disable_options: ["UI/Forms/DisableOptionsDialogWebView.cs"],
   generation_preview: ["UI/Forms/GenerationPreviewForm.cs"],
   modspack_update: ["UI/Forms/ModsPackUpdateDialog.cs"],
   modspack_updates: ["UI/Forms/ModsPackUpdatesDialogWebView.cs"],
   status_details: ["UI/Forms/StatusDetailsDialogWebView.cs"],
   support: ["UI/Forms/SupportDialog.cs"],
   update_available: ["UI/Forms/UpdateAvailableDialogWebView.cs"],
   verify_files: ["UI/Forms/VerifyFilesDialogWebView.cs"],
   whatsnew: ["UI/Forms/WhatsNewDialogWebView.cs"],
   install_method: ["UI/Forms/InstallMethodDialogWebView.cs"],
   progress: ["UI/Forms/ProgressOverlay.cs"],
   settings_form: ["UI/Forms/SettingsFormWebView.cs"],
   misc_form: ["UI/Forms/MiscFormWebView.cs"],
   hero_gallery: ["UI/Forms/HeroGalleryForm.cs"],
   dota2_performance: ["UI/Forms/Dota2PerformanceForm.cs"],
   main_shell: ["UI/Forms/MainFormWebView.cs", "UI/Forms/MainFormWebView.View.cs"],
};

function walk(dir, exts, out = []) {
   if (!existsSync(dir)) return out;
   for (const entry of readdirSync(dir)) {
      const p = join(dir, entry);
      if (statSync(p).isDirectory()) walk(p, exts, out);
      else if (exts.some((e) => entry.endsWith(e))) out.push(p);
   }
   return out;
}

const sends = (source) => new Set([...source.matchAll(/\bsend\(\s*"([^"]+)"/g)].map((m) => m[1]));

const sharedSends = new Map();
for (const file of [...walk(join(root, "src", "ui"), [".tsx", ".ts"]), ...walk(join(root, "src", "bridge"), [".tsx", ".ts"])]) {
   const name = file.split(/[\\/]/).pop().replace(/\.tsx?$/, "");
   sharedSends.set(name, sends(readFileSync(file, "utf8")));
}

function reachableSends(pageSource) {
   const set = new Set(sends(pageSource));
   for (const m of pageSource.matchAll(/from "\.\.\/\.\.\/(?:ui|bridge)\/([A-Za-z][\w]*)"/g))
      for (const type of sharedSends.get(m[1]) ?? []) {
         if (type === "minimize" && !/\bminimize\b/.test(pageSource)) continue;
         if (m[1] === "Titlebar" && type === "close") {
            const tag = pageSource.match(/<Titlebar\b[\s\S]*?\/>/);
            if (tag && /\bonClose\s*=/.test(tag[0])) continue;
         }
         if (m[1] === "host" && type === "startDrag") {
            const usesDrag = /startDragUnlessInteractive/.test(pageSource) || /<Titlebar\b/.test(pageSource);
            if (!usesDrag) continue;
         }
         set.add(type);
      }
   return set;
}

function exposed(source) {
   const names = new Set();
   for (const block of source.matchAll(/expose\(\s*\{([\s\S]*?)\n\s*\}\s*\)\s*;/g))
      for (const m of block[1].matchAll(/^\s*([A-Za-z_$][\w$]*)\s*:/gm)) names.add(m[1]);
   return names;
}

const BUILTIN = new Set(["JSON", "document", "window", "setTimeout", "getComputedStyle", "String", "Object"]);

const JS_KEYWORDS = new Set(["if", "for", "while", "switch", "function", "return", "typeof", "catch"]);

function csCalls(source) {
   const names = new Map();
   const add = (name, index) => {
      if (BUILTIN.has(name) || JS_KEYWORDS.has(name) || names.has(name)) return;
      names.set(name, source.slice(0, index).split("\n").length);
   };

   for (const m of source.matchAll(/[$@]{0,2}"(?:window\.)?([A-Za-z_$][\w$]*)\s*\(/g)) add(m[1], m.index);

   for (const m of source.matchAll(/\b(?:CallJs\w*|ExecuteScript\w*|InvokeJs\w*)\s*\(\s*"([A-Za-z_$][\w$]*)"/g))
      add(m[1], m.index);

   for (const m of source.matchAll(/window\.([A-Za-z_$][\w$]*)\s*\(/g)) add(m[1], m.index);

   return names;
}

const cases = (source) => new Set([...source.matchAll(/case\s+"([A-Za-z][\w]*)"\s*:/g)].map((m) => m[1]));

const ported = existsSync(pagesDir)
   ? readdirSync(pagesDir, { withFileTypes: true }).filter((d) => d.isDirectory()).map((d) => d.name).sort()
   : [];

let problems = 0;

for (const page of ported) {
   const hosts = HOSTS[page] ?? [];
   const cs = hosts
      .map((h) => (existsSync(join(repo, h)) ? readFileSync(join(repo, h), "utf8") : ""))
      .join("\n");
   if (!cs) {
      console.log(`${page}: no host form mapped — add it to HOSTS`);
      problems++;
      continue;
   }

   const pageSource = walk(join(pagesDir, page), [".tsx", ".ts"]).map((f) => readFileSync(f, "utf8")).join("\n");

   const sent = reachableSends(pageSource);

   const handled = cases(cs);
   const orphanSends = [...sent].filter((t) => !handled.has(t)).sort();

   const installed = exposed(pageSource);
   const called = csCalls(cs);
   const missingExposed = [...called].filter(([name]) => !installed.has(name)).map(([n, l]) => `${n}() @${l}`);

   const notes = [];
   if (orphanSends.length) notes.push(`sends but UNHANDLED: ${orphanSends.join(", ")}`);
   if (missingExposed.length) notes.push(`C# calls but NOT exposed: ${missingExposed.join(", ")}`);

   if (notes.length) {
      problems += orphanSends.length + missingExposed.length;
      console.log(`${page}  *** ${notes.join("  |  ")}`);
   } else {
      console.log(
         `${page}  OK — ${sent.size} message type(s) all handled, ${installed.size} host entry point(s) all installed.`,
      );
   }

   const deadCases = [...handled].filter((t) => !sent.has(t)).sort();
   if (deadCases.length) console.log(`      (form also handles, never sent: ${deadCases.join(", ")})`);
}

if (problems) {
   console.log(`\n${problems} bridge problem(s).`);
   process.exit(1);
}
console.log(`\n${ported.length} ported page(s), bridge intact both directions.`);
