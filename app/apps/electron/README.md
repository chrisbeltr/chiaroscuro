# @chiaroscuro/electron

The Electron shell: spawns `Chiaroscuro.Api` as a local sidecar (see `src/backendProcess.ts`)
and loads the `@chiaroscuro/renderer` SPA against it.

## Dev

Requires the backend and the renderer's Vite dev server running separately first:

```bash
dotnet run --project ../../../src/Chiaroscuro.Api/Chiaroscuro.Api.csproj
npm run dev --workspace apps/renderer
npm run dev --workspace apps/electron
```

## Packaging (`npm run package`)

Not yet runnable as-is - two prerequisites this scaffold intentionally leaves undone:

1. **Backend binaries**: publish `Chiaroscuro.Api` self-contained per target RID into
   `resources/backend/<rid>/` (e.g. `dotnet publish ../../../src/Chiaroscuro.Api -r win-x64
   --self-contained -p:PublishSingleFile=true -o resources/backend/win-x64`). Mirrors the
   per-RID matrix in `.github/workflows/release.yml`.
2. **Icons**: `build/icon.ico` (Windows), `build/icon.icns` (macOS), `build/icon.png`
   (Linux) - replacing the old Avalonia-branded window icon, which needs new artwork rather
   than a direct port.
