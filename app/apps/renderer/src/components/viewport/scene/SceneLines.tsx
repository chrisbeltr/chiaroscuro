import { Line } from '@react-three/drei';
import type { ScenePrimitive } from '../../../viewport/types';

interface Props {
  primitives: ScenePrimitive[];
}

export function SceneLines({ primitives }: Props) {
  const lines = primitives.filter((p) => p.kind === 'line');

  return (
    <>
      {lines.map((line, index) => (
        <Line
          key={index}
          points={[
            [line.start.x, line.start.y, line.start.z],
            [line.end.x, line.end.y, line.end.z],
          ]}
          color={toCssColor(line.color)}
          transparent={line.color.a < 255}
          opacity={line.color.a / 255}
          lineWidth={1}
        />
      ))}
    </>
  );
}

function toCssColor(color: { r: number; g: number; b: number }): string {
  return `rgb(${color.r}, ${color.g}, ${color.b})`;
}
