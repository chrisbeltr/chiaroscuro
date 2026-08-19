// TS port of Chiaroscuro.UI/Viewport/RoomBoundsClipper.cs. Rendering-only concern, deliberately
// separate from the backend's IlluminationPatchClipper (which re-projects overflow onto whichever
// surface light physically continues onto): the light cone is a loose visual abstraction with
// nothing physically meaningful to re-project onto, so anything outside the room's box is
// simply discarded here.

import type { RoomShape, Vec3 } from './types';
import { add, scale, sub } from './vec3';

/** Clips `polygon` against all 6 of the room's bounding planes (floor, ceiling, 4 walls) and
 * returns whatever remains inside. May return an empty array if nothing is left. */
export function clipToRoom(polygon: Vec3[], room: RoomShape): Vec3[] {
  const halfWidth = room.width / 2;
  const halfLength = room.length / 2;
  const height = room.height;

  let clipped = polygon;
  clipped = clipToHalfSpace(clipped, (p) => halfWidth - p.x);
  clipped = clipToHalfSpace(clipped, (p) => p.x + halfWidth);
  clipped = clipToHalfSpace(clipped, (p) => halfLength - p.y);
  clipped = clipToHalfSpace(clipped, (p) => p.y + halfLength);
  clipped = clipToHalfSpace(clipped, (p) => height - p.z);
  clipped = clipToHalfSpace(clipped, (p) => p.z);

  return clipped;
}

/** Standard Sutherland-Hodgman clip of a convex polygon against one half-space: keeps every
 * vertex with a non-negative insideDistance, inserting the exact boundary-crossing point
 * wherever an edge switches sides. */
function clipToHalfSpace(polygon: Vec3[], insideDistance: (p: Vec3) => number): Vec3[] {
  if (polygon.length === 0) {
    return polygon;
  }

  const result: Vec3[] = [];

  for (let i = 0; i < polygon.length; i++) {
    const current = polygon[i];
    const next = polygon[(i + 1) % polygon.length];
    const currentDistance = insideDistance(current);
    const nextDistance = insideDistance(next);

    if (currentDistance >= 0) {
      result.push(current);
    }

    if (currentDistance * nextDistance < 0) {
      const t = currentDistance / (currentDistance - nextDistance);
      result.push(add(current, scale(sub(next, current), t)));
    }
  }

  return result;
}
