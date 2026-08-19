import { useMemo } from 'react';
import { Canvas } from '@react-three/fiber';
import { GizmoHelper, GizmoViewport, OrbitControls, PerspectiveCamera } from '@react-three/drei';
import { useCalculatorStore } from '../../state/useCalculatorStore';
import { useIllumination } from '../../api/useIllumination';
import { buildScenePrimitives } from '../../viewport/buildScenePrimitives';
import { SceneLines } from './scene/SceneLines';
import { ScenePolygons } from './scene/ScenePolygons';
import './RoomViewportCanvas.css';

// Straight from Chiaroscuro.UI/Viewport/OrbitCamera.cs's own constants - no panning, pitch
// clamped to +/-89 degrees, distance clamped 1-50.
const MIN_DISTANCE = 1;
const MAX_DISTANCE = 50;
const MAX_PITCH_DEGREES = 89;

export function RoomViewportCanvas() {
  const {
    roomWidth, roomLength, roomHeight, roomRotationDegrees,
    windowWall, windowHorizontalOffset, windowSillHeight, windowWidth, windowHeight,
    targetX, targetY, targetZ, toleranceDegrees,
  } = useCalculatorStore();
  const illumination = useIllumination();

  const room = { width: roomWidth, length: roomLength, height: roomHeight, rotationDegrees: roomRotationDegrees };
  const win = {
    wall: windowWall,
    horizontalOffset: windowHorizontalOffset,
    sillHeight: windowSillHeight,
    width: windowWidth,
    height: windowHeight,
  };
  const target = targetX !== null && targetY !== null && targetZ !== null ? { x: targetX, y: targetY, z: targetZ } : undefined;

  const primitives = useMemo(
    () =>
      buildScenePrimitives(room, win, illumination.data?.illumination ?? null, {
        target,
        toleranceDegrees: toleranceDegrees ?? undefined,
      }),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [
      room.width, room.length, room.height,
      win.wall, win.horizontalOffset, win.sillHeight, win.width, win.height,
      illumination.data, target?.x, target?.y, target?.z, toleranceDegrees,
    ],
  );

  // Matches OrbitCamera.cs's default target: the room's volumetric center, not the floor
  // (Room's own origin is floor-center - see Chiaroscuro.Core/Geometry/Room.cs).
  const lookAt: [number, number, number] = [0, 0, room.height / 2];

  return (
    <div className="room-viewport">
      <Canvas>
        {/* Room's convention is +Z = Up (see Room.cs), but three.js defaults to Y-up - every
            camera/controls/gizmo element below must agree on up=[0,0,1] or nothing here lines
            up with the room-space coordinates buildScenePrimitives already produced. */}
        <PerspectiveCamera makeDefault position={[0, -8, room.height / 2 + 2]} up={[0, 0, 1]} fov={45} near={0.1} far={1000} />
        <ambientLight intensity={1.2} />
        <SceneLines primitives={primitives} />
        <ScenePolygons primitives={primitives} />
        <OrbitControls
          target={lookAt}
          minDistance={MIN_DISTANCE}
          maxDistance={MAX_DISTANCE}
          minPolarAngle={((90 - MAX_PITCH_DEGREES) * Math.PI) / 180}
          maxPolarAngle={((90 + MAX_PITCH_DEGREES) * Math.PI) / 180}
          enablePan={false}
        />
        {/* drei's GizmoViewport only labels the +X/+Y/+Z heads (negative axes render as plain
            unlabeled dots) - the closest available match to RoomViewport.cs's hand-drawn
            six-label E/N/U/W/S/D gizmo, given +X=East/+Y=North/+Z=Up here. */}
        <GizmoHelper alignment="bottom-right" margin={[64, 64]}>
          <GizmoViewport labels={['E', 'N', 'U']} axisColors={['#f59e0b', '#4ade80', '#60a5fa']} />
        </GizmoHelper>
      </Canvas>
    </div>
  );
}
