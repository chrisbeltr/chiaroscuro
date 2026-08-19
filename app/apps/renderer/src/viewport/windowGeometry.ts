// TS port of Chiaroscuro.Core/Geometry/Window.cs's two pure geometry methods
// (GetCenter/GetCorners) - buildScenePrimitives.ts needs the window's world-space corners
// to draw the frame and light cone, and this geometry has no server-side reason to cross
// the API boundary (it's plain, input-only math, same as clipToRoom.ts).

import type { RoomShape, Vec3, WindowShape } from './types';
import { add, scale, sub } from './vec3';

export function getWindowCenter(room: RoomShape, win: WindowShape): Vec3 {
  switch (win.wall) {
    case 'North':
      return { x: win.horizontalOffset, y: room.length / 2, z: win.sillHeight + win.height / 2 };
    case 'South':
      return { x: win.horizontalOffset, y: -room.length / 2, z: win.sillHeight + win.height / 2 };
    case 'East':
      return { x: room.width / 2, y: win.horizontalOffset, z: win.sillHeight + win.height / 2 };
    case 'West':
      return { x: -room.width / 2, y: win.horizontalOffset, z: win.sillHeight + win.height / 2 };
  }
}

/** Bottom-left, bottom-right, top-right, top-left - matches Window.GetCorners' ordering. */
export function getWindowCorners(room: RoomShape, win: WindowShape): Vec3[] {
  const center = getWindowCenter(room, win);
  const halfWidth = win.width / 2;
  const halfHeight = win.height / 2;

  const horizontalAxis: Vec3 =
    win.wall === 'North' || win.wall === 'South' ? { x: 1, y: 0, z: 0 } : { x: 0, y: 1, z: 0 };
  const verticalAxis: Vec3 = { x: 0, y: 0, z: 1 };

  const h = scale(horizontalAxis, halfWidth);
  const v = scale(verticalAxis, halfHeight);

  return [
    sub(sub(center, h), v),
    sub(add(center, h), v),
    add(add(center, h), v),
    add(sub(center, h), v),
  ];
}
