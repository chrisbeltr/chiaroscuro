import { contextBridge } from 'electron';

// The renderer's only IPC surface: the resolved backend base URL, passed down from main.ts
// via additionalArguments at BrowserWindow creation time. api/client.ts's isElectron() checks
// for window.chiaroscuro's presence to decide between this and a hosted-web base URL, and
// api/useGeolocation.ts uses the same check to route geolocation through the backend instead
// of navigator.geolocation.
contextBridge.exposeInMainWorld('chiaroscuro', {
  apiBaseUrl: readApiBaseUrlFromArgv(),
});

function readApiBaseUrlFromArgv(): string {
  const prefix = '--chiaroscuro-api-base-url=';
  const arg = process.argv.find((value) => value.startsWith(prefix));
  if (!arg) {
    throw new Error('Missing --chiaroscuro-api-base-url launch argument.');
  }
  return arg.slice(prefix.length);
}
