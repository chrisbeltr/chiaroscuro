// Local form-input state, one field per MainViewModel.cs [ObservableProperty] input. Server-
// derived state (illumination, alignment matches) deliberately does NOT live here - that's
// react-query's job (see api/useIllumination.ts, api/useAlignments.ts), mirroring the plan's
// "server cache vs. local form state" split now that recomputation happens over HTTP instead
// of in-process.

import { create } from 'zustand';
import type { WallOrientation } from '../api/types';

export interface CalendarDate {
  year: number;
  month: number;
  day: number;
}

export interface TimeOfDay {
  hour: number;
  minute: number;
}

interface CalculatorState {
  latitude: number | null;
  longitude: number | null;
  date: CalendarDate;
  time: TimeOfDay;
  utcOffsetHours: number;

  roomWidth: number;
  roomLength: number;
  roomHeight: number;
  roomRotationDegrees: number;

  windowWall: WallOrientation;
  windowHorizontalOffset: number;
  windowSillHeight: number;
  windowWidth: number;
  windowHeight: number;

  targetX: number;
  targetY: number;
  targetZ: number;
  toleranceDegrees: number;

  /** Once the inverse-solver target has been set (by the user, or seeded from the first
   * illumination result), it's never re-seeded automatically again - mirrors MainViewModel's
   * constructor, which seeds TargetX/Y/Z exactly once. */
  hasSeededTarget: boolean;

  setLatitude: (value: number | null) => void;
  setLongitude: (value: number | null) => void;
  setDate: (value: CalendarDate) => void;
  setTime: (value: TimeOfDay) => void;
  setUtcOffsetHours: (value: number) => void;
  setRoomWidth: (value: number) => void;
  setRoomLength: (value: number) => void;
  setRoomHeight: (value: number) => void;
  setRoomRotationDegrees: (value: number) => void;
  setWindowWall: (value: WallOrientation) => void;
  setWindowHorizontalOffset: (value: number) => void;
  setWindowSillHeight: (value: number) => void;
  setWindowWidth: (value: number) => void;
  setWindowHeight: (value: number) => void;
  setTargetX: (value: number) => void;
  setTargetY: (value: number) => void;
  setTargetZ: (value: number) => void;
  setToleranceDegrees: (value: number) => void;

  /** Mirrors MainViewModel's JumpToNow(): resets date/time/UTC offset to the current moment. */
  jumpToNow: () => void;
  /** Mirrors MainViewModel's JumpToCurrentLocation(): applies a resolved geolocation result. */
  applyGeolocation: (location: { latitude: number; longitude: number; utcOffsetHours: number }) => void;
  /** Mirrors MainViewModel's constructor-time target seed - a no-op once hasSeededTarget is true. */
  seedTargetIfUnset: (point: { x: number; y: number; z: number }) => void;
}

function todayLocal(): CalendarDate {
  const now = new Date();
  return { year: now.getFullYear(), month: now.getMonth() + 1, day: now.getDate() };
}

function nowTimeLocal(): TimeOfDay {
  const now = new Date();
  return { hour: now.getHours(), minute: now.getMinutes() };
}

function localUtcOffsetHours(): number {
  // Date.getTimezoneOffset() returns minutes to ADD to local time to reach UTC - the negative
  // of Chiaroscuro's UtcOffsetHours convention (e.g. EST is -4/-5, not +4/+5) - so flip its sign.
  return -new Date().getTimezoneOffset() / 60;
}

export const useCalculatorStore = create<CalculatorState>((set, get) => ({
  // Defaults to New York City, matching MainViewModel.cs's own default (verified against
  // suncalc.org during manual testing there).
  latitude: 40.7128,
  longitude: -74.006,
  date: todayLocal(),
  time: nowTimeLocal(),
  utcOffsetHours: localUtcOffsetHours(),

  roomWidth: 6,
  roomLength: 5,
  roomHeight: 3,
  roomRotationDegrees: 0,

  windowWall: 'South',
  windowHorizontalOffset: 0,
  windowSillHeight: 1,
  windowWidth: 1.2,
  windowHeight: 1.5,

  targetX: 0,
  targetY: 0,
  targetZ: 0,
  toleranceDegrees: 2,

  hasSeededTarget: false,

  setLatitude: (value) => set({ latitude: value }),
  setLongitude: (value) => set({ longitude: value }),
  setDate: (value) => set({ date: value }),
  setTime: (value) => set({ time: value }),
  setUtcOffsetHours: (value) => set({ utcOffsetHours: value }),
  setRoomWidth: (value) => set({ roomWidth: value }),
  setRoomLength: (value) => set({ roomLength: value }),
  setRoomHeight: (value) => set({ roomHeight: value }),
  setRoomRotationDegrees: (value) => set({ roomRotationDegrees: value }),
  setWindowWall: (value) => set({ windowWall: value }),
  setWindowHorizontalOffset: (value) => set({ windowHorizontalOffset: value }),
  setWindowSillHeight: (value) => set({ windowSillHeight: value }),
  setWindowWidth: (value) => set({ windowWidth: value }),
  setWindowHeight: (value) => set({ windowHeight: value }),
  setTargetX: (value) => set({ targetX: value, hasSeededTarget: true }),
  setTargetY: (value) => set({ targetY: value, hasSeededTarget: true }),
  setTargetZ: (value) => set({ targetZ: value, hasSeededTarget: true }),
  setToleranceDegrees: (value) => set({ toleranceDegrees: value }),

  jumpToNow: () => set({ date: todayLocal(), time: nowTimeLocal(), utcOffsetHours: localUtcOffsetHours() }),

  applyGeolocation: (location) =>
    set({
      latitude: location.latitude,
      longitude: location.longitude,
      utcOffsetHours: location.utcOffsetHours,
    }),

  seedTargetIfUnset: (point) => {
    if (get().hasSeededTarget) {
      return;
    }
    set({ targetX: point.x, targetY: point.y, targetZ: point.z, hasSeededTarget: true });
  },
}));
