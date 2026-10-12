import { build } from "esbuild";
import { spawnSync } from "node:child_process";
import { mkdtempSync, readdirSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

const entries = readdirSync("src", { recursive: true })
   .filter((f) => String(f).endsWith(".test.ts"))
   .map((f) => join("src", String(f)));
if (entries.length === 0) {
   console.log("no *.test.ts files");
   process.exit(0);
}

const out = mkdtempSync(join(tmpdir(), "amt-frontend-test-"));
try {
   await build({
      entryPoints: entries,
      outdir: out,
      outbase: "src",
      bundle: true,
      platform: "node",
      format: "esm",
      outExtension: { ".js": ".mjs" },
      jsx: "automatic",
      loader: { ".css": "empty" },
      logLevel: "warning",
   });
   const bundles = readdirSync(out, { recursive: true }).filter((f) => String(f).endsWith(".mjs")).map((f) => join(out, String(f)));
   const run = spawnSync(process.execPath, ["--test", ...bundles], { stdio: "inherit" });
   process.exitCode = run.status ?? 1;
} finally {
   rmSync(out, { recursive: true, force: true });
}
