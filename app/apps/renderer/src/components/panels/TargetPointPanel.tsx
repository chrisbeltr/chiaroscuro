import { useCalculatorStore } from '../../state/useCalculatorStore';

export function TargetPointPanel() {
  const { targetX, targetY, targetZ, toleranceDegrees, setTargetX, setTargetY, setTargetZ, setToleranceDegrees } =
    useCalculatorStore();

  return (
    <section className="panel">
      <h2>Target Point</h2>
      <label>
        X
        <input type="number" step="0.5" value={targetX} onChange={(event) => setTargetX(Number(event.target.value))} />
      </label>
      <label>
        Y
        <input type="number" step="0.5" value={targetY} onChange={(event) => setTargetY(Number(event.target.value))} />
      </label>
      <label>
        Z
        <input type="number" step="0.5" value={targetZ} onChange={(event) => setTargetZ(Number(event.target.value))} />
      </label>
      <label>
        Tolerance (&deg;)
        <input
          type="number"
          step="0.5"
          value={toleranceDegrees}
          onChange={(event) => setToleranceDegrees(Number(event.target.value))}
        />
      </label>
    </section>
  );
}
