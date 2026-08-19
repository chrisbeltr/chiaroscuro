// Plain, framework-agnostic scene types - mirrors Chiaroscuro.UI/Viewport/ScenePrimitive.cs's
// deliberate independence from any particular rendering framework (there it was "no Avalonia/
// Skia dependency"; here it's "no three.js/r3f dependency" - buildScenePrimitives.ts stays pure
// and testable without a WebGL context).

export interface Vec3 {
  x: number;
  y: number;
  z: number;
}

export type WallOrientation = 'North' | 'South' | 'East' | 'West';
export type RoomSurface = 'Floor' | 'NorthWall' | 'SouthWall' | 'EastWall' | 'WestWall';

export interface RoomShape {
  width: number;
  length: number;
  height: number;
  rotationDegrees: number;
}

export interface WindowShape {
  wall: WallOrientation;
  horizontalOffset: number;
  sillHeight: number;
  width: number;
  height: number;
}

export interface LandingPatch {
  surface: RoomSurface;
  polygon: Vec3[];
}

export interface IlluminationResult {
  surface: RoomSurface;
  centerPoint: Vec3;
  illuminatedPolygon: Vec3[];
  patches: LandingPatch[];
}

/** RGBA, 0-255 per channel - matches Chiaroscuro.UI/Viewport/ScenePrimitive.cs's SceneColor. */
export interface SceneColor {
  r: number;
  g: number;
  b: number;
  a: number;
}

export interface SceneLine {
  kind: 'line';
  start: Vec3;
  end: Vec3;
  color: SceneColor;
}

export interface ScenePolygon {
  kind: 'polygon';
  corners: Vec3[];
  color: SceneColor;
}

export type ScenePrimitive = SceneLine | ScenePolygon;
