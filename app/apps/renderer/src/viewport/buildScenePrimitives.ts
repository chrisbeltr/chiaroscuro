// TS port of Chiaroscuro.UI/Viewport/SceneBuilder.cs - the "what to draw" step, independent
// of "how to draw it" (that's RoomViewportCanvas.tsx's job, via react-three-fiber). Room
// rotation deliberately does NOT rotate this wireframe: Room.ToRoomSpaceDirection instead
// rotates the *sun direction* into room-local space server-side, so the room mesh itself
// always stays axis-aligned here - matching SceneBuilder.cs's own behavior exactly.

import type { IlluminationResult, RoomShape, SceneColor, ScenePrimitive, Vec3, WindowShape } from './types';
import { clipToRoom } from './clipToRoom';
import { getWindowCenter, getWindowCorners } from './windowGeometry';
import { add, cross, length, normalize, scale, sub } from './vec3';

// Matches theme/tokens.css's palette: --color-foreground for wireframe, --color-amber/
// --color-sun-yellow (with transparency) for light - see SceneBuilder.cs's own comment.
const WIREFRAME_COLOR: SceneColor = { r: 0x94, g: 0x91, b: 0xc0, a: 255 };
const LIGHT_CONE_COLOR: SceneColor = { r: 0xf5, g: 0x9e, b: 0x0b, a: 60 };
const LANDING_PATCH_COLOR: SceneColor = { r: 0xfd, g: 0xe0, b: 0x47, a: 140 };

export interface BuildSceneOptions {
  target?: Vec3;
  toleranceDegrees?: number;
}

export function buildScenePrimitives(
  room: RoomShape,
  win: WindowShape,
  illumination: IlluminationResult | null,
  options: BuildSceneOptions = {},
): ScenePrimitive[] {
  const primitives: ScenePrimitive[] = [];
  const windowCorners = getWindowCorners(room, win);

  addRoomWireframe(primitives, room);
  addRectangleEdges(primitives, windowCorners, WIREFRAME_COLOR);

  if (illumination) {
    // The cone still starts from the raw, unclipped illuminatedPolygon rather than following
    // the fill's per-surface wrap - each resulting face is clipped to the room's box so it
    // never visually pokes through a wall/floor/ceiling.
    addLightCone(primitives, room, windowCorners, illumination.illuminatedPolygon);

    for (const patch of illumination.patches) {
      primitives.push({ kind: 'polygon', corners: patch.polygon, color: LANDING_PATCH_COLOR });
    }
  }

  if (options.target) {
    addTargetIndicator(primitives, getWindowCenter(room, win), options.target, options.toleranceDegrees);
  }

  return primitives;
}

function addRoomWireframe(primitives: ScenePrimitive[], room: RoomShape): void {
  const halfWidth = room.width / 2;
  const halfLength = room.length / 2;
  const height = room.height;

  const floor: Vec3[] = [
    { x: -halfWidth, y: -halfLength, z: 0 },
    { x: halfWidth, y: -halfLength, z: 0 },
    { x: halfWidth, y: halfLength, z: 0 },
    { x: -halfWidth, y: halfLength, z: 0 },
  ];
  const ceiling: Vec3[] = floor.map((p) => ({ ...p, z: height }));

  addRectangleEdges(primitives, floor, WIREFRAME_COLOR);
  addRectangleEdges(primitives, ceiling, WIREFRAME_COLOR);

  for (let i = 0; i < 4; i++) {
    primitives.push({ kind: 'line', start: floor[i], end: ceiling[i], color: WIREFRAME_COLOR });
  }
}

function addRectangleEdges(primitives: ScenePrimitive[], corners: Vec3[], color: SceneColor): void {
  for (let i = 0; i < corners.length; i++) {
    primitives.push({ kind: 'line', start: corners[i], end: corners[(i + 1) % corners.length], color });
  }
}

/** Up to four translucent quad faces connecting each window corner to the matching corner of
 * where its light lands - relies on getWindowCorners and IlluminationResult.illuminatedPolygon
 * sharing the same corner ordering (bottom-left, bottom-right, top-right, top-left). */
function addLightCone(primitives: ScenePrimitive[], room: RoomShape, windowCorners: Vec3[], landingCorners: Vec3[]): void {
  for (let i = 0; i < windowCorners.length; i++) {
    const next = (i + 1) % windowCorners.length;
    const face = clipToRoom([windowCorners[i], windowCorners[next], landingCorners[next], landingCorners[i]], room);

    if (face.length >= 3) {
      primitives.push({ kind: 'polygon', corners: face, color: LIGHT_CONE_COLOR });
    }
  }
}

/** A small crosshair at `target`, plus - if `toleranceDegrees` is given - a ring showing how
 * far off the sun's direction could be and still count as a match. The ring lies in the plane
 * perpendicular to the window->target direction (a reticle facing the window), which is also
 * the mathematically exact slice of the angular tolerance cone. */
function addTargetIndicator(primitives: ScenePrimitive[], windowCenter: Vec3, target: Vec3, toleranceDegrees?: number): void {
  const toWindow = sub(windowCenter, target);
  if (length(toWindow) < 1e-9) {
    return; // target sits exactly on the window's center - no well-defined direction to build a basis from
  }

  const direction = normalize(toWindow);
  const seed: Vec3 = Math.abs(direction.z) > 0.99 ? { x: 1, y: 0, z: 0 } : { x: 0, y: 0, z: 1 };
  const right = normalize(cross(direction, seed));
  const up = normalize(cross(right, direction));

  const crosshairArmLength = 0.1;
  primitives.push({
    kind: 'line',
    start: sub(target, scale(right, crosshairArmLength)),
    end: add(target, scale(right, crosshairArmLength)),
    color: WIREFRAME_COLOR,
  });
  primitives.push({
    kind: 'line',
    start: sub(target, scale(up, crosshairArmLength)),
    end: add(target, scale(up, crosshairArmLength)),
    color: WIREFRAME_COLOR,
  });

  if (toleranceDegrees !== undefined) {
    const radius = length(toWindow) * Math.tan((toleranceDegrees * Math.PI) / 180);
    const segments = 32;
    const ringPoints: Vec3[] = [];
    for (let i = 0; i < segments; i++) {
      const angle = (2 * Math.PI * i) / segments;
      ringPoints.push(add(add(target, scale(right, radius * Math.cos(angle))), scale(up, radius * Math.sin(angle))));
    }
    for (let i = 0; i < segments; i++) {
      primitives.push({ kind: 'line', start: ringPoints[i], end: ringPoints[(i + 1) % segments], color: WIREFRAME_COLOR });
    }
  }
}
