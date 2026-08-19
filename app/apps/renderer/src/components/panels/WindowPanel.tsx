import type { WallOrientation } from '../../api/types';
import { useCalculatorStore } from '../../state/useCalculatorStore';

// No NoWheelComboBox equivalent needed here: that Avalonia control existed purely to stop the
// mouse wheel from changing the ComboBox's value while scrolling the parameter panel over it -
// an HTML <select> has no such behavior, so this is a plain select.
const WALL_ORIENTATIONS: WallOrientation[] = ['North', 'South', 'East', 'West'];

export function WindowPanel() {
  const {
    windowWall, windowHorizontalOffset, windowSillHeight, windowWidth, windowHeight,
    setWindowWall, setWindowHorizontalOffset, setWindowSillHeight, setWindowWidth, setWindowHeight,
  } = useCalculatorStore();

  return (
    <section className="panel">
      <h2>Window</h2>
      <label>
        Wall
        <select value={windowWall} onChange={(event) => setWindowWall(event.target.value as WallOrientation)}>
          {WALL_ORIENTATIONS.map((wall) => (
            <option key={wall} value={wall}>
              {wall}
            </option>
          ))}
        </select>
      </label>
      <label>
        Horizontal Offset (m)
        <input
          type="number"
          step="0.5"
          value={windowHorizontalOffset}
          onChange={(event) => setWindowHorizontalOffset(Number(event.target.value))}
        />
      </label>
      <label>
        Sill Height (m)
        <input
          type="number"
          step="0.5"
          value={windowSillHeight}
          onChange={(event) => setWindowSillHeight(Number(event.target.value))}
        />
      </label>
      <label>
        Width (m)
        <input type="number" step="0.5" value={windowWidth} onChange={(event) => setWindowWidth(Number(event.target.value))} />
      </label>
      <label>
        Height (m)
        <input type="number" step="0.5" value={windowHeight} onChange={(event) => setWindowHeight(Number(event.target.value))} />
      </label>
    </section>
  );
}
