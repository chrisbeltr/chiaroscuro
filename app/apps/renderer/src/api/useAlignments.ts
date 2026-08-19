import { useQuery } from '@tanstack/react-query';
import { apiPost } from './client';
import type { AlignmentsRequest, AlignmentsResponse } from './types';
import { useCalculatorStore } from '../state/useCalculatorStore';
import { useDebouncedValue } from '../hooks/useDebouncedValue';

/** Mirrors the inverse-solver half of MainViewModel.Recalculate() (InverseAlignmentSolver +
 * AlignmentMatchSummarizer) - a ~35,000-iteration year sweep server-side, so unlike
 * useIllumination this debounces its request rather than firing on every keystroke. */
export function useAlignments() {
  const {
    latitude, longitude, date, utcOffsetHours,
    roomWidth, roomLength, roomHeight, roomRotationDegrees,
    windowWall, windowHorizontalOffset, windowSillHeight, windowWidth, windowHeight,
    targetX, targetY, targetZ, toleranceDegrees,
  } = useCalculatorStore();

  const ready = latitude !== null && longitude !== null;

  const request: AlignmentsRequest | null = ready
    ? {
        room: { width: roomWidth, length: roomLength, height: roomHeight, rotationDegrees: roomRotationDegrees },
        window: {
          wall: windowWall,
          horizontalOffset: windowHorizontalOffset,
          sillHeight: windowSillHeight,
          width: windowWidth,
          height: windowHeight,
        },
        target: { x: targetX, y: targetY, z: targetZ },
        latitude,
        longitude,
        utcOffsetHours,
        // The sweep starts at the currently selected date, exactly as MainViewModel.Recalculate()
        // passes its own `localDate` (built from the same Date field used for illumination) as
        // InverseAlignmentSolver's startDate - not always January 1st.
        year: date.year,
        month: date.month,
        day: date.day,
        toleranceDegrees,
      }
    : null;

  const debouncedRequest = useDebouncedValue(request, 400);

  return useQuery({
    queryKey: ['alignments', debouncedRequest],
    queryFn: () => apiPost<AlignmentsResponse, AlignmentsRequest>('/api/solar/alignments', debouncedRequest!),
    enabled: debouncedRequest !== null,
  });
}
