import type { AlignmentMatchDto } from '../../api/types';
import { useCalculatorStore } from '../../state/useCalculatorStore';

interface Props {
  match: AlignmentMatchDto;
}

// Reproduces AlignmentMatchCard.cs's DateLabel/TimeLabel/AngleLabel formatting ("MMM d",
// "h:mm tt", "F2° off") and MainViewModel's OnSelectedAlignmentMatchChanged (clicking a card
// jumps Date/TimeOfDay to that match).
export function AlignmentMatchCard({ match }: Props) {
  const setDate = useCalculatorStore((state) => state.setDate);
  const setTime = useCalculatorStore((state) => state.setTime);

  const date = new Date(match.year, match.month - 1, match.day, match.hour, match.minute);
  const dateLabel = date.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
  const timeLabel = date.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
  const angleLabel = `${match.angleDifferenceDegrees.toFixed(2)}° off`;

  return (
    <button
      type="button"
      className="matchCard"
      onClick={() => {
        setDate({ year: match.year, month: match.month, day: match.day });
        setTime({ hour: match.hour, minute: match.minute });
      }}
    >
      <div className="matchCard-date">{dateLabel}</div>
      <div className="matchCard-time">{timeLabel}</div>
      <div className="matchCard-angle">{angleLabel}</div>
    </button>
  );
}
