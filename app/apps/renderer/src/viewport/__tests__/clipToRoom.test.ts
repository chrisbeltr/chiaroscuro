// Ported 1:1 from tests/Chiaroscuro.UI.Tests/Viewport/RoomBoundsClipperTests.cs.
import { describe, expect, it } from 'vitest';
import { clipToRoom } from '../clipToRoom';
import type { RoomShape, Vec3 } from '../types';

// halfWidth = 3, halfLength = 2, height = 3.
const testRoom: RoomShape = { width: 6, length: 4, height: 3, rotationDegrees: 0 };

describe('clipToRoom', () => {
  it('returns the polygon unchanged when entirely within bounds', () => {
    const polygon: Vec3[] = [
      { x: -1, y: -1, z: 1 },
      { x: 1, y: -1, z: 1 },
      { x: 1, y: 1, z: 1 },
      { x: -1, y: 1, z: 1 },
    ];

    const result = clipToRoom(polygon, testRoom);

    expect(result).toEqual(polygon);
  });

  it('truncates a polygon extending past a wall at the wall', () => {
    const polygon: Vec3[] = [
      { x: 2, y: -1, z: 1 },
      { x: 4, y: -1, z: 1 },
      { x: 4, y: 1, z: 1 },
      { x: 2, y: 1, z: 1 },
    ];

    const result = clipToRoom(polygon, testRoom);

    expect(result.every((v) => v.x <= 3.0 + 1e-9)).toBe(true);
    expect(result.some((v) => Math.abs(v.x - 3.0) < 1e-9)).toBe(true);
    expect(result.some((v) => Math.abs(v.x - 2.0) < 1e-9)).toBe(true);
  });

  it('returns empty when entirely outside bounds', () => {
    const polygon: Vec3[] = [
      { x: 4, y: -1, z: 1 },
      { x: 5, y: -1, z: 1 },
      { x: 5, y: 1, z: 1 },
      { x: 4, y: 1, z: 1 },
    ];

    const result = clipToRoom(polygon, testRoom);

    expect(result).toHaveLength(0);
  });
});
