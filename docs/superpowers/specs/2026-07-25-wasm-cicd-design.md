# Phase 5: WebAssembly & Cross-Platform Deployment — Design

## Context

`SPEC.md` §5 Phase 5 (the final phase) calls for building `Chiaroscuro.Wasm` for
browser execution and configuring GitHub Actions CI/CD to build the
WebAssembly static site and desktop native binaries. Phases 1-4 are complete:
`Chiaroscuro.Core` (solar/geometry math) and `Chiaroscuro.UI` (Avalonia shell,
3D viewport, inverse solver UI) both work as a native desktop app today. This
phase adds a second UI head (browser) sharing all of that code, plus the CI
pipeline to build and ship both.

An exploration pass confirmed nothing in the existing code blocks a browser
target: `RoomViewport`'s custom rendering goes through Avalonia's standard
`ICustomDrawOperation` + `ISkiaSharpApiLeaseFeature` pattern (works
identically under Avalonia.Browser's WASM-compiled Skia), there's no
filesystem/process/raw-threading/P-Invoke usage anywhere in the repo, and
`Chiaroscuro.UI.Tests` has no `Avalonia.Headless` dependency and instantiates
no real `Application`/`Window` — so CI test steps need no Xvfb/headless setup
on any OS.

## Decisions

- **New `Chiaroscuro.Wasm` project**, not multi-targeting `Chiaroscuro.UI`
  itself. `Chiaroscuro.UI.csproj` today plays double duty as both the shared
  UI library *and* the desktop head (`OutputType=WinExe`, references
  `Avalonia.Desktop`, has a desktop `Program.cs`). Rather than untangling
  that, `Chiaroscuro.Wasm` is added as a third head that references
  `Chiaroscuro.UI` for its shared views/view-models, with its own
  browser-specific `Program.cs`.
- **Prevent `Avalonia.Desktop` from flowing downstream.** A plain
  `ProjectReference` would transitively drag `Avalonia.Desktop` (and its
  `Avalonia.X11`/`Avalonia.Native`/Win32 interop dependencies) into
  `Chiaroscuro.Wasm`'s graph — none of that is meaningful or safe in a wasm
  publish/trim pipeline. Fix: mark that one `PackageReference` in
  `Chiaroscuro.UI.csproj` as `PrivateAssets="All"`. This only affects what's
  exposed to *referencing* projects; the desktop app itself is unaffected.
- **Extract `MainView` from `MainWindow`.** Avalonia.Browser's application
  lifetime is `ISingleViewApplicationLifetime` (no `Window` concept, just a
  content view). `App.axaml.cs` currently only handles
  `IClassicDesktopStyleApplicationLifetime`. The standard, minimal-diff fix:
  move `MainWindow.axaml`'s content into a new `MainView : UserControl`,
  reduce `MainWindow.axaml` to a thin shell hosting `<views:MainView/>`, and
  add an `ISingleViewApplicationLifetime` branch in `App.axaml.cs` that sets
  `singleView.MainView = new MainView { DataContext = new MainViewModel() }`.
  Pure move, not a rewrite — no bindings or control types change.
- **CI desktop matrix: Linux, Windows, macOS** (`ubuntu-latest`,
  `windows-latest`, `macos-latest`). Each builds, runs the existing
  `dotnet test` suite, and publishes a self-contained binary as a build
  artifact. macOS's GitHub-hosted runner is Apple Silicon, so its RID is
  `osx-arm64` (not `osx-x64`). CI running the test suite gives free
  cross-platform signal even though the user doesn't own a Mac to manually
  verify against.
- **WASM build deploys to GitHub Pages**, separate workflow, deploy-only-on
  `main`. Uses `actions/configure-pages`'s `base_path` output to rewrite the
  published `index.html`'s `<base href>` for the project-pages subpath,
  rather than hardcoding the repo name into source — keeps local dev and any
  future custom domain working unmodified.
- **`wasm-tools` workload is not installed in this dev sandbox** and won't be
  installed here (a local SDK mutation outside this session's scope). The
  plan is written so `dotnet build`/`dotnet test` (which don't need the
  workload) are verified locally, while `dotnet publish` of `Chiaroscuro.Wasm`
  (which does) is verified by CI on its first real run — that's an
  acceptable, cheap first real test rather than a blocking local prerequisite.

## API shape

### `src/Chiaroscuro.Wasm/Chiaroscuro.Wasm.csproj` (new)

`Microsoft.NET.Sdk.WebAssembly`, `TargetFramework=net10.0-browser`,
`OutputType=Exe`, references `Avalonia.Browser` (pinned to the same `12.1.0`
as the rest of the repo's Avalonia packages) and `Chiaroscuro.UI`.

### `src/Chiaroscuro.Wasm/Program.cs` (new)

Its own `BuildAvaloniaApp()` — deliberately *not* reusing
`Chiaroscuro.UI.Program.BuildAvaloniaApp()`, since that method calls
`.UsePlatformDetect()`, an extension only available via `Avalonia.Desktop`
(which this project intentionally can't see) and which wouldn't know how to
select the browser backend anyway:

```csharp
private static Task Main(string[] args) => BuildAvaloniaApp()
    .WithInterFont()
    .StartBrowserAppAsync("out");

public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>();
```

### `src/Chiaroscuro.UI/Views/MainView.axaml` + `.axaml.cs` (new)

A `UserControl` holding exactly what `MainWindow.axaml`'s `<Grid>` holds
today. `MainWindow.axaml` becomes a thin `<Window>` wrapping
`<views:MainView/>`.

### `App.axaml.cs` (extended)

Adds an `else if (ApplicationLifetime is ISingleViewApplicationLifetime
singleView)` branch alongside the existing desktop branch.

### `.github/workflows/dotnet-ci.yml` (new)

Matrix job (`ubuntu-latest`/`windows-latest`/`macos-latest`): restore, build,
test, publish self-contained `Chiaroscuro.UI` per-RID, upload as an artifact.
Triggers on push to `main` and on pull requests.

### `.github/workflows/deploy-wasm.yml` (new)

Single `ubuntu-latest` job, triggers on push to `main` (+ manual dispatch):
install `wasm-tools`, publish `Chiaroscuro.Wasm`, rewrite `<base href>` via
`actions/configure-pages`'s `base_path` output, deploy via
`actions/upload-pages-artifact` + `actions/deploy-pages`. Scoped
`permissions: pages: write, id-token: write` — kept out of the desktop
workflow, which needs neither.

## Data flow

No runtime data flow changes — this phase is purely about *how the existing
app is built and shipped*, not new application behavior. `MainView` renders
identically to what `MainWindow`'s content rendered before; the browser head
runs the exact same `MainViewModel`/`SceneBuilder`/solver code the desktop
head does.

## Testing

- Existing `dotnet test` suite must stay green after the `MainView`
  extraction and the `PrivateAssets` csproj change (both are structurally
  inert from the test suite's point of view — no test instantiates
  `MainWindow`/`MainView` directly).
- `dotnet build Chiaroscuro.slnx -c Release` must succeed with
  `Chiaroscuro.Wasm` included — this is checkable locally without the
  `wasm-tools` workload (plain compile against `Avalonia.Browser`'s managed
  API surface doesn't need the browser runtime pack; only `publish`/AOT does).
- `dotnet publish` of `Chiaroscuro.Wasm` and the deployed Pages URL rendering
  correctly are both left for CI / manual verification (see Error handling).

## Error handling

- If `wasm-tools` installation or `dotnet publish` fails in CI, that job
  fails visibly in the Actions log without blocking the independent desktop
  CI workflow.
- If the deployed Pages site 404s on `_framework/*` assets, the first suspect
  is the `<base href>` rewrite — confirm `configure-pages`'s `base_path`
  output and that the `sed` pattern still matches `index.html`.
- No new failure modes are introduced in the application code itself — this
  phase only touches project/build/CI configuration.

## Out of scope for this pass

- Restructuring `Chiaroscuro.UI.csproj` into separate library/desktop-head
  projects (the `PrivateAssets` fix is a smaller, equally effective change).
- Mobile (iOS/Android) heads — `SPEC.md`'s Phase 5 names WebAssembly and
  desktop native binaries only.
- A custom domain for the Pages site.
- Actually enabling GitHub Pages in the repo's Settings UI — that's a
  one-time manual step for the repo owner, not something committable.
