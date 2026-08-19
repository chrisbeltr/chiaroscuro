import { useCalculatorStore } from '../../state/useCalculatorStore';
import { useGeolocation } from '../../api/useGeolocation';
import { pad2, pad4, parseOptionalNumber } from './formatting';

export function LocationTimePanel() {
  const {
    latitude, longitude, date, time, utcOffsetHours,
    setLatitude, setLongitude, setDate, setTime, setUtcOffsetHours,
    jumpToNow, applyGeolocation,
  } = useCalculatorStore();
  const { resolve, isLoading } = useGeolocation();

  const dateValue = `${pad4(date.year)}-${pad2(date.month)}-${pad2(date.day)}`;
  const timeValue = `${pad2(time.hour)}:${pad2(time.minute)}`;

  return (
    <section className="panel">
      <h2>Location &amp; Time</h2>
      <label>
        Latitude
        <input
          type="number"
          step="0.0001"
          value={latitude ?? ''}
          onChange={(event) => setLatitude(parseOptionalNumber(event.target.value))}
        />
      </label>
      <label>
        Longitude
        <input
          type="number"
          step="0.0001"
          value={longitude ?? ''}
          onChange={(event) => setLongitude(parseOptionalNumber(event.target.value))}
        />
      </label>
      <label>
        Date
        <input
          type="date"
          value={dateValue}
          onChange={(event) => {
            const [year, month, day] = event.target.value.split('-').map(Number);
            if (year && month && day) {
              setDate({ year, month, day });
            }
          }}
        />
      </label>
      <label>
        Time
        <input
          type="time"
          value={timeValue}
          onChange={(event) => {
            const [hour, minute] = event.target.value.split(':').map(Number);
            if (!Number.isNaN(hour) && !Number.isNaN(minute)) {
              setTime({ hour, minute });
            }
          }}
        />
      </label>
      <label>
        UTC Offset (hours)
        <input
          type="number"
          step="0.25"
          value={utcOffsetHours}
          onChange={(event) => setUtcOffsetHours(Number(event.target.value))}
        />
      </label>
      <div className="panel-actions">
        <button type="button" onClick={jumpToNow}>
          Now
        </button>
        <button
          type="button"
          disabled={isLoading}
          onClick={async () => {
            const location = await resolve();
            if (location) {
              applyGeolocation(location);
            }
          }}
        >
          My Location
        </button>
      </div>
    </section>
  );
}
