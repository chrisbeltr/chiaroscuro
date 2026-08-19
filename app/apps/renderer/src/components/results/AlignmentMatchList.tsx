import { useAlignments } from '../../api/useAlignments';
import { AlignmentMatchCard } from './AlignmentMatchCard';

// Reproduces MainView.axaml's horizontally-scrolling ListBox of inverse-solver result cards,
// including MainViewModel.Recalculate()'s "no matches" placeholder card.
export function AlignmentMatchList() {
  const alignments = useAlignments();
  const matches = alignments.data?.matches ?? [];

  return (
    <section className="alignment-match-list">
      {matches.length === 0 ? (
        <div className="matchCard">
          <div>No matches found.</div>
          <div>Try a higher tolerance or different position.</div>
        </div>
      ) : (
        matches.map((match) => (
          <AlignmentMatchCard key={`${match.year}-${match.month}-${match.day}-${match.hour}-${match.minute}`} match={match} />
        ))
      )}
    </section>
  );
}
