// The renderer's base URL resolution: inside Electron, the main process passes the sidecar
// backend's chosen port to the preload script's contextBridge (window.chiaroscuro.apiBaseUrl -
// see apps/electron/src/preload.ts); everywhere else (the plain-web/browser build, or the Vite
// dev server) it falls back to VITE_API_BASE_URL or the backend's fixed dev port (see
// src/Chiaroscuro.Api/Properties/launchSettings.json).

declare global {
  interface Window {
    chiaroscuro?: {
      apiBaseUrl: string;
    };
  }
}

const DEV_FALLBACK_BASE_URL = 'http://127.0.0.1:5259';

export function isElectron(): boolean {
  return typeof window !== 'undefined' && Boolean(window.chiaroscuro);
}

export function getApiBaseUrl(): string {
  if (typeof window !== 'undefined' && window.chiaroscuro?.apiBaseUrl) {
    return window.chiaroscuro.apiBaseUrl;
  }
  return import.meta.env.VITE_API_BASE_URL ?? DEV_FALLBACK_BASE_URL;
}

async function request<TResponse>(path: string, init?: RequestInit): Promise<TResponse> {
  const response = await fetch(`${getApiBaseUrl()}${path}`, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...init?.headers },
  });

  if (!response.ok) {
    throw new Error(`${init?.method ?? 'GET'} ${path} failed with status ${response.status}`);
  }

  return (await response.json()) as TResponse;
}

export function apiGet<TResponse>(path: string): Promise<TResponse> {
  return request<TResponse>(path);
}

export function apiPost<TResponse, TBody>(path: string, body: TBody): Promise<TResponse> {
  return request<TResponse>(path, { method: 'POST', body: JSON.stringify(body) });
}
