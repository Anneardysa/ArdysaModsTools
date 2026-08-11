import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { viteSingleFile } from "vite-plugin-singlefile";

export default defineConfig(() => {
   const page = process.env.AMT_PAGE;

   return {
      plugins: [react(), viteSingleFile({ removeViteModuleLoader: true })],
      build: {
         outDir: ".vite-out",
         emptyOutDir: false,
         assetsInlineLimit: Number.MAX_SAFE_INTEGER,
         cssCodeSplit: false,
         rollupOptions: page ? { input: `src/pages/${page}/index.html` } : undefined,
         target: "es2020",
         reportCompressedSize: false,
      },
      server: {
         port: 5173,
         strictPort: true,
      },
   };
});
