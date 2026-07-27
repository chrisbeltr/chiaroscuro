# Chiaroscuro

Chiaroscuro is a cross-platform visual solar path tracer for architects, designers, and anyone
planning a room around natural light. Model a room and a window, pick a location and a moment in
time, and Chiaroscuro traces the sun's ray through the window to show exactly where the light
lands — on the floor or on an interior wall. It also solves the problem in reverse: give it a
target point in the room and it will search the year for every date and time the sun aligns
closely enough with that point to light it up.

Built with [Avalonia](https://avaloniaui.net/), the same UI runs unmodified as a native desktop
app (Windows/Linux/macOS) and as a WebAssembly app in the browser.

## Running the project

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

### Desktop

```bash
dotnet run --project src/Chiaroscuro.Desktop
```

### Browser (WebAssembly)

```bash
dotnet workload install wasm-tools   # one-time
dotnet run --project src/Chiaroscuro.Wasm
```

This starts a local dev server and opens the app in your default browser.

## Building an executable

### Desktop

```bash
dotnet publish src/Chiaroscuro.Desktop -c Release -r <RID> --self-contained
```

Replace `<RID>` with your target runtime identifier, e.g. `win-x64`, `linux-x64`, or `osx-arm64`.
The published executable is written to
`src/Chiaroscuro.Desktop/bin/Release/net10.0/<RID>/publish/`.

### Browser (static site)

```bash
dotnet workload install wasm-tools   # one-time
dotnet publish src/Chiaroscuro.Wasm -c Release
```

The output in `src/Chiaroscuro.Wasm/bin/Release/net10.0-browser/publish/wwwroot/` is a static
site that can be hosted anywhere.

## Features

- **Date and time control** — pick any date and time via the UI's date/time pickers, or hit
  **Now** to jump straight to the current moment. Location and UTC offset can be entered by hand
  or filled in automatically from an IP-based lookup (desktop) or the browser's geolocation API.
- **Room and window modeling** — define a rectangular room by width, length, and height, and set
  a window's wall, horizontal offset, sill height, width, and height. The 3D viewport renders the
  room and window live as you adjust these values, and re-traces the sun's ray through the window
  on every change to show where the light currently lands.
- **Solar position target finding (inverse solver)** — instead of picking a time and seeing where
  the light falls, pick a target point in the room (by dragging or entering coordinates) and a
  tolerance in degrees, and Chiaroscuro sweeps the entire year in 15-minute steps to find every
  date and time the sun's direction aligns within that tolerance of the target. Matches are listed
  as cards you can click to jump the date/time picker straight to that moment.
- **Accurate solar positioning** — sun elevation and azimuth are computed with the Meeus
  low-precision solar position algorithm (the same formula chain behind NOAA's solar calculator),
  accurate to roughly ±0.01°.

## Project structure

| Project | Description |
|---|---|
| `Chiaroscuro.Core` | Platform-independent geometry, solar position math, and the inverse alignment solver. |
| `Chiaroscuro.UI` | Shared Avalonia UI (views, view models, 3D viewport) used by both app heads. |
| `Chiaroscuro.Desktop` | Native desktop app head (Windows/Linux/macOS), with IP-based geolocation. |
| `Chiaroscuro.Wasm` | Browser app head (WebAssembly), with browser geolocation. |

## Running tests

```bash
dotnet test
```
