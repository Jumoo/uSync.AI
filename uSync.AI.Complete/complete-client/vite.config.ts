import { defineConfig } from "vite";

export default defineConfig({
  build: {
    lib: {
      entry: "src/bundle.manifests.ts",
      formats: ["es"],
      fileName: "bundle",
    },
    outDir: "../wwwroot/App_Plugins/uSync.AI.Complete",
    emptyOutDir: true,
    sourcemap: true,
    rollupOptions: {
      external: [/^@umbraco/],
      // No content hash in chunk names. A hashed name changes whenever a source file does,
      // and the .NET static web assets cache then points at a file that no longer exists,
      // failing the first build after every client edit.
      output: {
        chunkFileNames: "[name].js",
      },
    },
  },
});
