import { useCalculatorStore } from '../../state/useCalculatorStore';

export function RoomPanel() {
  const { roomWidth, roomLength, roomHeight, roomRotationDegrees, setRoomWidth, setRoomLength, setRoomHeight, setRoomRotationDegrees } =
    useCalculatorStore();

  return (
    <section className="panel">
      <h2>Room</h2>
      <label>
        Width (m)
        <input type="number" step="0.5" value={roomWidth} onChange={(event) => setRoomWidth(Number(event.target.value))} />
      </label>
      <label>
        Length (m)
        <input type="number" step="0.5" value={roomLength} onChange={(event) => setRoomLength(Number(event.target.value))} />
      </label>
      <label>
        Height (m)
        <input type="number" step="0.5" value={roomHeight} onChange={(event) => setRoomHeight(Number(event.target.value))} />
      </label>
      <label>
        Rotation (&deg;)
        <input
          type="number"
          step="0.5"
          value={roomRotationDegrees}
          onChange={(event) => setRoomRotationDegrees(Number(event.target.value))}
        />
      </label>
    </section>
  );
}
