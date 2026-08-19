// Ported 1:1 from tests/Chiaroscuro.UI.Tests/Viewport/SceneBuilderTests.cs.
import { describe, expect, it } from 'vitest';
import { buildScenePrimitives } from '../buildScenePrimitives';
import { getWindowCorners } from '../windowGeometry';
import { dot, length, scale, sub } from '../vec3';
import type { IlluminationResult, LandingPatch, RoomShape, ScenePolygon, SceneLine, Vec3, WindowShape } from '../types';

const testRoom: RoomShape = { width: 6, length: 5, height: 3, rotationDegrees: 0 };
const testWindow: WindowShape = { wall: 'South', horizontalOffset: 0, sillHeight: 1, width: 1.2, height: 1.5 };

const testIlluminatedPolygon: Vec3[] = [
  { x: -0.6, y: -2.5, z: 0 },
  { x: 0.6, y: -2.5, z: 0 },
  { x: 0.6, y: -1.0, z: 0 },
  { x: -0.6, y: -1.0, z: 0 },
];

const testPatches: LandingPatch[] = [{ surface: 'Floor', polygon: testIlluminatedPolygon }];

const lines = (primitives: ReturnType<typeof buildScenePrimitives>): SceneLine[] =>
  primitives.filter((p): p is SceneLine => p.kind === 'line');
const polygons = (primitives: ReturnType<typeof buildScenePrimitives>): ScenePolygon[] =>
  primitives.filter((p): p is ScenePolygon => p.kind === 'polygon');

describe('buildScenePrimitives', () => {
  it('only emits wireframe lines when there is no illumination', () => {
    const primitives = buildScenePrimitives(testRoom, testWindow, null);

    expect(primitives.every((p) => p.kind === 'line')).toBe(true);
    // 12 room edges (4 floor + 4 ceiling + 4 vertical) + 4 window frame edges.
    expect(primitives).toHaveLength(16);
  });

  it('also emits four light-cone faces and one landing patch when illuminated', () => {
    const illumination: IlluminationResult = {
      surface: 'Floor',
      centerPoint: { x: 0, y: -1.75, z: 0 },
      illuminatedPolygon: testIlluminatedPolygon,
      patches: testPatches,
    };

    const primitives = buildScenePrimitives(testRoom, testWindow, illumination);

    expect(polygons(primitives)).toHaveLength(5); // 4 light-cone faces + 1 landing patch
    expect(lines(primitives)).toHaveLength(16); // wireframe is unaffected
  });

  it('connects matching window and landing corners for each light-cone face', () => {
    const windowCorners = getWindowCorners(testRoom, testWindow);
    const illumination: IlluminationResult = {
      surface: 'Floor',
      centerPoint: { x: 0, y: -1.75, z: 0 },
      illuminatedPolygon: testIlluminatedPolygon,
      patches: testPatches,
    };

    const primitives = buildScenePrimitives(testRoom, testWindow, illumination);

    const firstFace = polygons(primitives)[0];
    expect(firstFace.corners).toEqual([
      windowCorners[0],
      windowCorners[1],
      testIlluminatedPolygon[1],
      testIlluminatedPolygon[0],
    ]);
  });

  it('emits one ScenePolygon per patch when there are multiple patches', () => {
    const twoPatches: LandingPatch[] = [
      { surface: 'Floor', polygon: testIlluminatedPolygon },
      {
        surface: 'SouthWall',
        polygon: [
          { x: -0.6, y: -2.5, z: 0.5 },
          { x: 0.6, y: -2.5, z: 0.5 },
          { x: 0.6, y: -2.5, z: 1.0 },
          { x: -0.6, y: -2.5, z: 1.0 },
        ],
      },
    ];
    const illumination: IlluminationResult = {
      surface: 'Floor',
      centerPoint: { x: 0, y: -1.75, z: 0 },
      illuminatedPolygon: testIlluminatedPolygon,
      patches: twoPatches,
    };

    const primitives = buildScenePrimitives(testRoom, testWindow, illumination);

    // 4 light-cone faces (unchanged, still built from illuminatedPolygon) + 2 landing-patch
    // fills (one per patch, instead of the usual 1).
    expect(polygons(primitives)).toHaveLength(6);
  });

  it('clips each light-cone face to the room bounds when it extends past the room', () => {
    // Reaching X=4 - past testRoom's halfWidth of 3 - simulates the raw, unclipped projection
    // poking through the East wall the way it used to render.
    const overflowingPolygon: Vec3[] = [
      { x: -0.6, y: -2.5, z: 0 },
      { x: 4.0, y: -2.5, z: 0 },
      { x: 4.0, y: -1.0, z: 0 },
      { x: -0.6, y: -1.0, z: 0 },
    ];
    const illumination: IlluminationResult = {
      surface: 'Floor',
      centerPoint: { x: 0, y: -1.75, z: 0 },
      illuminatedPolygon: overflowingPolygon,
      patches: [{ surface: 'Floor', polygon: overflowingPolygon }],
    };

    const primitives = buildScenePrimitives(testRoom, testWindow, illumination);

    // addLightCone runs before the patches loop, so the first 4 ScenePolygons are the cone faces.
    const coneFaces = polygons(primitives).slice(0, 4);
    expect(coneFaces).toHaveLength(4);
    expect(coneFaces.every((face) => face.corners.every((v) => v.x <= 3.0 + 1e-9))).toBe(true);
    expect(coneFaces.some((face) => face.corners.some((v) => Math.abs(v.x - 3.0) < 1e-9))).toBe(true);
  });

  it('emits crosshair lines centered on the target when given a target point', () => {
    const target: Vec3 = { x: 0, y: -1.5, z: 0.5 };

    const primitives = buildScenePrimitives(testRoom, testWindow, null, { target });

    // 16 wireframe lines (unaffected) + 2 crosshair segments (no ring, since no tolerance given).
    const allLines = lines(primitives);
    expect(allLines).toHaveLength(18);

    const crosshairLines = allLines.slice(16);
    for (const line of crosshairLines) {
      const midpoint = scale({ x: line.start.x + line.end.x, y: line.start.y + line.end.y, z: line.start.z + line.end.z }, 0.5);
      expect(midpoint.x).toBeCloseTo(target.x, 9);
      expect(midpoint.y).toBeCloseTo(target.y, 9);
      expect(midpoint.z).toBeCloseTo(target.z, 9);
    }
  });

  it('also emits a ring at the expected radius when given a target point and tolerance', () => {
    const target: Vec3 = { x: 0, y: -1.5, z: 0.5 };
    const toleranceDegrees = 5.0;

    const primitives = buildScenePrimitives(testRoom, testWindow, null, { target, toleranceDegrees });

    const windowCenter = { x: 0, y: -testRoom.length / 2, z: testWindow.sillHeight + testWindow.height / 2 };
    const toWindow = sub(windowCenter, target);
    const expectedRadius = length(toWindow) * Math.tan((toleranceDegrees * Math.PI) / 180);

    // 16 wireframe + 2 crosshair + 32 ring segments.
    const allLines = lines(primitives);
    expect(allLines).toHaveLength(50);

    const ringLines = allLines.slice(18);
    expect(ringLines).toHaveLength(32);
    for (const line of ringLines) {
      expect(length(sub(line.start, target))).toBeCloseTo(expectedRadius, 6);
      expect(length(sub(line.end, target))).toBeCloseTo(expectedRadius, 6);

      // The ring must lie in the plane perpendicular to the window-target direction (a reticle
      // facing the window): every vertex minus the target should be orthogonal to that direction.
      expect(dot(sub(line.start, target), toWindow)).toBeCloseTo(0, 6);
      expect(dot(sub(line.end, target), toWindow)).toBeCloseTo(0, 6);
    }
  });

  it('emits no indicator primitives without a target point', () => {
    const primitives = buildScenePrimitives(testRoom, testWindow, null);

    expect(primitives).toHaveLength(16); // just the wireframe, nothing else
  });
});
