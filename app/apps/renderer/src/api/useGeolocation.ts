import { useCallback, useState } from 'react';
import { apiGet, isElectron } from './client';
import type { GeolocationResponse } from './types';

interface ResolvedLocation {
  latitude: number;
  longitude: number;
  utcOffsetHours: number;
}

/**
 * Dual strategy, matching the split kept from the original Avalonia heads: inside Electron,
 * Chromium's navigator.geolocation needs a Google API key to resolve, so that build calls the
 * backend's IP-lookup endpoint instead (see Chiaroscuro.Api's GeolocationEndpoints - ported
 * from IpGeolocation.cs). A plain browser tab has its own vendor geolocation implementation and
 * needs no API key, so the hosted-web build calls navigator.geolocation directly (mirroring
 * Chiaroscuro.Wasm/BrowserGeolocation.cs) and never hits the backend for this at all.
 */
export function useGeolocation() {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const resolve = useCallback(async (): Promise<ResolvedLocation | null> => {
    setIsLoading(true);
    setError(null);
    try {
      return isElectron() ? await resolveViaBackend() : await resolveViaBrowser();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to determine location.');
      return null;
    } finally {
      setIsLoading(false);
    }
  }, []);

  return { resolve, isLoading, error };
}

async function resolveViaBackend(): Promise<ResolvedLocation | null> {
  const response = await apiGet<GeolocationResponse>('/api/geolocation/ip');
  return response.success && response.latitude !== null && response.longitude !== null && response.utcOffsetHours !== null
    ? { latitude: response.latitude, longitude: response.longitude, utcOffsetHours: response.utcOffsetHours }
    : null;
}

function resolveViaBrowser(): Promise<ResolvedLocation | null> {
  return new Promise((resolve) => {
    if (!('geolocation' in navigator)) {
      resolve(null);
      return;
    }

    navigator.geolocation.getCurrentPosition(
      (position) =>
        resolve({
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
          utcOffsetHours: -new Date().getTimezoneOffset() / 60,
        }),
      () => resolve(null),
      { timeout: 8000 },
    );
  });
}
