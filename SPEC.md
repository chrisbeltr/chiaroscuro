# Project Specification: Chiaroscuro
> **Solar Path, Ray Intersection, & Window Alignment Engine**  
> **Target Architecture:** Cross-Platform C# / .NET 10 (Avalonia UI + WebAssembly)

---

## 1. Executive Summary & Aesthetic Vision

**Chiaroscuro** is a high-precision solar elevation tracking and ray-intersection visualization application built in C# / .NET 10. The system allows users to model architectural room spaces, position window apertures, and compute exact solar light projections across any geographic location, date, and time.

### Visual & Color Scheme Requirements
The application must strictly follow a **Chiaroscuro high-contrast aesthetic**:
* **Background & Environment:** Constant dark blue / deep purple palette (`#0C0A1D` base, `#120E2A` panel cards, `#2E235A` subtle borders).
* **Accents & Light Renders:** Golden and yellow colors (`#F59E0B` warm amber, `#FBBF24` gold, `#FDE047` sun yellow) are reserved **exclusively** for sun ray projections, light cones, illuminated target surfaces, active control highlights, and key UI focal points.

---

## 2. Technical Stack Architecture

The solution uses a **single C# codebase** to target WebAssembly (WASM), Windows, macOS, Linux, iOS, and Android without code duplication.

| Layer                   | Technology                    | Role & Purpose                                                         |
| :---------------------: | :---------------------------: | :--------------------------------------------------------------------: |
| **Runtime & Language**  | .NET 10 LTS / C# 13           | Cross-platform core engine, unified domain logic, WASM compilation.    |
| **UI Framework**        | Avalonia UI 11                | Single-source XAML/C# cross-platform desktop, mobile, & web rendering. |
| **3D Render Engine**    | SkiaSharp / Silk.NET / OpenTK | Hardware-accelerated 3D viewport for room geometry & ray tracing.      |
| **Solar Physics Engine**| Custom Solar Math / NodaTime  | Precise solar elevation/azimuth algorithms and timezone support.       |
| **State Management**    | CommunityToolkit.Mvvm         | Reactive DataBinding pipeline with real-time math execution on change. |

---

## 3. Mathematical & Physics Engine

The core math library operates in a 3D coordinate system centered at room origin $O = (0, 0, 0)$ aligned with True North.

### 3.1 Solar Direction Vector
Given Latitude ($\phi$), Longitude ($\lambda$), Date, and Time, compute:
1. **Solar Elevation ($\alpha$)** and **Solar Azimuth ($\theta$)**.
2. **Sun Unit Vector ($S_v$):**
   $$S_v = \begin{pmatrix} \sin(\theta) \cdot \cos(\alpha) \\ \cos(\theta) \cdot \cos(\alpha) \\ \sin(\alpha) \end{pmatrix}$$

### 3.2 Ray Intersection Calculation
* **Ray Equation:** $P(t) = W_{center} + t \cdot (-S_v)$
* **Target Surface Intersection:** Calculate point $P_{target}$ where $P(t)$ intersects floor or interior wall planes.
* **Aperture Projection:** Project the 2D polygon of the window frame along vector $-S_v$ to create an illuminated polygon on the interior surface.

### 3.3 Inverse Alignment Solver
* Given a target point $T$ and window aperture $W$, calculate target light vector $D_v = \frac{W - T}{\|W - T\|}$.
* Sweep 365 days in 15-minute intervals to find timestamps where $S_v \approx -D_v$ within a threshold angle $\epsilon$.

---

## 4. UI / UX Requirements & Component Layout

+-----------------------------------------------------------------------+
|  CHIAROSCURO | Solar & Ray Engine                     [WASM / Native] |
+------------------------------------+----------------------------------+
|  INPUT CONTROLS (Left Sidebar)     | 3D VIEWPORT CANVAS (Main Panel)  |
|  - Dark Blue/Purple Card Styling   | - Dark Violet/Purple Background  |
|  - Location (Lat / Long / Preset)  | - 3D Wireframe Room Model        |
|  - Date & Time Pickers / Sliders   | - Golden Light Cone & Rays       |
|  - Room Dimensions (W x L x H)     | - Bright Yellow Light Patch      |
|  - Window Aperture Position        | - Interactive Orbit/Pan/Zoom     |
+------------------------------------+----------------------------------+
|  RESULTS & INVERSE SOLVER (Bottom)                                    |
|  - Sun Altitude / Azimuth Metrics                                     |
|  - Inverse Matching Dates/Times (Golden Highlight Cards)              |
+-----------------------------------------------------------------------+

---

## 5. Phased Implementation Roadmap

### Phase 1: Core Domain & Solar Library
* Create `Chiaroscuro.Core` project targeting `net10.0`.
* Implement astronomical solar formulas and unit vector conversion.
* Implement 3D ray-plane intersection math with 100% unit test coverage.

### Phase 2: Avalonia Shell & Deep Purple Theme
* Set up `Chiaroscuro.UI` using Avalonia UI 11.
* Define dark blue/purple theme palette in XAML (`#0C0A1D` background, `#2E235A` borders, `#FBBF24` amber highlights).
* Implement MVVM view models with reactive parameter controls.

### Phase 3: Hardware-Accelerated 3D Render Viewport
* Implement SkiaSharp / OpenTK canvas inside Avalonia.
* Render 3D room wireframe and window frame in cool violet/slate tones.
* Shader render golden/amber translucent light volume cone and bright yellow floor projection patch.

### Phase 4: Inverse Alignment Solver & Analytics
* Build inverse solver engine to calculate target illumination dates across the year.
* Add interactive timeline view displaying top matching solar alignment dates.

### Phase 5: WebAssembly & Cross-Platform Deployment
* Build and test `Chiaroscuro.Wasm` for browser execution.
* Configure GitHub Actions CI/CD to build WebAssembly static site and desktop native binaries.
