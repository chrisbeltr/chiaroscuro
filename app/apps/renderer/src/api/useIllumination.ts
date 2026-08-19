import { useQuery } from '@tanstack/react-query';
import { apiPost } from './client';
import type { IlluminationRequest, IlluminationResponse } from './types';
import { useCalculatorStore } from '../state/useCalculatorStore';

/** Mirrors the illumination half of MainViewModel.Recalculate() (SolarCalculator + RayTracer).
 * Cheap enough to fire on every relevant field change with no debouncing, unlike useAlignments. */
export function useIllumination() {
  const {
    latitude, longitude, date, time, utcOffsetHours,
    roomWidth, roomLength, roomHeight, roomRotationDegrees,
    windowWall, windowHorizontalOffset, windowSillHeight, windowWidth, windowHeight,
  } = useCalculatorStore();

  const request: IlluminationRequest | null =
    latitude !== null && longitude !== null
      ? {
          room: { width: roomWidth, length: roomLength, height: roomHeight, rotationDegrees: roomRotationDegrees },
          window: {
            wall: windowWall,
            horizontalOffset: windowHorizontalOffset,
            sillHeight: windowSillHeight,
            width: windowWidth,
            height: windowHeight,
          },
          latitude,
          longitude,
          year: date.year,
          month: date.month,
          day: date.day,
          hour: time.hour,
          minute: time.minute,
          utcOffsetHours,
        }
      : null;

  return useQuery({
    queryKey: ['illumination', request],
    queryFn: () => apiPost<IlluminationResponse, IlluminationRequest>('/api/solar/illuminate', request!),
    enabled: request !== null,
  });
}
