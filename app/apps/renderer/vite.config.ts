import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// https://vite.dev/config/
export default defineConfig({
  // Relative, not absolute - the packaged Electron app loads this file via `file://`
  // (BrowserWindow.loadFile), where an absolute "/assets/..." reference resolves against the
  // filesystem root instead of the HTML file's own directory and 404s. The Vite dev server
  // (HTTP) and the hosted-web deployment both work fine with a relative base too.
  base: './',
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
  },
  build: {
    // three.js/@react-three/drei (~935 kB minified) can't be split any further than this:
    // r3f imports three directly and synchronously, and Rolldown's chunking (both the
    // manualChunks function form and its own codeSplitting/advancedChunks groups API) merges
    // tightly-coupled modules like that back into one chunk regardless of how they're
    // grouped - confirmed by testing both mechanisms here. What manualChunks *does* reliably
    // separate is react/react-dom/zustand/react-query into their own "vendor" chunk, away
    // from the 3D stack - a real caching win, since app code and the small vendor chunk
    // change far more often than three.js/r3f/drei do.
    rollupOptions: {
      output: {
        manualChunks(id) {
          const normalized = id.replaceAll('\\', '/');
          if (!normalized.includes('/node_modules/')) {
            return undefined;
          }
          if (normalized.includes('/three/') || normalized.includes('/three-stdlib/') || normalized.includes('/@react-three/')) {
            return 'three-vendor';
          }
          return 'vendor';
        },
      },
    },
    // The three-vendor chunk above is a known, unavoidable ~935 kB - raised so a build
    // doesn't print a misleading "you should code-split this" warning for a chunk that's
    // already been through that analysis and can't be split further without lazy-loading
    // the 3D viewport (a larger, separate change from this chunking cleanup).
    chunkSizeWarningLimit: 1000,
  },
});
