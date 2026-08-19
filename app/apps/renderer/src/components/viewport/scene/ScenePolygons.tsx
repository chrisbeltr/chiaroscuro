import { useMemo } from 'react';
import * as THREE from 'three';
import type { ScenePrimitive, SceneColor, Vec3 } from '../../../viewport/types';

interface Props {
  primitives: ScenePrimitive[];
}

export function ScenePolygons({ primitives }: Props) {
  const polygons = primitives.filter((p) => p.kind === 'polygon');

  return (
    <>
      {polygons.map((polygon, index) => (
        <PolygonMesh key={index} corners={polygon.corners} color={polygon.color} />
      ))}
    </>
  );
}

function PolygonMesh({ corners, color }: { corners: Vec3[]; color: SceneColor }) {
  const geometry = useMemo(() => {
    const geom = new THREE.BufferGeometry();
    const positions = new Float32Array(corners.length * 3);
    corners.forEach((corner, i) => {
      positions[i * 3] = corner.x;
      positions[i * 3 + 1] = corner.y;
      positions[i * 3 + 2] = corner.z;
    });
    geom.setAttribute('position', new THREE.BufferAttribute(positions, 3));

    // Fan triangulation from vertex 0 - valid since every polygon here (window/light-cone
    // faces clipped against convex half-spaces, and rectangular landing patches) is convex.
    const indices: number[] = [];
    for (let i = 1; i < corners.length - 1; i++) {
      indices.push(0, i, i + 1);
    }
    geom.setIndex(indices);
    geom.computeVertexNormals();
    return geom;
  }, [corners]);

  return (
    <mesh geometry={geometry}>
      <meshBasicMaterial
        color={`rgb(${color.r}, ${color.g}, ${color.b})`}
        transparent
        opacity={color.a / 255}
        side={THREE.DoubleSide}
        depthWrite={false}
      />
    </mesh>
  );
}
