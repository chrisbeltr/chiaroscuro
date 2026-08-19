# Chiaroscuro

Chiaroscuro is a cross-platform visual solar path tracer for architects, designers, and anyone
planning a room around natural light. Model a room and a window, pick a location and a moment in
time, and Chiaroscuro traces the sun's ray through the window to show exactly where the light
lands — on the floor or on an interior wall. It also solves the problem in reverse: give it a
target point in the room and it will search the year for every date and time the sun aligns
closely enough with that point to light it up.

Built as a stateless ASP.NET backend (all calculation logic) paired with a React + Three.js
frontend, shipped either as a self-contained Electron desktop app (Windows/Linux/macOS, with the
backend bundled as a local sidecar process) or as a plain web deployment of the same backend and a
static frontend build.

## Running the project

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) and [Node.js](https://nodejs.org/).

### Backend (ASP.NET API)

```bash
dotnet run --project src/Chiaroscuro.Api
```

Starts the API on `http://127.0.0.1:5259` (see `src/Chiaroscuro.Api/Properties/launchSettings.json`).

### Frontend (React SPA, dev server)

```bash
cd app
npm install
npm run dev
```

Starts a Vite dev server at `http://localhost:5173` that talks to the API above.

### Desktop (Electron)

With the API and the Vite dev server both running (see above):

```bash
cd app/apps/electron
npm run dev
```

## Building an executable

### Backend, self-contained per platform

```bash
dotnet publish src/Chiaroscuro.Api -c Release -r <RID> --self-contained -p:PublishSingleFile=true -o app/apps/electron/resources/backend/<RID>
```

Replace `<RID>` with your target runtime identifier, e.g. `win-x64`, `linux-x64`, or `osx-arm64`.

### Frontend

```bash
cd app/apps/renderer
npm run build
```

Produces a static site in `app/apps/renderer/dist/` that can be hosted anywhere, or bundled into
the Electron package below.

### Packaged Electron app

With both of the above built:

```bash
cd app/apps/electron
npm run build
npm run package
```

Produces a platform-specific installer in `app/apps/electron/out/` (NSIS on Windows, DMG on macOS,
AppImage on Linux) with the self-contained backend bundled as a sidecar process.

## Features

- **Date and time control** — pick any date and time via the UI's date/time pickers, or hit
  **Now** to jump straight to the current moment. Location and UTC offset can be entered by hand
  or filled in automatically — via an IP-based lookup (Electron) or the browser's geolocation API
  (hosted web).
- **Room and window modeling** — define a rectangular room by width, length, height, and rotation,
  and set a window's wall, horizontal offset, sill height, width, and height. The 3D viewport
  renders the room and window live as you adjust these values, and re-traces the sun's ray through
  the window on every change to show where the light currently lands.
- **Solar position target finding (inverse solver)** — instead of picking a time and seeing where
  the light falls, pick a target point in the room and a tolerance in degrees, and Chiaroscuro
  sweeps the entire year in 15-minute steps to find every date and time the sun's direction aligns
  within that tolerance of the target. Matches are listed as cards you can click to jump the
  date/time picker straight to that moment.
- **Accurate solar positioning** — sun elevation and azimuth are computed with the Meeus
  low-precision solar position algorithm (the same formula chain behind NOAA's solar calculator),
  accurate to roughly ±0.01°.

## Project structure

| Project | Description |
|---|---|
| `src/Chiaroscuro.Core` | Platform-independent geometry, solar position math, and the inverse alignment solver. |
| `src/Chiaroscuro.Api` | Stateless ASP.NET minimal API exposing `Chiaroscuro.Core`'s calculations over HTTP. |
| `app/apps/renderer` | React + Three.js (react-three-fiber) single-page app. |
| `app/apps/electron` | Electron desktop shell that spawns `Chiaroscuro.Api` as a local sidecar process. |

## Running tests

```bash
dotnet test
```

```bash
cd app
npm run test
```
